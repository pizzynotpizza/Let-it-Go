using TMPro; // ใช้ TextMeshPro สำหรับข้อความตัวเลข
using UnityEngine; // ใช้คลาสหลักของ Unity

// หลอดสถานะ 1 หลอด: ความกว้างของหลอดจะตามค่า value / max
// ขยับแบบนุ่มๆ มีหลอดเงา (ghost) ค้างให้เห็นว่าลด/เพิ่มไปเท่าไหร่ และสั่นตอนค่าลด
public class StatusBar : MonoBehaviour // คลาสหลอดสถานะ
{
    // ---- อ้างอิงออบเจกต์และค่าที่ปรับได้ ----
    public RectTransform fill; // หลอดหลักที่แสดงค่าปัจจุบัน
    public RectTransform ghostFill; // หลอดเงาที่ค่อยๆ ตามหลัง ให้เห็นว่าเสีย/ได้ไปเท่าไหร่
    public TMP_Text valueText; // ข้อความแสดงตัวเลข
    public string format = "{0:0}"; // รูปแบบตัวเลข (0 ตำแหน่งทศนิยม)
    public float lowThreshold = 0.25f; // ถ้าค่าต่ำกว่า 25% หลอดจะเต้นเตือน

    // ---- ตัวแปรภายใน ----
    float shown; // สัดส่วนที่หลอดหลักแสดงอยู่ตอนนี้ (0-1)
    float ghost; // สัดส่วนของหลอดเงา (0-1)
    float target; // สัดส่วนเป้าหมายที่หลอดต้องวิ่งไปหา (0-1)
    float shake; // ความแรงการสั่นที่เหลืออยู่
    RectTransform rt; // RectTransform ของหลอดทั้งอัน
    Vector2 basePos; // ตำแหน่งปกติของหลอด ใช้เป็นจุดกลางตอนสั่น

    // ---- จำตำแหน่งเดิมไว้ ----
    void Awake()
    {
        rt = (RectTransform)transform; // ดึง RectTransform ของตัวเอง
        basePos = rt.anchoredPosition; // จำตำแหน่งตั้งต้น
    }

    // ---- ตั้งค่าหลอดจากภายนอก (GameManager เรียกทุกเฟรม) ----
    public void Set(float value, float max, bool instant = false)
    {
        float t = Mathf.Clamp01(value / max); // แปลงเป็นสัดส่วน 0-1
        if (t < target - 0.001f) shake = 0.3f; // ถ้าค่าลดลง ให้หลอดสั่น 0.3 วินาที
        target = t; // ตั้งเป้าหมายใหม่
        if (instant) shown = ghost = t; // ถ้าสั่งแบบทันที ให้หลอดกระโดดไปเลยไม่ต้องค่อยๆ ขยับ
        // Set() ถูกเรียกทุกเฟรม (ค่าสถานะลดลงตลอด) เลยอัปเดตข้อความเฉพาะตอนตัวเลขเปลี่ยน
        // ไม่ต้องสร้าง string ใหม่ 60 ครั้งต่อวินาที
        int shownValue = Mathf.RoundToInt(value); // ปัดเป็นจำนวนเต็มเพื่อเทียบว่าเลขเปลี่ยนไหม
        if (valueText != null && (instant || shownValue != lastShownValue)) // เปลี่ยนข้อความเฉพาะตอนเลขต่างจากเดิม
        {
            lastShownValue = shownValue; // จำเลขที่แสดงล่าสุด
            valueText.text = string.Format(format, value); // อัปเดตข้อความ
        }
    }

    int lastShownValue = int.MinValue; // เลขที่แสดงล่าสุด เริ่มด้วยค่าต่ำสุดเพื่อให้รอบแรกอัปเดตแน่นอน

    // ---- ทุกเฟรม: ขยับหลอด หลอดเงา การสั่น และการเต้นตอนค่าต่ำ ----
    void Update()
    {
        float dt = Time.unscaledDeltaTime; // เวลาระหว่างเฟรม (ไม่ขึ้นกับ timeScale)
        shown = Mathf.Lerp(shown, target, 1f - Mathf.Exp(-12f * dt)); // หลอดหลักวิ่งเข้าหาเป้าแบบนุ่ม (12 = ความเร็ว)
        ghost = Mathf.MoveTowards(ghost, target, dt * 0.35f); // หลอดเงาขยับช้าๆ คงที่ 35% ต่อวินาที
        if (fill) fill.anchorMax = new Vector2(shown, 1); // ปรับความกว้างหลอดหลักด้วย anchorMax.x
        if (ghostFill) ghostFill.anchorMax = new Vector2(Mathf.Max(ghost, shown), 1); // หลอดเงาต้องไม่สั้นกว่าหลอดหลัก

        shake = Mathf.MoveTowards(shake, 0, dt); // ลดความแรงการสั่นลงเรื่อยๆ
        bool low = target <= lowThreshold; // ค่าต่ำกว่าเกณฑ์หรือยัง
        Vector2 off = Vector2.zero; // ระยะเลื่อนจากการสั่น
        if (shake > 0) off.x = Mathf.Sin(Time.unscaledTime * 60f) * 6f * shake; // สั่นซ้ายขวาเร็วๆ สูงสุด 6 หน่วย
        rt.anchoredPosition = basePos + off; // ย้ายหลอดตามการสั่น
        rt.localScale = Vector3.one * (low ? 1f + 0.03f * Mathf.Sin(Time.unscaledTime * 10f) : 1f); // ค่าต่ำให้หลอดเต้นขยาย 3% เตือนผู้เล่น
    }
}
