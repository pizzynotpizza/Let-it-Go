using TMPro; // ใช้ TextMeshPro สำหรับข้อความไทย
using UnityEngine; // ใช้คลาสหลักของ Unity
using UnityEngine.InputSystem; // ใช้ Input System อ่านปุ่ม Space
using UnityEngine.SceneManagement; // ใช้โหลดฉากถัดไป
using UnityEngine.UI; // ใช้ Image

// หน้าเช็คมือสั้นๆ ที่ขึ้นหลังกดปุ่ม Start ในหน้า Title
//   ขั้น 1: ขยับมือไปมา (เติมหลอด และเก็บค่าตอนแบมือ)
//   ขั้น 2: กำมือเพื่อไปต่อ (เก็บค่าตอนกำมือ แล้วโหลดฉาก Tutorial)
// ทำรอบเดียว ไม่มีหน้าเพิ่ม กด Space เพื่อข้าม (จะใช้ค่าที่เคยบันทึกไว้)
// UI ทั้งหมดสร้างตอนรันบน canvas ที่คอมโพเนนต์นี้อยู่
public class TitleHandCheck : MonoBehaviour // คลาสหน้าเช็คมือ/calibrate
{
    // ---- ค่าที่ต้องลากใส่ใน Inspector ----
    public string nextScene = "Tutorial"; // ฉากที่จะไปหลังเช็คมือเสร็จ
    public TMP_FontAsset font; // ฟอนต์ภาษาไทยที่ใช้ในข้อความ
    public Sprite openSprite; // รูปมือแบ
    public Sprite fistSprite; // รูปมือกำ

    // ---- ค่าที่ปรับความยากของการเช็คมือ ----
    [Tooltip("How far the hand must travel, in screen widths, to finish step 1.")] // คำอธิบายใน Inspector
    public float moveDistance = 2.5f; // ต้องขยับมือรวมกี่เท่าของความกว้างจอถึงจะผ่านขั้น 1
    [Tooltip("Seconds the fist must be held to continue.")] // คำอธิบายใน Inspector
    public float fistHold = 0.3f; // ต้องกำมือค้างกี่วินาทีถึงจะผ่านขั้น 2

    public bool IsRunning { get; private set; } // หน้าเช็คมือกำลังทำงานอยู่ไหม (ให้สคริปต์อื่นอ่าน)

    // ---- ตัวแปรภายใน ----
    enum Step { Move, Fist, Done } // ขั้นตอน: ขยับมือ / กำมือ / เสร็จ
    Step step; // ขั้นตอนปัจจุบัน
    RectTransform root, cursor, fill; // ออบเจกต์หลักของหน้า, เคอร์เซอร์ และหลอด progress
    Image cursorImg; // Image ของเคอร์เซอร์
    TMP_Text title, body; // ข้อความหัวข้อ และข้อความอธิบาย
    Vector2 lastPos; // ตำแหน่งมือเฟรมก่อน ใช้คำนวณระยะที่ขยับ
    float moved, fistTimer, openSum, fistSum; // ระยะขยับรวม, เวลากำมือ, ผลรวมค่าความแบ/กำ
    int openCount, fistCount; // จำนวนตัวอย่างค่าแบมือ/กำมือที่เก็บได้

    static readonly Color Ink = new Color(0.29f, 0.17f, 0.42f); // สีตัวอักษรหลัก (ม่วงเข้ม)

    // ---- เริ่มหน้าเช็คมือ (StartButton เรียก) ----
    public void Begin()
    {
        if (IsRunning) return; // ถ้าเปิดอยู่แล้วไม่ต้องเริ่มซ้ำ
        HandTrackingClient.Ensure(); // ให้แน่ใจว่าตัวรับข้อมูลมือทำงาน
        if (root == null) Build(); // สร้าง UI ครั้งแรก
        root.gameObject.SetActive(true); // แสดงหน้าเช็คมือ
        root.SetAsLastSibling(); // ย้ายไปบนสุดของ canvas
        IsRunning = true; // ตั้งสถานะว่ากำลังทำงาน
        HandTrackingClient.ResetHandLock(); // ล็อกมือใหม่: มือที่โชว์ในหน้านี้จะเป็น Hand 1
        SetStep(Step.Move); // เริ่มขั้นที่ 1
    }

    // ---- เปลี่ยนขั้นตอน และเปลี่ยนข้อความบนจอ ----
    void SetStep(Step s)
    {
        step = s; // จำขั้นปัจจุบัน
        moved = 0; fistTimer = 0; // รีเซ็ตตัวนับ
        var hand = HandTrackingClient.Instance; // ดึงข้อมูลมือ
        if (hand) lastPos = hand.ScreenPos01; // ตั้งตำแหน่งเริ่มต้นสำหรับวัดระยะ
        if (s == Step.Move) // ขั้นขยับมือ
        {
            title.text = "เตรียมมือให้พร้อม"; // หัวข้อ
            body.text = "ยกมือให้กล้องเห็น แล้ว<color=#2E8FD6><b>ขยับมือไปมา</b></color>"; // คำอธิบาย (ใช้ rich text ทำสี/ตัวหนา)
        }
        else if (s == Step.Fist) // ขั้นกำมือ
        {
            title.text = "ดีมาก!"; // หัวข้อ
            body.text = "<color=#E8457A><b>กำมือ</b></color>เพื่อไปต่อ"; // คำอธิบาย
        }
        SetFill(0); // รีเซ็ตหลอด progress
    }

    // ---- ทุกเฟรม: อัปเดตเคอร์เซอร์ และนับ progress ของแต่ละขั้น ----
    void Update()
    {
        if (!IsRunning) return; // ยังไม่เปิดหน้าเช็คมือ ไม่ต้องทำอะไร
        var hand = HandTrackingClient.Instance; // ดึงข้อมูลมือ
        var kb = Keyboard.current; // ดึงคีย์บอร์ด
        if (kb != null && kb.spaceKey.wasPressedThisFrame) { Finish(false); return; } // กด Space = ข้าม ไม่บันทึกค่าใหม่
        if (hand == null) return; // ยังไม่มีข้อมูลมือ

        // ให้เคอร์เซอร์ตามมือ ผู้เล่นจะเห็นว่าระบบจับมือได้จริง
        cursor.anchoredPosition = Vector2.Scale(hand.ScreenPos01, root.rect.size); // แปลงตำแหน่งมือ 0-1 เป็นหน่วย canvas
        cursorImg.sprite = hand.IsFist ? fistSprite : openSprite; // เปลี่ยนรูปตามท่ามือ
        cursorImg.color = hand.Detected ? Color.white : new Color(1, 1, 1, 0.35f); // หามือไม่เจอให้จางลง

        if (hand.MouseMode && step == Step.Move) // ผู้เล่นเลือกโหมดเมาส์เอง
            body.text = "ใช้<b>เมาส์</b>เล่น: ขยับเมาส์ไปมา (คลิกค้าง = กำมือ)\n<size=75%><color=#2E8FD6>อยากเล่นด้วยมือ? เปิดไฟล์ <b>Start Hand Tracker.bat</b> ก่อน</color></size>"; // บอกวิธีเล่นด้วยเมาส์ + วิธีเปิดตัวจับมือ
        else if (hand.UsingMouse && step == Step.Move) // หากล้องไม่เจอ เลยใช้เมาส์แทนอัตโนมัติ
            body.text = "ไม่พบตัวจับมือ ใช้<b>เมาส์</b>แทน (คลิกค้าง = กำมือ)\n<size=75%><color=#2E8FD6>อยากเล่นด้วยมือ? เปิดไฟล์ <b>Start Hand Tracker.bat</b> ก่อน</color></size>"; // แจ้งว่าไม่พบตัวจับมือ + วิธีเปิด

        float dt = Time.unscaledDeltaTime; // เวลาระหว่างเฟรม
        if (step == Step.Move) // ---- ขั้น 1: ขยับมือ ----
        {
            if (hand.Detected && !hand.IsFist) // นับเฉพาะตอนเห็นมือและแบมืออยู่
            {
                moved += (hand.ScreenPos01 - lastPos).magnitude; // บวกระยะที่ขยับในเฟรมนี้
                openSum += hand.RawOpenness; openCount++; // เก็บค่าความแบมือไว้หาค่าเฉลี่ย
            }
            lastPos = hand.ScreenPos01; // อัปเดตตำแหน่งล่าสุด
            SetFill(moved / moveDistance); // อัปเดตหลอดตามระยะที่ขยับ
            if (moved >= moveDistance) SetStep(Step.Fist); // ขยับครบแล้วไปขั้น 2
        }
        else if (step == Step.Fist) // ---- ขั้น 2: กำมือค้าง ----
        {
            if (hand.Detected && hand.IsFist) // กำมืออยู่
            {
                fistTimer += dt; // นับเวลากำค้าง
                fistSum += hand.RawOpenness; fistCount++; // เก็บค่าตอนกำมือไว้หาค่าเฉลี่ย
            }
            else fistTimer = Mathf.Max(0, fistTimer - dt); // ปล่อยมือ ให้หลอดค่อยๆ ลดลง
            SetFill(fistTimer / fistHold); // อัปเดตหลอดตามเวลากำ
            if (fistTimer >= fistHold) Finish(!hand.UsingMouse); // กำครบเวลาแล้วจบ (บันทึกค่าเฉพาะตอนใช้กล้องจริง)
        }
    }

    // ---- จบการเช็คมือ: บันทึกค่า calibrate แล้วไปฉากถัดไป ----
    void Finish(bool save)
    {
        step = Step.Done; // ตั้งเป็นขั้นเสร็จ
        IsRunning = false; // ปิดสถานะทำงาน
        var hand = HandTrackingClient.Instance; // ดึงข้อมูลมือ
        if (save && hand && openCount > 0 && fistCount > 0) // มีข้อมูลทั้งแบและกำครบ
            hand.SaveCalibration(openSum / openCount, fistSum / fistCount); // บันทึกค่าเฉลี่ยแบมือ/กำมือ ใช้แยกท่ามือของผู้เล่นคนนี้
        SceneManager.LoadScene(nextScene); // โหลดฉากถัดไป
    }

    // ---- ตั้งความยาวหลอด progress (0-1) ----
    void SetFill(float t)
    {
        t = Mathf.Clamp01(t); // บีบค่าให้อยู่ใน 0-1
        fill.anchorMax = new Vector2(t, 1); // ปรับความกว้างหลอดด้วย anchorMax.x
    }

    // ---------------------------------------------------------------- UI
    // ---- สร้าง UI ของหน้าเช็คมือด้วยโค้ด (พื้นหลังมืด, กรอบ, ข้อความ, หลอด, เคอร์เซอร์) ----
    // ปรับขนาด/ตำแหน่ง/สีของแต่ละชิ้นได้ในฟังก์ชันนี้
    void Build()
    {
        root = NewRect("HandCheck", (RectTransform)transform); // สร้างออบเจกต์หลักใต้ canvas
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; // ยืดเต็มจอ
        root.offsetMin = root.offsetMax = Vector2.zero; // ไม่มีขอบเว้น
        root.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.05f, 0.2f, 0.55f); // พื้นหลังม่วงเข้มโปร่งใส บังหน้า Title

        var frame = NewRect("Frame", root); // กรอบสีขาวตรงกลาง
        frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f); // ยึดกลางจอ
        frame.sizeDelta = new Vector2(1000, 420); // ขนาดกรอบ
        frame.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.95f); // สีขาวเกือบทึบ

        title = NewText(frame, "Title", 60, new Vector2(0, 120), new Vector2(900, 80)); // ข้อความหัวข้อ ขนาด 60
        body = NewText(frame, "Body", 44, new Vector2(0, 10), new Vector2(900, 130)); // ข้อความอธิบาย ขนาด 44
        body.enableAutoSizing = true; body.fontSizeMin = 24; body.fontSizeMax = 44; // ข้อความยาวให้ย่อลงเองให้พอดีกล่อง ไม่ล้นทับหัวข้อ
        body.textWrappingMode = TextWrappingModes.NoWrap; // ไม่ตัดบรรทัดกลางคำ ขึ้นบรรทัดเฉพาะที่ \n
        var skip = NewText(frame, "Skip", 26, new Vector2(0, -170), new Vector2(900, 40)); // ข้อความบอกวิธีข้าม ขนาด 26
        skip.text = "กด Space เพื่อข้าม"; // ข้อความข้าม
        skip.color = new Color(Ink.r, Ink.g, Ink.b, 0.6f); // สีจางกว่าข้อความหลัก

        var bar = NewRect("Progress", frame); // พื้นหลังหลอด progress
        bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0.5f); // ยึดกลางกรอบ
        bar.anchoredPosition = new Vector2(0, -100); // อยู่ใต้ข้อความ
        bar.sizeDelta = new Vector2(700, 36); // ขนาดหลอด
        bar.gameObject.AddComponent<Image>().color = new Color(Ink.r, Ink.g, Ink.b, 0.2f); // สีพื้นหลอดจางๆ
        fill = NewRect("Fill", bar); // ตัวหลอดที่เติม
        fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0, 1); // เริ่มที่ความกว้าง 0
        fill.offsetMin = fill.offsetMax = Vector2.zero; // ไม่มีขอบเว้น
        fill.gameObject.AddComponent<Image>().color = new Color(0.42f, 0.8f, 0.25f); // สีเขียว

        cursor = NewRect("HandCursor", root); // เคอร์เซอร์มือของหน้านี้
        cursor.anchorMin = cursor.anchorMax = Vector2.zero; // anchor มุมล่างซ้าย
        cursor.sizeDelta = new Vector2(220, 220); // ขนาดเคอร์เซอร์
        cursorImg = cursor.gameObject.AddComponent<Image>(); // เพิ่ม Image
        cursorImg.sprite = openSprite; // เริ่มด้วยรูปมือแบ
        cursorImg.preserveAspect = true; // รักษาสัดส่วนรูป
        cursorImg.raycastTarget = false; // ไม่ให้บังการคลิก
    }

    // ---- ตัวช่วยสร้าง RectTransform เปล่า ----
    static RectTransform NewRect(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); // สร้าง GameObject ที่มี RectTransform
        var rt = (RectTransform)go.transform; // ดึง RectTransform
        rt.SetParent(parent, false); // ใส่ไว้ใต้ parent
        return rt; // คืนค่า
    }

    // ---- ตัวช่วยสร้างข้อความ TextMeshPro ----
    TMP_Text NewText(RectTransform parent, string name, float size, Vector2 pos, Vector2 box)
    {
        var rt = NewRect(name, parent); // สร้าง RectTransform
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); // ยึดกลาง parent
        rt.anchoredPosition = pos; // ตำแหน่งข้อความ
        rt.sizeDelta = box; // ขนาดกล่องข้อความ
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>(); // เพิ่มคอมโพเนนต์ข้อความ
        if (font) t.font = font; // ใช้ฟอนต์ที่ตั้งไว้ (ถ้ามี)
        t.fontSize = size; // ขนาดตัวอักษร
        t.color = Ink; // สีตัวอักษร
        t.alignment = TextAlignmentOptions.Center; // จัดกึ่งกลาง
        t.raycastTarget = false; // ข้อความไม่ต้องรับคลิก
        return t; // คืนค่า
    }
}
