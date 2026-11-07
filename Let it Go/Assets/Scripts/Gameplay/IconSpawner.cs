using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Spawns Work and Happiness icons at the top of the play area. They fall down.
// When the hand closes (fist) over an icon, the icon is caught and its effect applied.
public class IconSpawner : MonoBehaviour
{
    public enum Kind { Work, Happiness }

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
    public float iconSize = 150f;
    public float sidePadding = 140f;
    public float topReserved = 200f;    // icons start under the status bar area
    public Vector2 spawnInterval = new Vector2(1.2f, 0.55f);   // start -> end of ramp
    public Vector2 fallSpeed = new Vector2(200f, 380f);        // start -> end of ramp
    public float rampSeconds = 120f;
    [Range(0, 1)] public float workChance = 0.55f;

    [Header("Grab")]
    [Tooltip("Seconds after the hand closes in which an icon touching the hand is still caught.")]
    public float grabWindow = 0.15f;

    [NonSerialized] public bool Paused = true;

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
            p.x = f.x + Mathf.Sin(Time.time * 2f + f.swayPhase) * 25f;
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
        GameManager.Instance?.ApplyItem(f.def);
        Popup(f.def, f.rt.anchoredPosition);
        f.rt.gameObject.AddComponent<CaughtFx>().target = hp;
        return true;
    }

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
        Kind kind = UnityEngine.Random.value < workChance ? Kind.Work : Kind.Happiness;
        float total = 0;
        foreach (var it in items) if (it.kind == kind) total += it.weight;
        float r = UnityEngine.Random.value * total;
        foreach (var it in items)
        {
            if (it.kind != kind) continue;
            r -= it.weight;
            if (r <= 0) return it;
        }
        return null;
    }

    void Popup(ItemDef def, Vector2 pos)
    {
        var parts = new List<string>();
        void Add(float v, string name, string color)
        {
            if (Mathf.Approximately(v, 0)) return;
            parts.Add($"<color={color}>{(v > 0 ? "+" : "")}{v:0} {name}</color>");
        }
        Add(def.money, "เงิน", "#D99A00");
        Add(def.happiness, "สุข", "#E8457A");
        Add(def.energy, "พลัง", "#2E8FD6");

        var go = new GameObject("Popup", typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        rt.SetParent(area, false);
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.sizeDelta = new Vector2(420, 160);
        rt.anchoredPosition = pos + new Vector2(0, 90);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font) t.font = font;
        t.fontSize = 38;
        t.alignment = TextAlignmentOptions.Center;
        t.outlineWidth = 0.25f;
        t.outlineColor = Color.white;
        t.raycastTarget = false;
        t.text = $"<b>{def.label}</b>\n" + string.Join("  ", parts);
        go.AddComponent<FloatUpFade>();
    }

    // Shrinks the caught icon into the hand.
    class CaughtFx : MonoBehaviour
    {
        public Vector2 target;
        float t;
        void Update()
        {
            t += Time.unscaledDeltaTime * 5f;
            var rt = (RectTransform)transform;
            rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, target, t);
            rt.localScale = Vector3.one * Mathf.Max(0, 1.2f - t);
            if (t >= 1.2f) Destroy(gameObject);
        }
    }

    class FloatUpFade : MonoBehaviour
    {
        float t;
        TMP_Text text;
        void Awake() => text = GetComponent<TMP_Text>();
        void Update()
        {
            t += Time.unscaledDeltaTime;
            ((RectTransform)transform).anchoredPosition += Vector2.up * 80f * Time.unscaledDeltaTime;
            text.alpha = Mathf.Clamp01(1.4f - t);
            if (t > 1.4f) Destroy(gameObject);
        }
    }
}
