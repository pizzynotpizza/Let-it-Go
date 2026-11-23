using UnityEngine; // ใช้คลาสหลักของ Unity
using UnityEngine.InputSystem; // ใช้ Input System ใหม่ อ่านปุ่มคีย์บอร์ด
using UnityEngine.SceneManagement; // ใช้ SceneManager สำหรับโหลดฉาก
using UnityEngine.UI; // ใช้ Image กับ Button

// แสดงหน้าสอนเล่นเป็นภาพเต็มจอทีละหน้า (Assets/Art/Backgrounds/Tutorial/1..9.jpg)
// ในรูปแต่ละหน้ามีกรอบกับข้อความอยู่แล้ว มีแค่ปุ่มที่เป็นออบเจกต์แยก
// กดปุ่มเพื่อไปหน้าถัดไป พอถึงหน้าสุดท้ายปุ่มจะเปลี่ยนเป็นรูป Start (วางตรงตำแหน่งปุ่ม Start ในภาพ) แล้วกดเพื่อเข้าเกม
public class TutorialPager : MonoBehaviour // คลาสเปลี่ยนหน้า tutorial
{
    // ---- อ้างอิงออบเจกต์/รูปที่ใช้ (ลากใส่ใน Inspector) ----
    public Image background; // Image พื้นหลังที่ใช้แสดงแต่ละหน้า
    public Sprite[] pages; // รูปของแต่ละหน้า เรียงตามลำดับ
    public Button nextButton; // ปุ่ม Next/Start
    public Sprite nextSprite; // รูปปุ่ม Next
    public Sprite startSprite; // รูปปุ่ม Start (ใช้หน้าสุดท้าย)
    public string gameplayScene = "Gameplay"; // ชื่อฉากเกมที่จะไปหลังหน้าสุดท้าย

    // ---- ตำแหน่งและขนาดปุ่ม ----
    // หน่วยเป็น canvas 1920x1080 ยึดกึ่งกลางจอ ปรับให้ปุ่มตรงกับกรอบปุ่มในภาพ
    [Header("Button layout (canvas units, 1920x1080, centre anchor)")] // หัวข้อใน Inspector
    public Vector2 nextPos = new Vector2(0, -436); // ตำแหน่งปุ่ม Next
    public Vector2 nextSize = new Vector2(550, 275); // ขนาดปุ่ม Next
    public Vector2 startPos = new Vector2(-3, -416); // ตำแหน่งปุ่ม Start
    public Vector2 startSize = new Vector2(480, 240); // ขนาดปุ่ม Start

    // ---- ตัวแปรภายใน ----
    RectTransform buttonRect; // RectTransform ของปุ่ม ใช้ย้ายตำแหน่ง/ขนาด
    Image buttonImage; // Image ของปุ่ม ใช้เปลี่ยนรูป
    int index; // หน้าที่แสดงอยู่ตอนนี้ (เริ่มที่ 0)

    // ---- ตั้งค่าปุ่มและแสดงหน้าแรก ----
    void Awake()
    {
        buttonRect = (RectTransform)nextButton.transform; // ดึง RectTransform ของปุ่ม
        buttonImage = nextButton.GetComponent<Image>(); // ดึง Image ของปุ่ม
        buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = new Vector2(0.5f, 0.5f); // ตั้ง anchor/pivot ไว้กลางจอ ให้ค่าตำแหน่งข้างบนใช้ได้ตรง
        buttonRect.localScale = Vector3.one; // รีเซ็ตสเกลปุ่มเป็นปกติ
        nextButton.onClick.AddListener(OnNext); // กดปุ่มแล้วเรียก OnNext
        Show(0); // แสดงหน้าแรก
    }

    // ---- เช็คคีย์บอร์ดทุกเฟรม (ไว้เทสต์) ----
    void Update()
    {
        // ปุ่มลัดตอนเทสต์: Enter หรือ ลูกศรขวา = ไปหน้าถัดไป
        var kb = Keyboard.current; // ดึงคีย์บอร์ดปัจจุบัน (อาจเป็น null ถ้าไม่มีคีย์บอร์ด)
        if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)) OnNext(); // กด Enter/→ ในเฟรมนี้ ให้ไปหน้าถัดไป
    }

    // ---- กดปุ่มถัดไป ----
    void OnNext()
    {
        if (index >= pages.Length - 1) // ถ้าอยู่หน้าสุดท้ายแล้ว
            SceneManager.LoadScene(gameplayScene); // เข้าฉากเล่นเกม
        else // ถ้ายังไม่ใช่หน้าสุดท้าย
            Show(index + 1); // ไปหน้าถัดไป
    }

    // ---- แสดงหน้าที่ i และปรับปุ่มให้ตรงกับหน้านั้น ----
    void Show(int i)
    {
        index = i; // จำว่าตอนนี้อยู่หน้าไหน
        if (background && pages.Length > 0) background.sprite = pages[i]; // เปลี่ยนรูปพื้นหลังเป็นหน้าที่ i
        bool last = index >= pages.Length - 1; // เช็คว่าเป็นหน้าสุดท้ายไหม
        buttonImage.sprite = last ? startSprite : nextSprite; // หน้าสุดท้ายใช้รูป Start นอกนั้นใช้ Next
        buttonRect.anchoredPosition = last ? startPos : nextPos; // ย้ายปุ่มตามชนิดปุ่ม
        buttonRect.sizeDelta = last ? startSize : nextSize; // ปรับขนาดปุ่มตามชนิดปุ่ม
    }
}
