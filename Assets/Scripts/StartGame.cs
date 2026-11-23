using UnityEngine; // ใช้คลาสหลักของ Unity
using UnityEngine.SceneManagement; // ใช้ SceneManager สำหรับโหลดฉาก

/// <summary>
/// โหลดฉากตามชื่อ เอา LoadTarget() ไปผูกกับ OnClick ของปุ่มได้เลย
/// ฉากที่จะโหลดต้องอยู่ใน File > Build Profiles (Build Settings) ด้วย
/// </summary>
public class StartGame : MonoBehaviour // คลาสสำหรับเปลี่ยนฉาก
{
    // ---- ค่าที่ปรับได้: ชื่อฉากปลายทาง ----
    public string sceneName = "Tutorial"; // ชื่อฉากที่จะโหลด

    // ---- ฟังก์ชันให้ปุ่มเรียก ----
    public void LoadTarget()
    {
        SceneManager.LoadScene(sceneName); // โหลดฉากตามชื่อที่ตั้งไว้
    }
}
