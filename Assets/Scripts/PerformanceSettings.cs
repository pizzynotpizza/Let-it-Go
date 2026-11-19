using UnityEngine; // ใช้คลาสหลักของ Unity (QualitySettings, Application, Physics ฯลฯ)

// ตั้งค่าให้เกมใช้ CPU/GPU น้อยลง ทำงานครั้งเดียวตอนเริ่มเกม ไม่ต้องใส่ในฉาก
// เกมเป็น UI 2D อย่างเดียว 60 fps ก็ลื่นพอ ถ้าไม่ล็อกไว้ VSync บนจอ 144 Hz
// จะทำให้ Unity วาด 144 เฟรมต่อวินาที งานเพิ่มเกินเท่าตัว และไปแย่ง CPU จากตัว hand tracker ที่เป็น Python
public static class PerformanceSettings // คลาส static ไม่ต้องสร้างออบเจกต์
{
    // ---- ค่าที่ปรับได้: fps เป้าหมาย ----
    public const int TargetFps = 60; // จำกัด fps ไว้ที่ 60

    // ---- ใช้ค่าตั้งค่าทั้งหมดตอนเริ่มเกม ----
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] // ให้ Unity เรียกเองก่อนโหลดฉากแรก
    static void Apply()
    {
        QualitySettings.vSyncCount = 0; // ปิด VSync ไม่งั้น fps จะวิ่งตามจอ (เช่น 144 Hz)
        Application.targetFrameRate = TargetFps; // ล็อก fps ตามค่าที่ตั้งไว้

        // ปิดฟีเจอร์กราฟิกที่เกม 2D ไม่ได้ใช้
        QualitySettings.antiAliasing = 0; // sprite กับ UI ไม่ต้องใช้ MSAA
        QualitySettings.shadows = ShadowQuality.Disable; // ปิดเงา
        QualitySettings.realtimeReflectionProbes = false; // ปิด reflection probe แบบ realtime
        QualitySettings.softParticles = false; // ปิด soft particles
        // เกมนี้ไม่มี rigidbody หรือ collider เลย เลยข้ามการคำนวณฟิสิกส์ (ปกติ 50 ครั้ง/วินาที)
        Physics.simulationMode = SimulationMode.Script; // ฟิสิกส์ 3D จะไม่รันเอง
        Physics2D.simulationMode = SimulationMode2D.Script; // ฟิสิกส์ 2D จะไม่รันเอง
    }
}
