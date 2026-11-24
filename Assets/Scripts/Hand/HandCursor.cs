using UnityEngine; // ใช้คลาสหลักของ Unity
using UnityEngine.UI; // ใช้ Image

// ไอคอนมือบน UI ที่ขยับตาม HandTrackingClient สลับรูประหว่างแบมือ (Open Palm) กับกำมือ (Closed Fist)
[RequireComponent(typeof(RectTransform))] // ต้องมี RectTransform เพราะเป็น UI
public class HandCursor : MonoBehaviour // คลาสเคอร์เซอร์มือในฉากเกม
{
    // ---- อ้างอิงรูปและค่าที่ปรับได้ ----
    public Image image; // Image ที่ใช้แสดงรูปมือ
    public Sprite openSprite; // รูปมือแบ
    public Sprite fistSprite; // รูปมือกำ
    [Tooltip("Radius in canvas units used to catch icons.")] // คำอธิบายใน Inspector
    public float grabRadius = 110f; // รัศมี (หน่วย canvas) ที่ถือว่ามือคว้าโดนไอคอน ยิ่งมากยิ่งคว้าง่าย
    public Color lostColor = new Color(1, 1, 1, 0.35f); // สีมือตอนกล้องหามือไม่เจอ (โปร่งใส)

    // ---- ตัวแปรภายใน ----
    RectTransform rt; // RectTransform ของเคอร์เซอร์
    RectTransform parentRt; // RectTransform ของ parent ใช้หาขนาดพื้นที่
    float punch; // ค่าแอนิเมชันตอนกำมือ (1 = เพิ่งกำ แล้วค่อยลดเป็น 0)

    public Vector2 CanvasPosition => rt.anchoredPosition; // ตำแหน่งมือบน canvas ให้สคริปต์อื่นอ่าน

    // ---- ตั้งค่าเริ่มต้น ----
    void Awake()
    {
        rt = (RectTransform)transform; // ดึง RectTransform ของตัวเอง
        parentRt = (RectTransform)rt.parent; // ดึง RectTransform ของ parent
        if (image == null) image = GetComponent<Image>(); // ถ้าไม่ได้ลาก Image ใส่ ให้หาเองจากตัวเอง
        if (image) image.enabled = true; // ในฉากปิดรูปมือไว้ (ไม่ให้บังตอนแก้ฉากใน Editor) เปิดตอนเริ่มเล่น
        // ยึด anchor ไว้มุมล่างซ้าย ทำให้ anchoredPosition = ระยะจากมุมล่างซ้ายของ canvas
        rt.anchorMin = rt.anchorMax = Vector2.zero; // ตั้ง anchor เป็น (0,0)
    }

    // ---- ทุกเฟรม: ย้ายเคอร์เซอร์ตามมือ และเปลี่ยนรูป/สี ----
    void Update()
    {
        var hand = HandTrackingClient.Instance; // ดึงตัวรับข้อมูลมือ
        if (hand == null) return; // ยังไม่มีตัวรับข้อมูลก็ไม่ต้องทำอะไร

        Vector2 size = parentRt.rect.size; // ขนาดพื้นที่ parent
        rt.anchoredPosition = Vector2.Scale(hand.ScreenPos01, size); // แปลงตำแหน่งมือ 0-1 เป็นหน่วย canvas

        image.sprite = hand.IsFist ? fistSprite : openSprite; // กำมือใช้รูปกำ แบมือใช้รูปแบ
        image.color = hand.Detected ? Color.white : lostColor; // หามือไม่เจอให้จางลง

        if (hand.FistDown) punch = 1f; // เฟรมที่เพิ่งกำมือ เริ่มแอนิเมชันยุบ
        // แก้ scale เฉพาะตอนแอนิเมชันทำงาน ไม่งั้น canvas จะถูก mark dirty ทุกเฟรม (เปลือง)
        if (punch > 0f || rt.localScale.x != 1f) // ยังมีแอนิเมชันค้างอยู่หรือสเกลยังไม่กลับปกติ
        {
            punch = Mathf.MoveTowards(punch, 0f, Time.unscaledDeltaTime * 6f); // ลด punch ลง (หมดภายใน ~0.17 วิ)
            rt.localScale = Vector3.one * (1f - 0.15f * punch); // ยุบเคอร์เซอร์ลงสูงสุด 15%
        }
    }
}
