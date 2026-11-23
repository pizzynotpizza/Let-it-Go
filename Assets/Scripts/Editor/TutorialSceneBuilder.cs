using UnityEditor; // ใช้ API ของ Editor (MenuItem, AssetDatabase, Undo)
using UnityEditor.SceneManagement; // ใช้ EditorSceneManager สำหรับ mark ฉากว่ามีการแก้ไข
using UnityEngine; // ใช้คลาสหลักของ Unity
using UnityEngine.UI; // ใช้ Image, Button, CanvasScaler

// เมนู: Let it Go > Build Tutorial (สคริปต์นี้ทำงานใน Editor เท่านั้น ไม่ติดไปในเกมที่ build)
// จัดฉาก Tutorial ที่เปิดอยู่ให้อัตโนมัติ: รูปหน้าเต็มจอ (Assets/Art/Backgrounds/Tutorial/1..9.jpg)
// กับปุ่ม Next/Start 1 ปุ่ม วางตรงตำแหน่งปุ่มในภาพดีไซน์
public static class TutorialSceneBuilder // คลาส static เครื่องมือสร้างฉาก
{
    [MenuItem("Let it Go/Build Tutorial")] // เพิ่มเมนูบนแถบด้านบนของ Unity
    public static void Build()
    {
        // ---- โหลดรูปหน้า tutorial 9 หน้า และตั้งค่า import ให้เป็น Sprite ----
        var pages = new Sprite[9]; // เตรียมช่อง 9 หน้า
        for (int i = 0; i < pages.Length; i++) // วนทีละหน้า
        {
            string path = $"Assets/Art/Backgrounds/Tutorial/{i + 1}.jpg"; // path รูปหน้าที่ i+1
            var ti = (TextureImporter)AssetImporter.GetAtPath(path); // ดึงตัวตั้งค่า import ของรูป
            if (ti != null && (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single)) // ถ้ายังไม่ได้ตั้งเป็น Sprite เดี่ยว
            {
                ti.textureType = TextureImporterType.Sprite; // ตั้งเป็น Sprite
                ti.spriteImportMode = SpriteImportMode.Single; // Sprite เดี่ยว ไม่ตัดหลายชิ้น
                ti.mipmapEnabled = false; // ปิด mipmap (UI ไม่ต้องใช้ ประหยัดหน่วยความจำ)
                ti.SaveAndReimport(); // บันทึกและ import ใหม่
            }
            pages[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path); // โหลด Sprite
            if (pages[i] == null) Debug.LogWarning("[Let it Go] Missing tutorial page " + path); // ไม่เจอรูปแจ้งเตือน
        }

        // ---- หา Canvas ในฉาก และตั้ง CanvasScaler ให้ใช้ 1920x1080 ----
        var canvas = Object.FindAnyObjectByType<Canvas>(); // หา Canvas
        if (canvas == null) { Debug.LogError("[Let it Go] No Canvas in the open scene."); return; } // ไม่มี Canvas หยุดทำ
        var root = (RectTransform)canvas.transform; // RectTransform ของ Canvas

        var scaler = canvas.GetComponent<CanvasScaler>(); // ดึง CanvasScaler
        if (scaler) // ถ้ามี
        {
            Undo.RecordObject(scaler, "Build Tutorial"); // บันทึกไว้ให้กด Undo ได้
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; // ปรับขนาดตามจอ
            scaler.referenceResolution = new Vector2(1920, 1080); // ความละเอียดอ้างอิง
            scaler.matchWidthOrHeight = 0.5f; // ถ่วงกว้าง/สูงเท่ากัน
        }

        // ---- พื้นหลัง: รูปหน้า tutorial เต็มจอ ----
        var bgT = root.Find("Background"); // หาออบเจกต์ Background
        Image bg; // Image ของพื้นหลัง
        if (bgT == null) // ยังไม่มีก็สร้างใหม่
        {
            var go = new GameObject("Background", typeof(RectTransform), typeof(Image)); // สร้างออบเจกต์พื้นหลัง
            Undo.RegisterCreatedObjectUndo(go, "Build Tutorial"); // ให้ Undo ลบได้
            go.transform.SetParent(root, false); // ใส่ใต้ Canvas
            bgT = go.transform; // เก็บ transform
        }
        bg = bgT.GetComponent<Image>(); // ดึง Image
        Undo.RecordObject(bg, "Build Tutorial"); // บันทึกให้ Undo ได้
        var fitter = bgT.GetComponent<AspectRatioFitter>(); // หา AspectRatioFitter เก่า
        if (fitter) Undo.DestroyObjectImmediate(fitter); // ลบทิ้ง (เราจะยืดเต็มจอเอง)
        var bgRt = (RectTransform)bgT; // RectTransform ของพื้นหลัง
        Undo.RecordObject(bgRt, "Build Tutorial"); // บันทึกให้ Undo ได้
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one; // ยืดเต็ม Canvas
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero; // ไม่มีขอบเว้น
        bg.sprite = pages[0]; // เริ่มด้วยหน้าแรก
        bg.color = Color.white; // สีปกติ
        bg.preserveAspect = false; // ยืดเต็มจอ ไม่รักษาสัดส่วน
        bg.raycastTarget = false; // ไม่รับคลิก
        bgT.SetAsFirstSibling(); // ให้อยู่ล่างสุด (วาดก่อน)

        // ---- ซ่อนส่วนของ layout เก่า (เดิมวาดข้อความทับกรอบเปล่า ตอนนี้ข้อความอยู่ในรูปแล้ว) ----
        foreach (var n in new[] { "Tutorial Panel", "Tutorial Text", "Stats Page" }) // ชื่อออบเจกต์เก่า
        {
            var t = root.Find(n); // หาออบเจกต์
            if (t == null) continue; // ไม่มีก็ข้าม
            Undo.RecordObject(t.gameObject, "Build Tutorial"); // บันทึกให้ Undo ได้
            t.gameObject.SetActive(false); // ซ่อน
            var oldPager = t.GetComponent<TutorialPager>(); // หา TutorialPager เก่าที่ติดอยู่
            if (oldPager) Undo.DestroyObjectImmediate(oldPager); // ลบทิ้ง
        }
        var spriteObj = GameObject.Find("Tutorial_0"); // หา sprite เก่าในฉาก
        if (spriteObj) { Undo.RecordObject(spriteObj, "Build Tutorial"); spriteObj.SetActive(false); } // ซ่อน

        // ---- ปุ่ม Next ----
        var btnT = root.Find("Next Button"); // หาปุ่ม Next
        if (btnT == null) { Debug.LogError("[Let it Go] 'Next Button' not found under the Canvas."); return; } // ไม่เจอปุ่ม หยุดทำ
        btnT.SetAsLastSibling(); // ให้อยู่บนสุด
        var btnImg = btnT.GetComponent<Image>(); // Image ของปุ่ม
        Undo.RecordObject(btnImg, "Build Tutorial"); // บันทึกให้ Undo ได้
        btnImg.preserveAspect = true; // รักษาสัดส่วนรูปปุ่ม
        btnImg.raycastTarget = true; // ปุ่มต้องรับคลิก

        // ---- ใส่ TutorialPager ไว้ที่ Canvas (active ตลอด) และผูกค่าทั้งหมด ----
        var pager = canvas.GetComponent<TutorialPager>(); // หา TutorialPager บน Canvas
        if (pager == null) pager = Undo.AddComponent<TutorialPager>(canvas.gameObject); // ไม่มีก็เพิ่ม
        Undo.RecordObject(pager, "Build Tutorial"); // บันทึกให้ Undo ได้
        pager.background = bg; // ผูกพื้นหลัง
        pager.pages = pages; // ผูกรูปทุกหน้า
        pager.nextButton = btnT.GetComponent<Button>(); // ผูกปุ่ม
        pager.nextSprite = LoadSprite("Assets/Art/UI/Button/Next Button.png"); // โหลดรูปปุ่ม Next
        pager.startSprite = LoadSprite("Assets/Art/UI/Button/Start Button.png"); // โหลดรูปปุ่ม Start
        btnImg.sprite = pager.nextSprite; // ตั้งรูปเริ่มต้นเป็น Next

        // ---- จัดตำแหน่งและขนาดปุ่มตามค่าใน TutorialPager ----
        var btnRt = (RectTransform)btnT; // RectTransform ของปุ่ม
        Undo.RecordObject(btnRt, "Build Tutorial"); // บันทึกให้ Undo ได้
        btnRt.anchorMin = btnRt.anchorMax = btnRt.pivot = new Vector2(0.5f, 0.5f); // ยึดกลางจอ
        btnRt.localScale = Vector3.one; // สเกลปกติ
        btnRt.localRotation = Quaternion.identity; // ไม่หมุน
        btnRt.anchoredPosition = pager.nextPos; // ตำแหน่งปุ่ม Next
        btnRt.sizeDelta = pager.nextSize; // ขนาดปุ่ม Next

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene()); // mark ฉากว่ามีการแก้ (จะได้กด Save ได้)
        Debug.Log("[Let it Go] Tutorial built."); // แจ้งว่าเสร็จ
    }

    // ---- โหลด Sprite จากไฟล์รูป (รองรับไฟล์ที่มีหลาย sub-asset) ----
    static Sprite LoadSprite(string path)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) // โหลดทุก asset ในไฟล์
            if (o is Sprite s) return s; // เจอ Sprite ตัวแรกก็คืนเลย
        Debug.LogWarning("[Let it Go] Sprite not found: " + path); // ไม่เจอแจ้งเตือน
        return null; // คืน null
    }
}
