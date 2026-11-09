using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Spawns activity icons (Work / Relax / Recover) at the top of the play area. They fall down.
// When the hand closes (fist) over an icon, the icon pops like a balloon and its effect is applied.
public class IconSpawner : MonoBehaviour
{
    public enum Kind { Work, Relax, Recover }

    [Serializable]
    public class ItemDef
    {
        public string label;
        public Kind kind;
        public Sprite sprite;
        public float energy;
        public float money;
        public float happiness;
        [Min(0.01f)] public float weight = 1f;
    }

    public ItemDef[] items;
    public RectTransform area;          // full-screen layer the icons fall in
    public HandCursor hand;
    public TMP_FontAsset font;

    [Header("Spawn")]
    public float iconSize = 240f;
    public float sidePadding = 170f;
    public float topReserved = 220f;    // icons start under the status bar area
    public Vector2 spawnInterval = new Vector2(1.1f, 0.5f);   // start -> end of ramp
    public Vector2 fallSpeed = new Vector2(260f, 520f);        // start -> end of ramp
    public float rampSeconds = 120f;   // match GameManager.monthLength
    [Range(0, 1)] public float workChance = 0.41f;
    [Range(0, 1)] public float relaxChance = 0.30f;
    [Tooltip("Extra Recover chance when energy is low, so the player can still save the run.")]
    [Range(0, 1)] public float lowEnergyRecoverBonus = 0.12f;
    [Tooltip("Extra Work chance when money is low (taken from Relax), so the player can still earn.")]
    [Range(0, 1)] public float lowMoneyWorkBonus = 0.12f;

    [Header("Grab")]
    [Tooltip("Seconds after the hand closes in which an icon touching the hand is still caught.")]
    public float grabWindow = 0.3f;

    [NonSerialized] public bool Paused = true;

    static readonly Color WorkColor = new Color(0.85f, 0.6f, 0f);
    static readonly Color RelaxColor = new Color(0.91f, 0.27f, 0.48f);
    static readonly Color RecoverColor = new Color(0.18f, 0.56f, 0.84f);

    class Falling
    {
        public ItemDef def;
        public RectTransform rt;
        public float speed;
        public float swayPhase;
        public float x;
    }

    readonly List<Falling> live = new List<Falling>();
    float spawnTimer;
    float elapsed;
    float grabTimer;

    void Update()
    {
        if (Paused) return;
        float dt = Time.deltaTime;
        elapsed += dt;
        float ramp = Mathf.Clamp01(elapsed / rampSeconds);

        spawnTimer -= dt;
        if (spawnTimer <= 0)
        {
            spawnTimer = Mathf.Lerp(spawnInterval.x, spawnInterval.y, ramp) * UnityEngine.Random.Range(0.8f, 1.2f);
            Spawn(Mathf.Lerp(fallSpeed.x, fallSpeed.y, ramp));
        }

        for (int i = live.Count - 1; i >= 0; i--)
        {
            var f = live[i];
            var p = f.rt.anchoredPosition;
            p.y -= f.speed * dt;
            p.x = f.x + Mathf.Sin(Time.time * 2.4f + f.swayPhase) * 45f;
            f.rt.anchoredPosition = p;
            f.rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 2.5f + f.swayPhase) * 10f);
            if (p.y < -iconSize)
            {
                Destroy(f.rt.gameObject);
                live.RemoveAt(i);
            }
        }

        var tracker = HandTrackingClient.Instance;
        if (tracker == null || hand == null) return;
        if (tracker.FistDown) grabTimer = grabWindow;
        if (grabTimer > 0)
        {
            grabTimer -= dt;
            if (TryGrab()) grabTimer = 0;
        }
    }

    bool TryGrab()
    {
        Vector2 hp = hand.CanvasPosition;
        float reach = hand.grabRadius + iconSize * 0.5f;
        int best = -1;
        float bestD = reach * reach;
        for (int i = 0; i < live.Count; i++)
        {
            float d = (live[i].rt.anchoredPosition - hp).sqrMagnitude;
            if (d < bestD) { bestD = d; best = i; }
        }
        if (best < 0) return false;

        var f = live[best];
        live.RemoveAt(best);
        Vector3 applied = GameManager.Instance != null ? GameManager.Instance.ApplyItem(f.def) : Vector3.zero;
        Popup(f.def, applied, f.rt.anchoredPosition);
        var pop = f.rt.gameObject.AddComponent<BalloonPop>();
        pop.color = KindColor(f.def.kind);
        return true;
    }

    static Color KindColor(Kind k) => k == Kind.Work ? WorkColor : k == Kind.Relax ? RelaxColor : RecoverColor;

    void Spawn(float speed)
    {
        ItemDef def = Pick();
        if (def == null || def.sprite == null) return;

        var go = new GameObject("Icon_" + def.label, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(area, false);
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.sizeDelta = Vector2.one * iconSize;
        var img = go.GetComponent<Image>();
        img.sprite = def.sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;

        float w = area.rect.width;
        float x = UnityEngine.Random.Range(sidePadding, w - sidePadding);
        rt.anchoredPosition = new Vector2(x, area.rect.height - topReserved + iconSize);
        live.Add(new Falling { def = def, rt = rt, speed = speed * UnityEngine.Random.Range(0.85f, 1.15f), swayPhase = UnityEngine.Random.value * 6.28f, x = x });
        // Hand cursor always drawn on top.
        if (hand) hand.transform.SetAsLastSibling();
    }

    ItemDef Pick()
    {
        float recoverBonus = 0, workBonus = 0;
        var gm = GameManager.Instance;
        if (gm != null && gm.EnergyFraction < 0.35f) recoverBonus = lowEnergyRecoverBonus;
        if (gm != null && gm.Money < gm.maxMoney * 0.3f) workBonus = lowMoneyWorkBonus;

        float work = workChance - recoverBonus * 0.6f + workBonus;
        float relax = Mathf.Max(0.05f, relaxChance - recoverBonus * 0.4f - workBonus);
        float r0 = UnityEngine.Random.value;
        Kind kind = r0 < work ? Kind.Work : r0 < work + relax ? Kind.Relax : Kind.Recover;

        float total = 0;
        foreach (var it in items) if (it.kind == kind) total += it.weight;
        if (total <= 0) return null;
        float r = UnityEngine.Random.value * total;
        foreach (var it in items)
        {
            if (it.kind != kind) continue;
            r -= it.weight;
            if (r <= 0) return it;
        }
        return null;
    }

    void Popup(ItemDef def, Vector3 applied, Vector2 pos)
    {
        var parts = new List<string>();
        void Add(float v, string name, string color)
        {
            if (Mathf.Abs(v) < 0.5f) return;
            parts.Add($"<color={color}>{(v > 0 ? "+" : "")}{v:0} {name}</color>");
        }
        Add(applied.y, "เงิน", "#2E9E3E");
        Add(applied.z, "สุข", "#D99A00");
        Add(applied.x, "พลัง", "#D32F2F");

        var go = new GameObject("Popup", typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        rt.SetParent(area, false);
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.sizeDelta = new Vector2(560, 180);
        rt.anchoredPosition = pos + new Vector2(0, 140);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font) t.font = font;
        t.fontSize = 46;
        t.alignment = TextAlignmentOptions.Center;
        t.outlineWidth = 0.25f;
        t.outlineColor = Color.white;
        t.raycastTarget = false;
        t.text = $"<b>{def.label}</b>\n" + string.Join("  ", parts);
        go.AddComponent<FloatUpFade>();
    }

    class FloatUpFade : MonoBehaviour
    {
        float t;
        TMP_Text text;
        void Awake() => text = GetComponent<TMP_Text>();
        void Update()
        {
            t += Time.unscaledDeltaTime;
            var rt = (RectTransform)transform;
            rt.anchoredPosition += Vector2.up * 90f * Time.unscaledDeltaTime;
            rt.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(t * 8f));
            text.alpha = Mathf.Clamp01(1.4f - t);
            if (t > 1.4f) Destroy(gameObject);
        }
    }
}
