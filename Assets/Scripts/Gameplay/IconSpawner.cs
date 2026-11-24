using System; // ใช้ [Serializable] และ [NonSerialized]
using System.Collections.Generic; // ใช้ List
using TMPro; // ใช้ TextMeshPro สำหรับข้อความแจ้งผล
using UnityEngine; // ใช้คลาสหลักของ Unity
using UnityEngine.UI; // ใช้ Image, Canvas, Outline

// ปล่อยไอคอนกิจกรรม (งาน / พักผ่อน / ฟื้นพลัง) จากด้านบนของพื้นที่เล่น ให้ตกลงมา
// เมื่อมือกำ (fist) ทับไอคอน ไอคอนจะแตกแบบลูกโป่ง แล้วใช้ผลของไอคอนนั้นกับค่าสถานะ
public class IconSpawner : MonoBehaviour // คลาสปล่อยและจัดการไอคอนที่ตกลงมา
{
    public enum Kind { Work, Relax, Recover, Songkran } // ประเภทไอคอน: งาน / พักผ่อน / ฟื้นพลัง / กิจกรรมสงกรานต์ (เฉพาะช่วง Event)

    // ---- ข้อมูลของไอคอน 1 ชนิด (ตั้งใน Inspector) ----
    [Serializable] // ให้แก้ค่าใน Inspector ได้
    public class ItemDef
    {
        public string label; // ชื่อกิจกรรม (แสดงในข้อความแจ้งผล)
        public Kind kind; // ประเภทไอคอน
        public Sprite sprite; // รูปไอคอน
        public float energy; // พลังงานที่เพิ่ม/ลด
        public float money; // เงินที่เพิ่ม/ลด
        public float happiness; // ความสุขที่เพิ่ม/ลด
        [Min(0.01f)] public float weight = 1f; // น้ำหนักการสุ่มในประเภทเดียวกัน ยิ่งมากยิ่งออกบ่อย
        [Tooltip("Songkran only: combo needed before this icon can appear (0 = always). Its chance grows with higher combo.")] // คำอธิบายใน Inspector
        [Min(0)] public int minCombo; // ช่วงสงกรานต์: ต้องมีคอมโบถึงเท่านี้ไอคอนนี้ถึงจะออก (0 = ออกตลอด) คอมโบยิ่งสูงยิ่งออกบ่อย
    }

    // ---- อ้างอิงออบเจกต์ในฉาก ----
    public ItemDef[] items; // รายการไอคอนทั้งหมด
    [Tooltip("Songkran activities. Spawned only while the Songkran event is on.")] // คำอธิบายใน Inspector
    public ItemDef[] eventItems; // ไอคอนกิจกรรมสงกรานต์ (ออกเฉพาะช่วง Event)
    public RectTransform area; // เลเยอร์เต็มจอที่ไอคอนตกลงมา
    public HandCursor hand; // เคอร์เซอร์มือ ใช้หาตำแหน่งตอนคว้า
    public TMP_FontAsset font; // ฟอนต์ไทยสำหรับข้อความ

    // ---- ค่าการปล่อยไอคอน: ขนาด ระยะขอบ ความถี่ ความเร็ว และโอกาสออกแต่ละประเภท ----
    // ค่า Vector2 แบบ (เริ่ม, ท้าย) จะค่อยๆ ไต่จากค่าแรกไปค่าหลังภายใน rampSeconds ทำให้เกมยากขึ้นเรื่อยๆ
    [Header("Spawn")] // หัวข้อใน Inspector
    public float iconSize = 240f; // ขนาดไอคอน (หน่วย canvas)
    public float sidePadding = 170f; // ระยะห่างจากขอบซ้ายขวาที่ไม่ปล่อยไอคอน
    public float topReserved = 220f; // เว้นด้านบนไว้ให้หลอดสถานะ ไอคอนเริ่มใต้ส่วนนี้
    public Vector2 spawnInterval = new Vector2(1.1f, 0.5f); // ช่วงเวลาระหว่างไอคอน: ต้นเกม 1.1 วิ -> ท้ายเกม 0.5 วิ
    public Vector2 fallSpeed = new Vector2(260f, 520f); // ความเร็วตก: ต้นเกม 260 -> ท้ายเกม 520 หน่วย/วิ
    public float rampSeconds = 180f; // เวลาที่ความยากไต่จนสุด (ควรเท่ากับ GameManager.monthLength)
    [Range(0, 1)] public float workChance = 0.41f; // โอกาสออกไอคอนงาน
    [Range(0, 1)] public float relaxChance = 0.30f; // โอกาสออกไอคอนพักผ่อน (ที่เหลือคือฟื้นพลัง)
    [Tooltip("Extra Recover chance when energy is low, so the player can still save the run.")] // คำอธิบายใน Inspector
    [Range(0, 1)] public float lowEnergyRecoverBonus = 0.12f; // พลังงานต่ำ เพิ่มโอกาสไอคอนฟื้นพลัง ให้ผู้เล่นยังพลิกเกมได้
    [Tooltip("Extra Work chance when money is low (taken from Relax), so the player can still earn.")] // คำอธิบายใน Inspector
    [Range(0, 1)] public float lowMoneyWorkBonus = 0.12f; // เงินต่ำ เพิ่มโอกาสไอคอนงาน (ดึงมาจากพักผ่อน)

    // ---- ค่าการคว้า ----
    [Header("Grab")] // หัวข้อใน Inspector
    [Tooltip("Seconds after the hand closes in which an icon touching the hand is still caught.")] // คำอธิบายใน Inspector
    public float grabWindow = 0.3f; // หลังกำมือ ยังคว้าไอคอนที่มาโดนได้อีกกี่วินาที (ช่วยให้คว้าง่ายขึ้น)

    [NonSerialized] public bool Paused = true; // หยุดปล่อยไอคอนอยู่ไหม (GameManager เป็นคนสั่ง)
    [Header("Songkran challenge")] // หัวข้อใน Inspector
    [Tooltip("Songkran icons fall this much faster.")] // คำอธิบายใน Inspector
    public float eventFallMultiplier = 1.4f; // ช่วง Event ไอคอนตกเร็วขึ้นกี่เท่า
    [Tooltip("Songkran spawn interval multiplier (smaller = more icons).")] // คำอธิบายใน Inspector
    public float eventSpawnMultiplier = 0.7f; // ช่วง Event ไอคอนออกถี่ขึ้น (คูณช่วงเวลา)
    [NonSerialized] public bool EventMode; // ช่วง Event สงกรานต์: ปล่อยเฉพาะไอคอนกิจกรรมพิเศษ (GameManager เป็นคนสั่ง)

    // ---- สีประจำประเภท (ใช้กับเอฟเฟกต์แตก) ----
    static readonly Color WorkColor = new Color(0.85f, 0.6f, 0f); // สีส้มทอง = งาน
    static readonly Color RelaxColor = new Color(0.91f, 0.27f, 0.48f); // สีชมพู = พักผ่อน
    static readonly Color RecoverColor = new Color(0.18f, 0.56f, 0.84f); // สีฟ้า = ฟื้นพลัง
    static readonly Color SongkranColor = new Color(0.2f, 0.75f, 0.95f); // สีฟ้าน้ำ = สงกรานต์

    // ---- ข้อมูลของไอคอนที่กำลังตก ----
    class Falling
    {
        public ItemDef def; // ข้อมูลชนิดไอคอน
        public RectTransform rt; // RectTransform ของไอคอน
        public float speed; // ความเร็วตกของไอคอนนี้
        public float swayPhase; // เฟสการแกว่ง (สุ่ม ให้แต่ละอันแกว่งไม่พร้อมกัน)
        public float x; // ตำแหน่ง x กลางของการแกว่ง
    }

    // ---- ตัวแปรภายใน ----
    readonly List<Falling> live = new List<Falling>(); // ไอคอนที่กำลังตกอยู่ทั้งหมด
    float spawnTimer; // นับถอยหลังถึงไอคอนถัดไป
    float elapsed; // เวลาที่เล่นไปแล้ว (ใช้คำนวณความยาก)
    float grabTimer; // เวลาที่ยังคว้าได้หลังกำมือ

    // ---- เตรียมของก่อนเริ่มเล่น ลดอาการกระตุก ----
    void Start()
    {
        // ไอคอนที่ตกขยับทุกเฟรม ใส่ Canvas ซ้อนไว้แยก จะได้ไม่ต้อง rebuild
        // หลอดสถานะกับข้อความทั้งหมดทุกเฟรม (UI ไม่ต้อง re-batch ทั้งจอ)
        if (area && !area.GetComponent<Canvas>()) // ถ้ายังไม่มี Canvas
        {
            area.gameObject.AddComponent<Canvas>(); // เพิ่ม Canvas ซ้อน
        }

        // ฟอนต์ไทยเป็นแบบ dynamic: ตัวอักษรใหม่จะถูกวาดลง atlas ตอนโผล่ครั้งแรก ทำให้กระตุก 1 เฟรม
        // เลยใส่ตัวอักษรทุกตัวที่เกมใช้ไว้ก่อนเริ่มเล่น
        if (font) // มีฟอนต์
        {
            var sb = new System.Text.StringBuilder( // รวมตัวอักษรที่ต้องใช้
                "0123456789+-:/ เงินความสุขพลังงานวันที่หมดไฟ!ทำงานได้เงินน้อยลงพลังงานใกล้หมดกินข้าวหรือนอนพักเงินใกล้หมดไปทำงานกันเถอะ" + GameManager.EventCharacters); // ข้อความคงที่ทั้งหมดในเกม
            if (items != null) foreach (var it in items) if (it != null) sb.Append(it.label); // เพิ่มชื่อกิจกรรมทุกอัน
            if (eventItems != null) foreach (var it in eventItems) if (it != null) sb.Append(it.label); // เพิ่มชื่อกิจกรรมสงกรานต์
            font.TryAddCharacters(sb.ToString()); // วาดตัวอักษรลง atlas ล่วงหน้า
        }

        // สร้าง sprite ของเอฟเฟกต์แตกไว้ก่อน ไม่ต้องรอคว้าครั้งแรก
        BalloonPop.Prewarm(items); // prewarm เอฟเฟกต์
        BalloonPop.Prewarm(eventItems); // prewarm เอฟเฟกต์ของไอคอนสงกรานต์
    }

    // ---- ทุกเฟรม: ปล่อยไอคอน ขยับไอคอน และเช็คการคว้า ----
    void Update()
    {
        if (Paused) return; // หยุดอยู่ ไม่ต้องทำอะไร
        float dt = Time.deltaTime; // เวลาระหว่างเฟรม
        elapsed += dt; // นับเวลาที่เล่น
        float ramp = Mathf.Clamp01(elapsed / rampSeconds); // ความยาก 0 (ต้นเกม) ถึง 1 (ท้ายเกม)

        // ---- ปล่อยไอคอนใหม่ตามเวลา ----
        spawnTimer -= dt; // นับถอยหลัง
        if (spawnTimer <= 0) // ถึงเวลาปล่อย
        {
            spawnTimer = Mathf.Lerp(spawnInterval.x, spawnInterval.y, ramp) * UnityEngine.Random.Range(0.8f, 1.2f) * (EventMode ? eventSpawnMultiplier : 1f); // ตั้งเวลาครั้งถัดไปตามความยาก + สุ่ม ±20%
            Spawn(Mathf.Lerp(fallSpeed.x, fallSpeed.y, ramp) * (EventMode ? eventFallMultiplier : 1f)); // ปล่อยไอคอนด้วยความเร็วตามความยาก
        }

        // ---- ขยับไอคอนที่กำลังตก (วนจากท้ายเพราะมีการลบออกจาก list) ----
        for (int i = live.Count - 1; i >= 0; i--) // ไล่จากท้ายไปหน้า
        {
            var f = live[i]; // ไอคอนตัวนี้
            // ไอคอนถูกลบไปแล้ว หรือกำลังแตกอยู่ (เช่นจากสคริปต์อื่น): เลิกติดตาม
            if (f.rt == null || f.rt.GetComponent<BalloonPop>() != null) // เช็คสถานะไอคอน
            {
                live.RemoveAt(i); // เอาออกจาก list
                continue; // ไปตัวถัดไป
            }
            var p = f.rt.anchoredPosition; // ตำแหน่งปัจจุบัน
            p.y -= f.speed * dt; // ตกลงตามความเร็ว
            p.x = f.x + Mathf.Sin(Time.time * 2.4f + f.swayPhase) * 45f; // แกว่งซ้ายขวา ±45 หน่วย
            f.rt.anchoredPosition = p; // ใส่ตำแหน่งใหม่
            f.rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 2.5f + f.swayPhase) * 10f); // เอียงไปมา ±10 องศา
            if (p.y < -iconSize) // ตกพ้นขอบล่างแล้ว
            {
                Destroy(f.rt.gameObject); // ลบไอคอน
                live.RemoveAt(i); // เอาออกจาก list
            }
        }

        // ---- เช็คการคว้า ----
        var tracker = HandTrackingClient.Instance; // ตัวรับข้อมูลมือ
        if (tracker == null || hand == null) return; // ไม่มีข้อมูลมือก็จบ
        if (tracker.FistDown) grabTimer = grabWindow; // เพิ่งกำมือ เปิดช่วงเวลาคว้า
        if (grabTimer > 0) // ยังอยู่ในช่วงคว้า
        {
            grabTimer -= dt; // นับถอยหลัง
            if (TryGrab()) grabTimer = 0; // คว้าโดนแล้วปิดช่วงคว้า (1 กำ = 1 ไอคอน)
        }
    }

    // ---- ลบไอคอนที่กำลังตกทั้งหมด (ใช้ตอนเปลี่ยนเข้า/ออก Event) ----
    public void ClearIcons()
    {
        foreach (var f in live) if (f.rt) Destroy(f.rt.gameObject); // ลบทุกอัน
        live.Clear(); // ล้าง list
        grabTimer = 0; // ยกเลิกการคว้าที่ค้าง
    }

    // ---- หาไอคอนที่ใกล้มือที่สุดในระยะคว้า แล้วใช้ผล ----
    bool TryGrab()
    {
        Vector2 hp = hand.CanvasPosition; // ตำแหน่งมือ
        float reach = hand.grabRadius + iconSize * 0.5f; // ระยะคว้า = รัศมีมือ + ครึ่งขนาดไอคอน
        int best = -1; // index ไอคอนที่ใกล้ที่สุด (-1 = ไม่เจอ)
        float bestD = reach * reach; // ระยะใกล้สุด (เทียบแบบยกกำลังสอง ไม่ต้อง sqrt)
        for (int i = 0; i < live.Count; i++) // ไล่ทุกไอคอน
        {
            float d = (live[i].rt.anchoredPosition - hp).sqrMagnitude; // ระยะยกกำลังสองจากมือ
            if (d < bestD) { bestD = d; best = i; } // ใกล้กว่าก็จำไว้
        }
        if (best < 0) return false; // ไม่มีไอคอนในระยะ

        var f = live[best]; // ไอคอนที่คว้าได้
        live.RemoveAt(best); // เอาออกจาก list ไอคอนที่ตก
        Vector3 applied = GameManager.Instance != null ? GameManager.Instance.ApplyItem(f.def) : Vector3.zero; // ส่งผลไปที่ GameManager
        Popup(f.def, applied, f.rt.anchoredPosition); // แสดงข้อความแจ้งผล
        var pop = f.rt.gameObject.AddComponent<BalloonPop>(); // ใส่เอฟเฟกต์แตก
        pop.color = KindColor(f.def.kind); // สีเอฟเฟกต์ตามประเภท
        if (GameManager.Instance != null) { if (f.def.kind == Kind.Songkran && f.def.money > 0) GameManager.Instance.PlayCoin(); else GameManager.Instance.PlayGrab(f.def.kind); } // เสียงตอนคว้า ต่างกันตามประเภท (ไอคอนได้เงินช่วงสงกรานต์ = เสียงเหรียญ)
        return true; // คว้าสำเร็จ
    }

    static Color KindColor(Kind k) => k == Kind.Work ? WorkColor : k == Kind.Relax ? RelaxColor : k == Kind.Songkran ? SongkranColor : RecoverColor; // คืนสีตามประเภทไอคอน

    // ---- สร้างไอคอนใหม่ 1 อันที่ด้านบน ----
    // ---- รางวัลคอมโบ: ปล่อยไอคอนเงิน (ได้โบนัส / ญาติให้ตังค์) n อัน (GameManager เรียกทุกๆ 10 คอมโบ) ----
    public const string BonusLabel = "ได้โบนัส"; // ชื่อไอคอนโบนัส (ต้องตรงกับ label ในรายการ eventItems)
    public const string RelativesLabel = "ญาติให้ตังค์"; // ชื่อไอคอนญาติให้ตังค์

    // ---- ปล่อยไอคอนเงินตามชื่อ n อัน (GameManager เรียกทุกๆ 10 คอมโบ) ----
    public void DropMoneyIcon(string label, int n)
    {
        if (eventItems == null) return; // ไม่มีรายการ
        ItemDef pick = null; // ไอคอนที่จะปล่อย
        foreach (var it in eventItems) if (it != null && it.label == label && it.sprite) { pick = it; break; } // หาไอคอนตามชื่อ
        if (pick == null) return; // ไม่เจอ
        float ramp = Mathf.Clamp01(elapsed / rampSeconds); // ความยากตอนนี้
        float speed = Mathf.Lerp(fallSpeed.x, fallSpeed.y, ramp) * (EventMode ? eventFallMultiplier : 1f); // ความเร็วตกเท่าไอคอนอื่น
        for (int i = 0; i < n; i++) Spawn(speed, pick); // ปล่อยทีละอัน (ตำแหน่งสุ่มไม่ซ้ำกัน)
    }

    void Spawn(float speed, ItemDef forced = null)
    {
        ItemDef def = forced ?? Pick(); // สุ่มชนิดไอคอน (หรือใช้ไอคอนที่กำหนด)
        if (def == null || def.sprite == null) return; // สุ่มไม่ได้หรือไม่มีรูป ข้าม

        var go = new GameObject("Icon_" + def.label, typeof(RectTransform), typeof(Image)); // สร้างออบเจกต์ไอคอน
        var rt = (RectTransform)go.transform; // ดึง RectTransform
        rt.SetParent(area, false); // ใส่ไว้ในพื้นที่เล่น
        rt.anchorMin = rt.anchorMax = Vector2.zero; // anchor มุมล่างซ้าย
        rt.sizeDelta = Vector2.one * iconSize; // ขนาดไอคอน
        var img = go.GetComponent<Image>(); // ดึง Image
        img.sprite = def.sprite; // ตั้งรูป
        img.preserveAspect = true; // รักษาสัดส่วนรูป
        img.raycastTarget = false; // ไม่รับคลิก

        float w = area.rect.width; // ความกว้างพื้นที่เล่น
        float x = UnityEngine.Random.Range(sidePadding, w - sidePadding); // สุ่มตำแหน่ง x โดยเว้นขอบ
        rt.anchoredPosition = new Vector2(x, area.rect.height - topReserved + iconSize); // เริ่มเหนือพื้นที่เล่นเล็กน้อย
        live.Add(new Falling { def = def, rt = rt, speed = speed * UnityEngine.Random.Range(0.85f, 1.15f), swayPhase = UnityEngine.Random.value * 6.28f, x = x }); // เพิ่มเข้า list พร้อมสุ่มความเร็ว ±15% และเฟสแกว่ง
        // ให้เคอร์เซอร์มืออยู่บนสุดเสมอ
        if (hand) hand.transform.SetAsLastSibling(); // ย้ายมือไปท้ายลำดับ (วาดทับไอคอน)
    }

    // ---- สุ่มชนิดไอคอน: สุ่มประเภทก่อน แล้วสุ่มไอคอนในประเภทนั้นตาม weight ----
    ItemDef Pick()
    {
        if (EventMode && eventItems != null && eventItems.Length > 0) return PickWeighted(eventItems, null, EventWeight); // ช่วง Event: สุ่มเฉพาะไอคอนสงกรานต์ (ไอคอนเงินออกตามคอมโบ)
        float recoverBonus = 0, workBonus = 0; // โบนัสโอกาส
        var gm = GameManager.Instance; // ดึง GameManager
        if (gm != null && gm.EnergyFraction < 0.35f) recoverBonus = lowEnergyRecoverBonus; // พลังงานต่ำกว่า 35% เพิ่มโอกาสฟื้นพลัง
        if (gm != null && gm.Money < gm.maxMoney * 0.3f) workBonus = lowMoneyWorkBonus; // เงินต่ำกว่า 30% เพิ่มโอกาสงาน

        float work = workChance - recoverBonus * 0.6f + workBonus; // โอกาสงานหลังปรับโบนัส
        float relax = Mathf.Max(0.05f, relaxChance - recoverBonus * 0.4f - workBonus); // โอกาสพักผ่อนหลังปรับ (ขั้นต่ำ 5%)
        float r0 = UnityEngine.Random.value; // สุ่ม 0-1
        Kind kind = r0 < work ? Kind.Work : r0 < work + relax ? Kind.Relax : Kind.Recover; // เลือกประเภทตามช่วงที่สุ่มได้

        return PickWeighted(items, kind); // สุ่มไอคอนในประเภทนั้นตาม weight
    }

    // ---- สุ่มไอคอนจากรายการตาม weight (ถ้าระบุ kind จะสุ่มเฉพาะประเภทนั้น) ----
    // ---- น้ำหนักการสุ่มช่วงสงกรานต์: ไอคอนที่มี minCombo จะออกเมื่อคอมโบถึง และออกบ่อยขึ้นเมื่อคอมโบสูงขึ้น ----
    [Tooltip("Extra chance per combo above minCombo (0.5 = +50% each).")] // คำอธิบายใน Inspector
    public float comboWeightStep = 0.5f; // คอมโบเกินขั้นต่ำ 1 ครั้ง โอกาสเพิ่มเท่าไหร่
    public float comboWeightMax = 3f; // โอกาสเพิ่มได้สูงสุดกี่เท่า
    float EventWeight(ItemDef it)
    {
        if (it.money > 0) return 0f; // ไอคอนเงินไม่สุ่มออกเอง: ทุกๆ 10 คอมโบ ตกมาเป็นรางวัล (DropMoneyIcon)
        if (it.minCombo <= 0) return it.weight; // ไอคอนปกติ ออกตามน้ำหนักเดิม
        int combo = GameManager.Instance != null ? GameManager.Instance.Combo : 0; // คอมโบตอนนี้
        if (combo < it.minCombo) return 0f; // คอมโบยังไม่ถึง ไม่ออก
        return it.weight * Mathf.Min(comboWeightMax, 1f + (combo - it.minCombo) * comboWeightStep); // คอมโบยิ่งสูงยิ่งออกบ่อย
    }

    static ItemDef PickWeighted(ItemDef[] list, Kind? kind = null, Func<ItemDef, float> weightOf = null)
    {
        if (weightOf == null) weightOf = it => it.weight; // ไม่ระบุใช้ weight ปกติ
        float total = 0; // ผลรวม weight ของประเภทนี้
        foreach (var it in list) if (it != null && (kind == null || it.kind == kind)) total += weightOf(it); // รวม weight
        if (total <= 0) return null; // ไม่มีไอคอนประเภทนี้
        float r = UnityEngine.Random.value * total; // สุ่มค่าในช่วงผลรวม weight
        foreach (var it in list) // ไล่หาไอคอนที่ตรงกับค่าที่สุ่ม
        {
            if (it == null || (kind != null && it.kind != kind)) continue; // ข้ามประเภทอื่น
            float w = weightOf(it); // น้ำหนักของไอคอนนี้
            if (w <= 0) continue; // ออกไม่ได้ ข้าม
            r -= w; // ลบ weight ออก
            if (r <= 0) return it; // ถึงตัวที่สุ่มได้แล้ว
        }
        return null; // กันพลาด (ปกติไม่ถึงตรงนี้)
    }

    Material popupMaterial; // material ของ popup (ถ้ามีสร้างไว้)
    GameObject currentToast; // ข้อความแจ้งผลที่แสดงอยู่ตอนนี้

    // ---- ล้าง material ตอนออบเจกต์ถูกลบ ----
    void OnDestroy()
    {
        if (popupMaterial) Destroy(popupMaterial); // ลบ material กัน memory leak
    }

    // ---- ข้อความแจ้งผลมุมล่างซ้าย เช่น "Work: +10 เงิน -5 ความสุข" ----
    // ปรับขนาดกล่อง สีขอบ ขนาดตัวอักษร ตำแหน่ง ได้ในฟังก์ชันนี้
    void Popup(ItemDef def, Vector3 applied, Vector2 pos)
    {
        // กล่องพื้นขาว ตัวอักษรดำ ให้อ่านง่ายทุกแบบ มีแค่ขอบหนาที่เป็นสีตามประเภท
        // งาน = แดง, พักผ่อน = เหลือง, ฟื้นพลัง = เขียว
        Color boxColor = Color.white, textColor = Color.black, borderColor; // สีกล่อง สีตัวอักษร สีขอบ
        switch (def.kind) // เลือกสีขอบตามประเภท
        {
            case Kind.Work:  borderColor = new Color32(0xE5, 0x1C, 0x23, 0xFF); break; // แดง
            case Kind.Relax: borderColor = new Color32(0xF5, 0xB4, 0x00, 0xFF); break; // เหลือง
            case Kind.Songkran: borderColor = new Color32(0x1E, 0xA7, 0xE1, 0xFF); break; // ฟ้า = สงกรานต์
            default:         borderColor = new Color32(0x22, 0xA6, 0x3A, 0xFF); break; // เขียว
        }

        var parts = new List<string>(); // ข้อความย่อยของแต่ละค่า
        void Add(float v, string name) // ฟังก์ชันย่อย เพิ่มข้อความถ้าค่าเปลี่ยนจริง
        {
            if (Mathf.Abs(v) < 0.5f) return; // ค่าน้อยมาก (ปัดแล้วเป็น 0) ไม่ต้องแสดง
            parts.Add($"{(v > 0 ? "+" : "")}{v:0} {name}"); // เช่น "+10 เงิน" หรือ "-5 ความสุข"
        }
        Add(applied.y, "เงิน"); // เงิน
        Add(applied.z, "ความสุข"); // ความสุข
        Add(applied.x, "พลังงาน"); // พลังงาน

        // แสดงทีละอันที่มุมล่างซ้าย อันใหม่มาแทนอันเก่า
        if (currentToast) Destroy(currentToast); // ลบอันเก่า
        var canvas = area.GetComponentInParent<Canvas>(); // หา Canvas ที่ครอบอยู่
        var parent = canvas ? (RectTransform)canvas.rootCanvas.transform : area; // ใส่ไว้ที่ root canvas (ไม่ใช่ canvas ซ้อนของไอคอน)
        var go = new GameObject("Toast", typeof(RectTransform), typeof(CanvasGroup), typeof(UnityEngine.UI.Image)); // สร้างกล่องข้อความ
        currentToast = go; // จำอันปัจจุบัน
        var rt = (RectTransform)go.transform; // ดึง RectTransform
        rt.SetParent(parent, false); // ใส่ไว้ใต้ canvas
        rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero; // ยึดมุมล่างซ้าย
        var bg = go.GetComponent<UnityEngine.UI.Image>(); // พื้นหลังกล่อง
        bg.color = boxColor; // สีพื้น
        bg.raycastTarget = false; // ไม่รับคลิก
        // ขอบหนาทึบ (ซ้อน Outline 2 ชั้น = ขอบ 8 ทิศรอบกล่อง)
        foreach (var d in new[] { new Vector2(8, 8), new Vector2(8, 0) }) // 2 ชุดระยะขอบ
        {
            var o = go.AddComponent<UnityEngine.UI.Outline>(); // เพิ่ม Outline
            o.effectColor = borderColor; // สีขอบ
            o.effectDistance = d; // ระยะขอบ
        }

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)); // สร้างข้อความ
        var trt = (RectTransform)textGo.transform; // ดึง RectTransform
        trt.SetParent(rt, false); // ใส่ไว้ในกล่อง
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; // ยืดเต็มกล่อง
        trt.offsetMin = new Vector2(20, 8); trt.offsetMax = new Vector2(-20, -8); // เว้นขอบใน
        var t = textGo.GetComponent<TextMeshProUGUI>(); // ดึงคอมโพเนนต์ข้อความ
        if (font) t.font = font; // ใช้ฟอนต์ไทย
        t.fontSize = 30; // ขนาดตัวอักษร
        t.fontStyle = FontStyles.Bold; // ตัวหนา
        t.color = textColor; // สีตัวอักษร
        t.alignment = TextAlignmentOptions.MidlineLeft; // ชิดซ้าย กลางแนวตั้ง
        t.textWrappingMode = TextWrappingModes.NoWrap; // ไม่ตัดบรรทัด
        t.raycastTarget = false; // ไม่รับคลิก
        var gmc = GameManager.Instance; // แสดงคอมโบในข้อความแจ้งผล
        string comboTxt = def.kind == Kind.Songkran && gmc != null && gmc.Combo > 1 ? $"   คอมโบ x{gmc.Combo}!" : "";
        t.text = $"{def.label}:  " + string.Join("   ", parts) + comboTxt; // ข้อความ: ชื่อกิจกรรม + ผลที่ได้

        // กล่องขนาดคงที่ทุกกิจกรรม ถ้าข้อความยาวตัวอักษรจะย่อลงเอง
        rt.sizeDelta = new Vector2(680, 64); // ขนาดกล่อง
        t.enableAutoSizing = true; // เปิดย่อขนาดอัตโนมัติ
        t.fontSizeMin = 20; // ขนาดเล็กสุด
        t.fontSizeMax = 30; // ขนาดใหญ่สุด
        t.overflowMode = TextOverflowModes.Overflow; // ล้นได้ ไม่ตัดทิ้ง
        rt.anchoredPosition = new Vector2(24, 20); // ห่างจากมุมล่างซ้าย
        go.AddComponent<FloatUpFade>(); // ใส่แอนิเมชันเฟดเข้า/ออก
    }

    // ---- แอนิเมชันข้อความแจ้งผล: เฟดเข้าเร็ว ค้างไว้ แล้วเฟดออก (รวม 3 วินาที) ----
    class FloatUpFade : MonoBehaviour
    {
        float t; // เวลาที่ผ่านไป
        CanvasGroup group; // ใช้ปรับความโปร่งใสทั้งกล่อง
        void Awake() => group = GetComponent<CanvasGroup>(); // ดึง CanvasGroup
        void Update()
        {
            t += Time.unscaledDeltaTime; // นับเวลา
            // เฟดเข้าใน ~0.17 วิ ค้างไว้ แล้วเฟดออกช่วง 0.5 วิสุดท้าย อยู่กับที่มุมจอ
            group.alpha = Mathf.Min(Mathf.Clamp01(t * 6f), Mathf.Clamp01((3f - t) / 0.5f)); // คำนวณความทึบ
            if (t > 3f) Destroy(gameObject); // ครบ 3 วิ ลบทิ้ง
        }
    }
}
