using UnityEngine; // ใช้คลาสหลักของ Unity (MonoBehaviour, RectTransform, Mathf ฯลฯ)

// สคริปต์ทำให้โลโก้ในหน้า UI ดูมีชีวิต: ลอยขึ้นลง เอียงซ้ายขวา และขยายหดเบาๆ
// ใส่ไว้ที่ GameObject ของโลโก้ (ต้องมี RectTransform) แล้วปรับค่าใน Inspector ได้เลย
public class LogoFloat : MonoBehaviour // คลาสคอมโพเนนต์ของโลโก้ลอย
{
    // ---- ค่าที่ปรับได้ ----
    // floatHeight/floatSpeed = ระยะกับความเร็วที่ลอยขึ้นลง
    // tiltAngle/tiltSpeed   = มุมกับความเร็วที่เอียงซ้ายขวา
    // pulseScale/pulseSpeed = ขนาดกับความเร็วที่ขยายหด
    public float floatHeight = 20f; // ลอยขึ้นลงสูงสุดกี่หน่วย (หน่วย canvas)
    public float floatSpeed = 1.5f; // ความเร็วการลอยขึ้นลง
    public float tiltAngle = 3f; // เอียงได้สูงสุดกี่องศา
    public float tiltSpeed = 1.2f; // ความเร็วการเอียงไปมา
    public float pulseScale = 0.04f; // ขยายหดได้กี่เปอร์เซ็นต์ (0.04 = 4%)
    public float pulseSpeed = 2f; // ความเร็วการขยายหด

    // ---- ตัวแปรภายใน ----
    RectTransform rt; // เก็บ RectTransform ของโลโก้ไว้ใช้ซ้ำ ไม่ต้อง GetComponent ทุกเฟรม
    Vector2 startPos; // ตำแหน่งเริ่มต้นของโลโก้ ใช้เป็นจุดกลางของการลอย

    // ---- เริ่มต้น: จำตำแหน่งตั้งต้นไว้ก่อน ----
    void Awake()
    {
        rt = GetComponent<RectTransform>(); // ดึง RectTransform ของตัวเอง
        startPos = rt.anchoredPosition; // จำตำแหน่งเดิมไว้ จะได้ลอยรอบจุดนี้
    }

    // ---- ทุกเฟรม: คำนวณการลอย เอียง และขยายด้วย sin wave ----
    void Update()
    {
        float t = Time.unscaledTime; // ใช้เวลาแบบ unscaled เพื่อให้ยังขยับได้แม้ timeScale = 0 (ตอน pause)
        rt.anchoredPosition = startPos + Vector2.up * Mathf.Sin(t * floatSpeed) * floatHeight; // ลอยขึ้นลงตาม sin
        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * tiltSpeed) * tiltAngle); // หมุนแกน Z ให้เอียงซ้ายขวา
        rt.localScale = Vector3.one * (1f + Mathf.Sin(t * pulseSpeed) * pulseScale); // ขยายหดรอบขนาดปกติ (1)
    }
}
