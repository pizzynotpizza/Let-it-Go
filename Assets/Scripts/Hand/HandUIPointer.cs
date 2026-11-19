using System.Collections.Generic; // ใช้ List
using UnityEngine; // ใช้คลาสหลักของ Unity
using UnityEngine.EventSystems; // ใช้ EventSystem ยิง raycast และส่ง event คลิก/hover ให้ UI
using UnityEngine.UI; // ใช้ Canvas, CanvasScaler, Image, Selectable

// เคอร์เซอร์มือสำหรับฉากเมนู (Title, Tutorial): ชี้มือไปที่ปุ่ม UI แล้วกำมือเพื่อกด
// ปุ่มที่มือชี้อยู่จะขึ้น highlight แบบปกติของปุ่ม
// ถ้าตัว tracker MediaPipe ยังไม่รัน จะสั่งเปิดให้
// สร้าง canvas overlay ของตัวเอง เลยต้องการแค่รูปมือ 2 รูป
public class HandUIPointer : MonoBehaviour // คลาสเคอร์เซอร์มือสำหรับกดปุ่มเมนู
{
    // ---- ค่าที่ปรับได้ใน Inspector ----
    public Sprite openSprite; // รูปมือแบ
    public Sprite fistSprite; // รูปมือกำ
    public Vector2 cursorSize = new Vector2(210, 210); // ขนาดเคอร์เซอร์มือ
    [Tooltip("Ignore fists for this long after the scene opens, so a fist from the last scene does not click.")] // คำอธิบายใน Inspector
    public float startDelay = 0.6f; // ไม่รับการกำมือช่วงแรกหลังเปิดฉาก (วินาที) กันกำค้างจากฉากก่อนแล้วกดโดนปุ่ม
    [Tooltip("Hide the cursor while this object is active (for example a panel with its own cursor).")] // คำอธิบายใน Inspector
    public TitleHandCheck hideWhileRunning; // ถ้าหน้าเช็คมือกำลังทำงาน ให้ซ่อนเคอร์เซอร์นี้ (เพราะมีเคอร์เซอร์ของตัวเอง)

    // ---- ตัวแปรภายใน ----
    RectTransform canvasRt, cursor; // RectTransform ของ canvas และของเคอร์เซอร์
    Image cursorImg; // Image ของเคอร์เซอร์
    GameObject hovered; // ปุ่มที่มือชี้อยู่ตอนนี้
    PointerEventData ped; // ข้อมูล pointer ที่ใช้ส่ง event ให้ UI (สร้างครั้งเดียวแล้วใช้ซ้ำ)
    readonly List<RaycastResult> hits = new List<RaycastResult>(); // ผล raycast ของ UI (ใช้ซ้ำกัน GC)
    float age; // เวลาที่ผ่านไปตั้งแต่เปิดฉาก

    // ---- เริ่ม: เปิด tracker และสร้าง UI เคอร์เซอร์ ----
    void Start()
    {
        HandTrackingClient.Ensure(); // ให้แน่ใจว่ามีตัวรับข้อมูลมือ (และเปิด Python tracker)
        Build(); // สร้าง canvas และเคอร์เซอร์
    }

    // ---- ทุกเฟรม: ย้ายเคอร์เซอร์ หาปุ่มใต้มือ และกดปุ่มเมื่อกำมือ ----
    void Update()
    {
        age += Time.unscaledDeltaTime; // นับเวลาตั้งแต่เปิดฉาก
        var hand = HandTrackingClient.Instance; // ดึงข้อมูลมือ
        bool hide = hand == null || (hideWhileRunning != null && hideWhileRunning.IsRunning); // ซ่อนถ้าไม่มีข้อมูลมือ หรือหน้าเช็คมือเปิดอยู่
        cursor.gameObject.SetActive(!hide); // เปิด/ปิดเคอร์เซอร์
        if (hide) { SetHover(null); return; } // ถ้าซ่อนก็ยกเลิก hover แล้วจบเฟรม

        Vector2 screen = Vector2.Scale(hand.ScreenPos01, new Vector2(Screen.width, Screen.height)); // ตำแหน่งมือเป็นพิกเซลจอ (ใช้ raycast)
        cursor.anchoredPosition = Vector2.Scale(hand.ScreenPos01, canvasRt.rect.size); // ตำแหน่งมือเป็นหน่วย canvas (ใช้วางเคอร์เซอร์)
        cursorImg.sprite = hand.IsFist ? fistSprite : openSprite; // เปลี่ยนรูปตามท่ามือ
        cursorImg.color = hand.Detected ? Color.white : new Color(1, 1, 1, 0.35f); // หามือไม่เจอให้จางลง

        // ถ้าใช้โหมดเมาส์แทนมือ UI ปกติของ Unity กดปุ่มให้อยู่แล้ว ไม่ต้องทำซ้ำ
        if (hand.UsingMouse || EventSystem.current == null) { SetHover(null); return; } // ข้ามส่วนกดปุ่มด้วยมือ

        GameObject target = hand.Detected ? ButtonUnder(screen) : null; // หาปุ่มที่อยู่ใต้มือ
        SetHover(target); // อัปเดต highlight ของปุ่ม

        if (target != null && hand.FistDown && age >= startDelay) // ถ้ามีปุ่มใต้มือ + เพิ่งกำมือ + พ้นช่วงหน่วงแล้ว
        {
            ExecuteEvents.Execute(target, Ped(screen), ExecuteEvents.pointerClickHandler); // ส่ง event คลิกให้ปุ่ม
            punch = 1f; // เริ่มแอนิเมชันยุบเคอร์เซอร์
        }
        punch = Mathf.MoveTowards(punch, 0f, Time.unscaledDeltaTime * 6f); // ค่อยๆ ลดแอนิเมชันยุบ
        cursor.localScale = Vector3.one * (1f - 0.15f * punch); // ยุบเคอร์เซอร์สูงสุด 15%
    }

    float punch; // ค่าแอนิเมชันตอนกด (1 -> 0)

    // ---- สร้าง/อัปเดต PointerEventData ตามตำแหน่งจอ ----
    PointerEventData Ped(Vector2 screen)
    {
        if (ped == null) ped = new PointerEventData(EventSystem.current); // สร้างครั้งแรกครั้งเดียว
        ped.position = screen; // ตั้งตำแหน่ง pointer
        return ped; // คืนค่าไปใช้ส่ง event
    }

    // ---- หาปุ่มบนสุดที่กดได้ใต้มือ ----
    GameObject ButtonUnder(Vector2 screen)
    {
        hits.Clear(); // ล้างผล raycast เก่า
        EventSystem.current.RaycastAll(Ped(screen), hits); // ยิง raycast หา UI ทุกชิ้นใต้ตำแหน่งนี้ (เรียงจากบนลงล่าง)
        foreach (var h in hits) // ไล่ดูทีละชิ้น
        {
            var go = ExecuteEvents.GetEventHandler<IPointerClickHandler>(h.gameObject); // หาออบเจกต์ที่รับคลิกได้ (ตัวมันหรือ parent)
            if (go == null) continue; // ไม่รับคลิก ข้าม
            var sel = go.GetComponent<Selectable>(); // ดูว่าเป็นปุ่ม/Selectable ไหม
            if (sel != null && !sel.IsInteractable()) continue; // ปุ่มถูกปิดใช้งาน ข้าม
            return go; // เจอปุ่มที่กดได้ คืนค่าเลย
        }
        return null; // ไม่เจอปุ่ม
    }

    // ---- จัดการ hover: ส่ง pointerExit ให้ปุ่มเก่า และ pointerEnter ให้ปุ่มใหม่ ----
    void SetHover(GameObject go)
    {
        if (go == hovered) return; // ปุ่มเดิม ไม่ต้องทำอะไร
        if (hovered != null) ExecuteEvents.Execute(hovered, Ped(Vector2.zero), ExecuteEvents.pointerExitHandler); // บอกปุ่มเก่าว่ามือออกไปแล้ว
        hovered = go; // จำปุ่มใหม่
        if (hovered != null) ExecuteEvents.Execute(hovered, ped, ExecuteEvents.pointerEnterHandler); // บอกปุ่มใหม่ว่ามือชี้อยู่ (ขึ้น highlight)
    }

    void OnDisable() => SetHover(null); // ถูกปิดเมื่อไหร่ ยกเลิก hover ให้ปุ่มไม่ค้าง highlight

    // ---- สร้าง canvas overlay และเคอร์เซอร์มือด้วยโค้ด ----
    void Build()
    {
        var canvasGo = new GameObject("HandPointerCanvas", typeof(Canvas), typeof(CanvasScaler)); // สร้าง GameObject ที่มี Canvas + CanvasScaler
        canvasGo.transform.SetParent(transform, false); // ให้อยู่ใต้ออบเจกต์นี้
        var canvas = canvasGo.GetComponent<Canvas>(); // ดึง Canvas
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; // วาดทับบนสุดของจอ
        canvas.sortingOrder = 1000; // ลำดับสูงมาก ให้อยู่บน UI อื่นทั้งหมด
        var scaler = canvasGo.GetComponent<CanvasScaler>(); // ดึง CanvasScaler
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; // ปรับขนาด UI ตามขนาดจอ
        scaler.referenceResolution = new Vector2(1920, 1080); // ความละเอียดอ้างอิง
        scaler.matchWidthOrHeight = 0.5f; // ถ่วงความกว้างกับความสูงเท่ากัน
        canvasRt = (RectTransform)canvasGo.transform; // เก็บ RectTransform ของ canvas

        var go = new GameObject("HandPointer", typeof(RectTransform), typeof(Image)); // สร้างออบเจกต์เคอร์เซอร์
        cursor = (RectTransform)go.transform; // เก็บ RectTransform ของเคอร์เซอร์
        cursor.SetParent(canvasRt, false); // ใส่ไว้ใน canvas
        cursor.anchorMin = cursor.anchorMax = Vector2.zero; // anchor มุมล่างซ้าย
        cursor.sizeDelta = cursorSize; // ตั้งขนาดเคอร์เซอร์
        cursorImg = go.GetComponent<Image>(); // ดึง Image
        cursorImg.sprite = openSprite; // เริ่มด้วยรูปมือแบ
        cursorImg.preserveAspect = true; // รักษาสัดส่วนรูป
        cursorImg.raycastTarget = false; // ไม่ให้เคอร์เซอร์บังการกดปุ่มด้านล่าง
    }
}
