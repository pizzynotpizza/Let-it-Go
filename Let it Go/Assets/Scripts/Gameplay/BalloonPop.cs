using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Balloon pop effect for a UI icon (Image on a RectTransform).
// Add it to the icon: the icon swells for a moment, then bursts into 4 pieces
// with a flash, an expanding ring and confetti. The icon object is destroyed at the end.
public class BalloonPop : MonoBehaviour
{
    public Color color = Color.white;
    public float swellTime = 0.08f;
    public float burstTime = 0.75f;

    static Sprite circleSprite;
    static Sprite ringSprite;

    class Bit
    {
        public RectTransform rt;
        public Graphic g;
        public Vector2 vel;
        public float spin;
        public float startScale;
        public float shrink;
    }

    readonly List<Bit> bits = new List<Bit>();
    RectTransform rt;
    Image icon;
    RectTransform ring;
    Image ringImg;
    RectTransform flash;
    Image flashImg;
    float t;
    bool burst;
    Vector3 baseScale;

    void Awake()
    {
        rt = (RectTransform)transform;
        icon = GetComponent<Image>();
        baseScale = rt.localScale;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        t += dt;

        if (!burst)
        {
            // Swell with a little wobble, like air pressure building up.
            float k = Mathf.Clamp01(t / swellTime);
            float sx = 1f + 0.35f * k;
            float sy = 1f + 0.25f * k;
            rt.localScale = new Vector3(baseScale.x * sx, baseScale.y * sy, 1);
            if (k >= 1f) Burst();
            return;
        }

        float bt = t - swellTime;
        float u = Mathf.Clamp01(bt / burstTime);

        foreach (var b in bits)
        {
            b.vel.y -= 1400f * dt;              // gravity
            b.vel *= 1f - 1.5f * dt;            // air drag
            b.rt.anchoredPosition += b.vel * dt;
            b.rt.localRotation *= Quaternion.Euler(0, 0, b.spin * dt);
            b.rt.localScale = Vector3.one * b.startScale * Mathf.Max(0, 1f - u * b.shrink);
            var c = b.g.color; c.a = 1f - u * u; b.g.color = c;
        }

        if (ring)
        {
            float e = 1f - (1f - u) * (1f - u) * (1f - u);
            ring.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.9f, e);
            var c = ringImg.color; c.a = Mathf.Clamp01(1f - u * 1.6f); ringImg.color = c;
        }
        if (flash)
        {
            float f = Mathf.Clamp01(bt / 0.15f);
            flash.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.3f, f);
            var c = flashImg.color; c.a = 0.9f * (1f - f); flashImg.color = c;
        }

        if (u >= 1f) Destroy(gameObject);
    }

    void Burst()
    {
        burst = true;
        rt.localScale = baseScale;
        rt.localRotation = Quaternion.identity;
        Vector2 size = rt.rect.size;
        Sprite sprite = icon ? icon.sprite : null;
        if (icon) icon.enabled = false;

        EnsureSprites();

        // Flash + ring behind the pieces.
        flash = MakeImage("Flash", circleSprite, new Color(1, 1, 1, 0.9f), size * 0.9f, out flashImg);
        ring = MakeImage("Ring", ringSprite, color, size * 1.1f, out ringImg);

        // Icon breaks into 4 pieces.
        if (sprite != null)
        {
            Rect tr = sprite.textureRect;
            float aspect = tr.width / tr.height;
            Vector2 shown = aspect >= 1 ? new Vector2(size.x, size.x / aspect) : new Vector2(size.y * aspect, size.y);
            for (int ix = 0; ix < 2; ix++)
            for (int iy = 0; iy < 2; iy++)
            {
                var sub = new Rect(tr.x + ix * tr.width * 0.5f, tr.y + iy * tr.height * 0.5f, tr.width * 0.5f, tr.height * 0.5f);
                var piece = Sprite.Create(sprite.texture, sub, new Vector2(0.5f, 0.5f), sprite.pixelsPerUnit);
                Vector2 dir = new Vector2(ix == 0 ? -1 : 1, iy == 0 ? -1 : 1);
                var prt = MakeImage("Piece", piece, Color.white, shown * 0.5f, out var pimg);
                prt.anchoredPosition = Vector2.Scale(dir, shown * 0.25f);
                pimg.preserveAspect = false;
                bits.Add(new Bit
                {
                    rt = prt, g = pimg,
                    vel = dir.normalized * Random.Range(380f, 560f) + Vector2.up * 260f,
                    spin = Random.Range(-540f, 540f),
                    startScale = 1f, shrink = 0.6f
                });
                Destroy(piece, burstTime + swellTime + 0.1f);
            }
        }

        // Confetti: coloured and white dots shooting out.
        for (int i = 0; i < 16; i++)
        {
            float a = (i / 16f) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float s = Random.Range(18f, 34f);
            Color c = i % 3 == 0 ? Color.white : color;
            var drt = MakeImage("Dot", circleSprite, c, new Vector2(s, s), out var dimg);
            drt.anchoredPosition = dir * size.x * 0.25f;
            bits.Add(new Bit
            {
                rt = drt, g = dimg,
                vel = dir * Random.Range(500f, 900f),
                spin = 0, startScale = 1f, shrink = 1f
            });
        }
    }

    RectTransform MakeImage(string name, Sprite sprite, Color c, Vector2 size, out Image img)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var r = (RectTransform)go.transform;
        r.SetParent(transform, false);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = size;
        r.anchoredPosition = Vector2.zero;
        img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = c;
        img.raycastTarget = false;
        return r;
    }

    static void EnsureSprites()
    {
        if (circleSprite == null) circleSprite = MakeCircle(64, 0f);
        if (ringSprite == null) ringSprite = MakeCircle(128, 0.82f);
    }

    // Soft-edged disc (inner = 0) or ring (inner = inner radius as a fraction of the outer radius).
    static Sprite MakeCircle(int n, float inner)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var px = new Color32[n * n];
        float r = n * 0.5f;
        float edge = 1.5f;
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
            float a = Mathf.Clamp01((r - d) / edge);
            if (inner > 0) a *= Mathf.Clamp01((d - r * inner) / edge);
            px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
        }
        tex.SetPixels32(px);
        tex.Apply();
        var s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        s.hideFlags = HideFlags.DontSave;
        return s;
    }
}
