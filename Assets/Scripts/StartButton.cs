using UnityEngine; // ใช้คลาสหลักของ Unity
using UnityEngine.SceneManagement; // ใช้ SceneManager สำหรับโหลดฉาก
using UnityEngine.UI; // ใช้คลาส Button

// ปุ่ม Start ในหน้า Title
// ถ้าในฉากมี TitleHandCheck จะให้เช็คมือก่อน (ตัวเช็คมือจะโหลดฉากถัดไปเอง)
// ถ้าไม่มี จะโหลดฉากเป้าหมายทันที
[RequireComponent(typeof(Button))] // บังคับว่า GameObject นี้ต้องมี Button
public class StartButton : MonoBehaviour // คลาสปุ่มเริ่มเกม
{
    // ---- ค่าที่ปรับได้: ชื่อฉากที่จะไปต่อ ----
    public string sceneName = "Tutorial"; // ชื่อฉากที่จะโหลดเมื่อกดปุ่ม

    // ---- ผูกฟังก์ชันกับปุ่มตอนเริ่ม ----
    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OnClick); // กดปุ่มแล้วให้เรียก OnClick
    }

    // ---- เมื่อกดปุ่ม ----
    void OnClick()
    {
        var check = FindAnyObjectByType<TitleHandCheck>(); // หาตัวเช็คมือในฉาก
        if (check != null) check.Begin(); // ถ้ามี ให้เริ่มขั้นตอนเช็คมือก่อน
        else SceneManager.LoadScene(sceneName); // ถ้าไม่มี โหลดฉากถัดไปเลย
    }
}
