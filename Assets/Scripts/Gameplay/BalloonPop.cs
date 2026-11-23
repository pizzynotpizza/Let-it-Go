using System.Collections.Generic; // ใช้ List และ Dictionary
using UnityEngine; // ใช้คลาสหลักของ Unity
using UnityEngine.UI; // ใช้ Image และ Graphic

// เอฟเฟกต์ลูกโป่งแตกสำหรับไอคอน UI (Image บน RectTransform)
// ใส่ให้ไอคอนแล้ว: ไอคอนจะพองขึ้นแป๊บนึง แล้วแตกเป็น 4 ชิ้น
// มีแฟลช วงแหวนขยาย และ confetti พอจบเอฟเฟกต์ไอคอนจะถูกลบทิ้ง
public class BalloonPop : MonoBehaviour // คลาสเอฟเฟกต์ไอคอนแตก
{
    // ---- ค่าที่ปรับได้: สีและเวลาของเอฟเฟกต์ ----
    public Color color = Color.white; // สีวงแหวนและ confetti (ตั้งตามชนิดไอคอน)
    public float swellTime = 0.08f; // เวลาพองก่อนแตก (วินาที)
    public float burstTime = 0.75f; // เวลาเอฟเฟกต์หลังแตกจนหายไป (วินาที)

    static Sprite circleSprite; // sprite วงกลมทึบ (ใช้กับแฟลชและ confetti) สร้างครั้งเดียวใช้ร่วมกัน
    static Sprite ringSprite; // sprite วงแหวน สร้างครั้งเดียวใช้ร่วมกัน

    // ---- ข้อมูลของเศษแต่ละชิ้น ----
    class Bit
    {
        public RectTransform rt; // RectTransform ของเศษ
        public Graphic g; // Graphic ไว้ปรับความโปร่งใส
        public Vector2 vel; // ความเร็วปัจจุบัน
        public float spin; // ความเร็วหมุน (องศา/วินาที)
        public float startScale; // ขนาดเริ่มต้น
        public float shrink; // อัตราการหดตัวตามเวลา
    }

    // ---- ตัวแปรภายใน ----
    readonly List<Bit> bits = new List<Bit>(); // รายการเศษทั้งหมด
    RectTransform rt; // RectTransform ของไอคอน
    Image icon; // Image ของไอคอน
    RectTransform ring; // วงแหวนที่ขยายออก
    Image ringImg; // Image ของวงแหวน
    RectTransform flash; // แฟลชสีขาว
    Image flashImg; // Image ของแฟลช
    float t; // เวลาที่ผ่านไปตั้งแต่เริ่มเอฟเฟกต์
    bool burst; // แตกไปแล้วหรือยัง
    Vector3 baseScale; // สเกลเดิมของไอคอน

    // ---- เก็บค่าเริ่มต้น ----
    void Awake()
    {
        rt = (RectTransform)transform; // ดึง RectTransform
        icon = GetComponent<Image>(); // ดึง Image ของไอคอน
        baseScale = rt.localScale; // จำสเกลเดิม
    }

    // ---- ทุกเฟรม: ช่วงพอง แล้วช่วงแตก ----
    void Update()
    {
        float dt = Time.unscaledDeltaTime; // เวลาระหว่างเฟรม
        t += dt; // นับเวลา

        if (!burst) // ---- ช่วงพองก่อนแตก ----
        {
            // พองขึ้นแบบไม่เท่ากัน เหมือนแรงลมดันข้างใน
            float k = Mathf.Clamp01(t / swellTime); // ความคืบหน้าของการพอง 0-1
            float sx = 1f + 0.35f * k; // ขยายแนวนอนสูงสุด 35%
            float sy = 1f + 0.25f * k; // ขยายแนวตั้งสูงสุด 25%
            rt.localScale = new Vector3(baseScale.x * sx, baseScale.y * sy, 1); // ตั้งสเกลใหม่
            if (k >= 1f) Burst(); // พองสุดแล้วให้แตก
            return; // จบเฟรมนี้
        }

        float bt = t - swellTime; // เวลาที่ผ่านไปหลังแตก
        float u = Mathf.Clamp01(bt / burstTime); // ความคืบหน้าหลังแตก 0-1

        // ---- ขยับเศษทุกชิ้น ----
        foreach (var b in bits) // ไล่ทุกเศษ
        {
            b.vel.y -= 1400f * dt; // แรงโน้มถ่วงดึงลง
            b.vel *= 1f - 1.5f * dt; // แรงต้านอากาศ ให้ช้าลงเรื่อยๆ
            b.rt.anchoredPosition += b.vel * dt; // ย้ายตำแหน่งตามความเร็ว
            b.rt.localRotation *= Quaternion.Euler(0, 0, b.spin * dt); // หมุนเศษ
            b.rt.localScale = Vector3.one * b.startScale * Mathf.Max(0, 1f - u * b.shrink); // หดเศษตามเวลา
            var c = b.g.color; c.a = 1f - u * u; b.g.color = c; // ค่อยๆ จางหาย
        }

        // ---- วงแหวนขยายออกแล้วจาง ----
        if (ring) // ถ้ามีวงแหวน
        {
            float e = 1f - (1f - u) * (1f - u) * (1f - u); // ease-out แบบ cubic (เร็วตอนแรก ช้าตอนท้าย)
            ring.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.9f, e); // ขยายจาก 0.4 เป็น 1.9 เท่า
            var c = ringImg.color; c.a = Mathf.Clamp01(1f - u * 1.6f); ringImg.color = c; // จางเร็วกว่าเศษ
        }
        // ---- แฟลชขาวช่วงสั้นๆ ----
        if (flash) // ถ้ามีแฟลช
        {
            float f = Mathf.Clamp01(bt / 0.15f); // แฟลชอยู่แค่ 0.15 วินาที
            flash.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.3f, f); // ขยายจาก 0.6 เป็น 1.3 เท่า
            var c = flashImg.color; c.a = 0.9f * (1f - f); flashImg.color = c; // จางหาย
        }

        if (u >= 1f) Destroy(gameObject); // เอฟเฟกต์จบแล้ว ลบไอคอนทิ้ง
    }

    // ---- จังหวะแตก: สร้างแฟลช วงแหวน เศษ 4 ชิ้น และ confetti ----
    void Burst()
    {
        burst = true; // ตั้งว่าแตกแล้ว
        rt.localScale = baseScale; // คืนสเกลเดิม
        rt.localRotation = Quaternion.identity; // คืนการหมุนเดิม
        Vector2 size = rt.rect.size; // ขนาดไอคอน
        Sprite sprite = icon ? icon.sprite : null; // รูปไอคอน (ไว้ตัดเป็นเศษ)
        if (icon) icon.enabled = false; // ซ่อนไอคอนจริง เหลือแต่เศษ

        EnsureSprites(); // ให้แน่ใจว่ามี sprite วงกลม/วงแหวนแล้ว

        // แฟลช + วงแหวน อยู่ข้างหลังเศษ
        flash = MakeImage("Flash", circleSprite, new Color(1, 1, 1, 0.9f), size * 0.9f, out flashImg); // สร้างแฟลชสีขาว
        ring = MakeImage("Ring", ringSprite, color, size * 1.1f, out ringImg); // สร้างวงแหวนสีตามไอคอน

        // ไอคอนแตกเป็น 4 ชิ้น
        if (sprite != null) // มีรูปไอคอน
        {
            Rect tr = sprite.textureRect; // พื้นที่ของรูปใน texture
            float aspect = tr.width / tr.height; // อัตราส่วนกว้าง/สูง
            Vector2 shown = aspect >= 1 ? new Vector2(size.x, size.x / aspect) : new Vector2(size.y * aspect, size.y); // ขนาดรูปที่แสดงจริง (แบบ preserveAspect)
            var pieces = Pieces(sprite); // ดึง sprite 4 ชิ้น (จาก cache)
            for (int ix = 0; ix < 2; ix++) // ซ้าย/ขวา
            for (int iy = 0; iy < 2; iy++) // ล่าง/บน
            {
                var piece = pieces[ix * 2 + iy]; // ชิ้นที่ตรงกับตำแหน่งนี้
                Vector2 dir = new Vector2(ix == 0 ? -1 : 1, iy == 0 ? -1 : 1); // ทิศที่ชิ้นนี้จะกระเด็นไป
                var prt = MakeImage("Piece", piece, Color.white, shown * 0.5f, out var pimg); // สร้างภาพเศษขนาดครึ่งหนึ่ง
                prt.anchoredPosition = Vector2.Scale(dir, shown * 0.25f); // วางให้ตรงมุมของรูปเดิม
                pimg.preserveAspect = false; // ไม่ต้องรักษาสัดส่วน
                bits.Add(new Bit // เพิ่มเข้ารายการเศษ
                {
                    rt = prt, g = pimg, // อ้างอิงออบเจกต์
                    vel = dir.normalized * Random.Range(380f, 560f) + Vector2.up * 260f, // พุ่งออกตามทิศ + เด้งขึ้นนิดหน่อย
                    spin = Random.Range(-540f, 540f), // หมุนสุ่ม
                    startScale = 1f, shrink = 0.6f // หดลง 60% ตอนจบ
                });
            }
        }

        // confetti: จุดสีกับจุดขาวพุ่งออกรอบทิศ (16 จุด)
        for (int i = 0; i < 16; i++) // สร้าง 16 จุด
        {
            float a = (i / 16f) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f); // มุมแต่ละจุด กระจายรอบวง + สุ่มนิดหน่อย
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); // ทิศจากมุม
            float s = Random.Range(18f, 34f); // ขนาดจุดสุ่ม
            Color c = i % 3 == 0 ? Color.white : color; // ทุกๆ 3 จุดเป็นสีขาว ที่เหลือสีไอคอน
            var drt = MakeImage("Dot", circleSprite, c, new Vector2(s, s), out var dimg); // สร้างจุด
            drt.anchoredPosition = dir * size.x * 0.25f; // เริ่มห่างจากกลางนิดหน่อย
            bits.Add(new Bit // เพิ่มเข้ารายการเศษ
            {
                rt = drt, g = dimg, // อ้างอิงออบเจกต์
                vel = dir * Random.Range(500f, 900f), // พุ่งออกเร็วสุ่ม
                spin = 0, startScale = 1f, shrink = 1f // ไม่หมุน หดจนหายตอนจบ
            });
        }
    }

    // ---- ตัวช่วยสร้าง Image ลูกของไอคอน ----
    RectTransform MakeImage(string name, Sprite sprite, Color c, Vector2 size, out Image img)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); // สร้างออบเจกต์ที่มี Image
        var r = (RectTransform)go.transform; // ดึง RectTransform
        r.SetParent(transform, false); // ใส่ไว้ใต้ไอคอน
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f); // ยึดกลาง
        r.sizeDelta = size; // ตั้งขนาด
        r.anchoredPosition = Vector2.zero; // วางกลางไอคอน
        img = go.GetComponent<Image>(); // ดึง Image ส่งออก
        img.sprite = sprite; // ตั้งรูป
        img.color = c; // ตั้งสี
        img.raycastTarget = false; // ไม่รับคลิก
        return r; // คืนค่า
    }

    // ---- cache ชิ้นส่วน 4 ชิ้นของแต่ละรูป สร้างครั้งเดียวแล้วใช้ซ้ำทุกครั้งที่แตก ----
    static readonly Dictionary<Sprite, Sprite[]> pieceCache = new Dictionary<Sprite, Sprite[]>(); // เก็บ sprite ต้นฉบับ -> 4 ชิ้น

    static Sprite[] Pieces(Sprite sprite)
    {
        if (pieceCache.TryGetValue(sprite, out var arr) && arr[0] != null) return arr; // มีใน cache แล้วใช้เลย
        Rect tr = sprite.textureRect; // พื้นที่รูปใน texture
        arr = new Sprite[4]; // เตรียม 4 ช่อง
        for (int ix = 0; ix < 2; ix++) // ครึ่งซ้าย/ขวา
        for (int iy = 0; iy < 2; iy++) // ครึ่งล่าง/บน
        {
            var sub = new Rect(tr.x + ix * tr.width * 0.5f, tr.y + iy * tr.height * 0.5f, tr.width * 0.5f, tr.height * 0.5f); // ตัดพื้นที่ 1/4
            var s = Sprite.Create(sprite.texture, sub, new Vector2(0.5f, 0.5f), sprite.pixelsPerUnit); // สร้าง sprite ใหม่จากพื้นที่นั้น
            s.hideFlags = HideFlags.DontSave; // ไม่ต้องบันทึกลงฉาก
            arr[ix * 2 + iy] = s; // เก็บลงช่อง
        }
        pieceCache[sprite] = arr; // ใส่ cache
        return arr; // คืนค่า
    }

    // ---- เรียกก่อนเริ่มเล่น ให้สร้าง texture/sprite ไว้ก่อน จะได้ไม่กระตุกตอนคว้าครั้งแรก ----
    public static void Prewarm(IconSpawner.ItemDef[] items)
    {
        EnsureSprites(); // สร้างวงกลม/วงแหวน
        if (items == null) return; // ไม่มีรายการไอคอนก็จบ
        foreach (var it in items) if (it != null && it.sprite) Pieces(it.sprite); // ตัดชิ้นของทุกไอคอนเก็บ cache ไว้
    }

    // ---- สร้าง sprite วงกลม/วงแหวนถ้ายังไม่มี ----
    static void EnsureSprites()
    {
        if (circleSprite == null) circleSprite = MakeCircle(64, 0f); // วงกลมทึบ 64x64
        if (ringSprite == null) ringSprite = MakeCircle(128, 0.82f); // วงแหวน 128x128 รูในกว้าง 82%
    }

    // ---- วาดวงกลมขอบนุ่มลง texture (inner = 0 คือวงกลมทึบ, inner > 0 คือวงแหวน) ----
    static Sprite MakeCircle(int n, float inner)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave }; // สร้าง texture n x n ไม่มี mipmap
        var px = new Color32[n * n]; // อาร์เรย์สีของทุกพิกเซล
        float r = n * 0.5f; // รัศมี
        float edge = 1.5f; // ความนุ่มของขอบ (พิกเซล)
        for (int y = 0; y < n; y++) // ไล่แถว
        for (int x = 0; x < n; x++) // ไล่คอลัมน์
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)); // ระยะจากจุดศูนย์กลาง
            float a = Mathf.Clamp01((r - d) / edge); // ความทึบ: ในวง = 1 นอกวง = 0 ขอบไล่ระดับ
            if (inner > 0) a *= Mathf.Clamp01((d - r * inner) / edge); // ถ้าเป็นวงแหวน เจาะรูตรงกลาง
            px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255)); // พิกเซลสีขาว ความทึบตามค่า a
        }
        tex.SetPixels32(px); // ใส่สีลง texture
        tex.Apply(); // อัปโหลด texture ไป GPU
        var s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f); // สร้าง sprite จาก texture
        s.hideFlags = HideFlags.DontSave; // ไม่ต้องบันทึกลงฉาก
        return s; // คืนค่า
    }
}
