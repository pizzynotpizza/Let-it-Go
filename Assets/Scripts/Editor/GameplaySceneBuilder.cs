using TMPro; // ใช้ TextMeshPro สำหรับข้อความ
using UnityEditor; // ใช้ API ของ Editor (MenuItem, AssetDatabase, Undo)
using UnityEditor.SceneManagement; // ใช้ EditorSceneManager สำหรับ mark ฉากว่ามีการแก้ไข
using UnityEngine; // ใช้คลาสหลักของ Unity
using UnityEngine.UI; // ใช้ Image, Button, Canvas, Outline

// เมนู: Let it Go > Build Gameplay UI (สคริปต์นี้ทำงานใน Editor เท่านั้น ไม่ติดไปในเกมที่ build)
// สร้าง (หรือสร้างใหม่ทับ) HUD, ตัวจับมือ และตัวปล่อยไอคอน ในฉาก Gameplay ที่เปิดอยู่
// พื้นหลังกับตัวละครจะอยู่ที่เดิมตามที่วางไว้
public static class GameplaySceneBuilder // คลาส static เครื่องมือสร้างฉากเกม
{
    // ---- ฟอนต์และสีหลักของ HUD ----
    const string FontPath = "Assets/Fonts/Kanit-Medium SDF.asset"; // ฟอนต์ไทย Kanit
    // สีตามหน้า Tutorial: เงิน = เขียว, ความสุข = เหลือง, พลังงาน = แดง
    static readonly Color MoneyColor = Hex("#2E9E3E"); // สีเงิน
    static readonly Color HappyColor = Hex("#E0A800"); // สีความสุข
    static readonly Color EnergyColor = Hex("#D32F2F"); // สีพลังงาน
    static readonly Color Ink = Hex("#4A2C6B"); // สีม่วงเข้ม ใช้กับกล่องเวลา

    // ---- path ของรูปที่ใช้ (ถ้าย้ายไฟล์ต้องแก้ตรงนี้) ----
    const string GameOverMoneyPath = "Assets/Art/Popups/GameOver/Gameover Money.png"; // รูปแพ้เพราะเงินหมด
    const string GameOverHealthPath = "Assets/Art/Popups/GameOver/Gameover Health.png"; // รูปแพ้เพราะพลังงานหมด
    const string VictoryFullPath = "Assets/Art/Popups/Victory/Victory.jpg"; // รูปหน้าชนะ
    public const string HandOpenPath = "Assets/Art/UI/Hand/Chibi Open.png"; // รูปมือแบ
    public const string HandFistPath = "Assets/Art/UI/Hand/Chibi Fist.png"; // รูปมือกำ
    public const string WorkerNormalPath = "Assets/Art/Characters/Worker Normal.png"; // ตัวละครหน้าปกติ
    public const string WorkerSadPath = "Assets/Art/Characters/Worker Sad.png"; // ตัวละครหน้าเศร้า
    public const string WorkerStressPath = "Assets/Art/Characters/Worker Stress.png"; // ตัวละครหน้าเครียด
    const string RetryPath = "Assets/Art/UI/Button/Retry Button.png"; // รูปปุ่ม Retry
    const string HomePath = "Assets/Art/UI/Button/Home Button.png"; // รูปปุ่ม Home
    static readonly string[] PopupArt = { GameOverMoneyPath, GameOverHealthPath, VictoryFullPath, RetryPath, HomePath, HandOpenPath, HandFistPath, WorkerNormalPath, WorkerSadPath, WorkerStressPath }; // รูปทั้งหมดที่ต้อง import เป็น Sprite เดี่ยว

    static TMP_FontAsset font; // ฟอนต์ที่โหลดแล้ว
    static Sprite rounded; // sprite ขอบมนของ Unity (ใช้ทำกล่อง/หลอด)

    [MenuItem("Let it Go/Build Gameplay UI")] // เพิ่มเมนูบนแถบด้านบนของ Unity
    public static void Build()
    {
        // ---- โหลดของที่ต้องใช้ และลบของเก่าที่เคยสร้างไว้ ----
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath); // โหลดฟอนต์
        rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"); // โหลด sprite ขอบมนที่มากับ Unity

        foreach (var n in new[] { "HUD Canvas", "GameManager", "HandTracking" }) // ชื่อออบเจกต์ที่สคริปต์นี้สร้าง
        {
            var old = GameObject.Find(n); // หาของเก่า
            if (old) Undo.DestroyObjectImmediate(old); // ลบทิ้ง (กด Undo ได้)
        }

        SetupWorld(); // ตั้งกล้องและพื้นหลัง

        // ---------- ตัวจับมือ ----------
        var trackingGo = new GameObject("HandTracking", typeof(HandTrackingClient)); // สร้างออบเจกต์รับข้อมูลมือ
        Undo.RegisterCreatedObjectUndo(trackingGo, "Build Gameplay UI"); // ให้ Undo ลบได้

        // ---------- Canvas หลักของ HUD ----------
        var canvasGo = new GameObject("HUD Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)); // สร้าง Canvas
        Undo.RegisterCreatedObjectUndo(canvasGo, "Build Gameplay UI"); // ให้ Undo ลบได้
        var canvas = canvasGo.GetComponent<Canvas>(); // ดึง Canvas
        // ใช้ Canvas แบบเดียวกับฉาก Title/Tutorial: ปรับขนาดตามจอ อ้างอิง 1920x1080 (match 0.5)
        // ใช้ Screen Space - Camera (ไม่ใช่ Overlay) กล้องจะถ่าย HUD ได้ด้วย (ใช้ทำภาพเบลอตอนเข้า Event) และวาดทับตัวละครตาม sortingOrder
        canvas.renderMode = RenderMode.ScreenSpaceCamera; // โหมด Screen Space - Camera
        canvas.worldCamera = Camera.main; // กล้องที่วาด Canvas และรับ event
        canvas.planeDistance = 5f; // ระยะ Canvas หน้ากล้อง
        canvas.sortingOrder = 10; // วาดทับพื้นหลังและตัวละคร
        var scaler = canvasGo.GetComponent<CanvasScaler>(); // ตัวปรับขนาด Canvas ตามจอ
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; // ปรับตามขนาดจอ
        scaler.referenceResolution = new Vector2(1920, 1080); // ขนาดอ้างอิงเท่าฉากอื่น
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; // เทียบทั้งกว้างและสูง
        scaler.matchWidthOrHeight = 0.5f; // ครึ่งๆ เท่าฉากอื่น
        var root = (RectTransform)canvasGo.transform; // RectTransform ของ Canvas (ขนาดจะถูกตั้งตามจออัตโนมัติ)

        // ---- เลเยอร์เล่นเกม: ไอคอนที่ตก + เคอร์เซอร์มือ ----
        var play = Rect("PlayLayer", root); // สร้างเลเยอร์
        Stretch(play); // ยืดเต็มจอ
        // ใส่ Canvas แยก: ไอคอนกับเอฟเฟกต์ขยับทุกเฟรม จะได้ไม่ rebuild mesh ของ HUD ทั้งหมดทุกเฟรม
        play.gameObject.AddComponent<Canvas>(); // Canvas ซ้อน

        // ---------- แถบด้านบน (หลอดสถานะ + เวลา) ----------
        var top = Rect("TopBar", root); // สร้างแถบบน
        top.anchorMin = new Vector2(0, 1); top.anchorMax = new Vector2(1, 1); // ยึดขอบบน ยืดเต็มความกว้าง
        top.pivot = new Vector2(0.5f, 1); // pivot ขอบบน
        top.sizeDelta = new Vector2(-40, 180); // แคบกว่าจอ 40 สูง 180
        top.anchoredPosition = new Vector2(0, -16); // ห่างขอบบน 16
        top.gameObject.AddComponent<Canvas>(); // หลอดขยับทุกเฟรม แยก batch
        var topBg = Img(top, rounded, new Color(1, 1, 1, 0.82f)); // พื้นแถบสีขาวโปร่งใสนิดๆ
        topBg.type = Image.Type.Sliced; // แบบ 9-slice ขอบมนไม่ยืดเพี้ยน
        top.gameObject.AddComponent<Outline>().effectColor = new Color(0.55f, 0.35f, 0.8f, 0.6f); // ขอบม่วง

        // ลำดับเหมือนหน้า Tutorial: เงิน, ความสุข, พลังงาน (ตัวเลขที่ 3 คือตำแหน่ง x)
        var moneyBar = MakeBar(top, "MoneyBar", 24, "เงิน", MoneyColor, LoadSprite("Assets/Art/Icons/Status/Money.png"), "{0:0}"); // หลอดเงิน
        var happyBar = MakeBar(top, "HappinessBar", 574, "ความสุข", HappyColor, LoadSprite("Assets/Art/Icons/Status/Happy.png"), "{0:0}"); // หลอดความสุข
        var energyBar = MakeBar(top, "EnergyBar", 1124, "พลังงาน", EnergyColor, LoadSprite("Assets/Art/Icons/Status/Energy.png"), "{0:0}"); // หลอดพลังงาน

        // ---- กล่องวันที่ + เวลา (ขวาของแถบบน) ----
        var timerBox = Rect("Timer", top); // สร้างกล่อง
        timerBox.anchorMin = timerBox.anchorMax = new Vector2(1, 0.5f); // ยึดขวา กลางแนวตั้ง
        timerBox.pivot = new Vector2(1, 0.5f); // pivot ขวา
        timerBox.sizeDelta = new Vector2(200, 150); // ขนาดกล่อง
        timerBox.anchoredPosition = new Vector2(-20, 0); // ห่างขอบขวา 20
        var tbImg = Img(timerBox, rounded, Ink); // พื้นกล่องสีม่วงเข้ม
        tbImg.type = Image.Type.Sliced; // 9-slice
        var dayText = Text(timerBox, "Day", "วันที่ 1/30", 28, Color.white); // ข้อความวันที่
        Place(dayText.rectTransform, new Vector2(0, 40), new Vector2(190, 40)); // วางด้านบนกล่อง
        var timerText = Text(timerBox, "Value", "3:00", 60, Hex("#FFD54F")); // ข้อความเวลา สีเหลือง
        Place(timerText.rectTransform, new Vector2(0, -16), new Vector2(190, 76)); // วางด้านล่างกล่อง

        // ---- ข้อความเตือนใต้แถบบน ----
        var stateText = Text(root, "StateText", "", 46, Color.white); // สร้างข้อความ
        var st = stateText.rectTransform; // RectTransform ของข้อความ
        st.anchorMin = st.anchorMax = new Vector2(0.5f, 1); // ยึดกลางบน
        st.sizeDelta = new Vector2(1100, 70); // ขนาดกล่อง
        st.anchoredPosition = new Vector2(0, -240); // ต่ำจากขอบบน 240
        stateText.outlineWidth = 0.25f; stateText.outlineColor = Color.white; // ขอบตัวอักษรสีขาว อ่านง่ายบนพื้นหลัง

        // ---------- เคอร์เซอร์มือ ----------
        var openSprite = LoadSprite(HandOpenPath); // รูปมือแบ
        var fistSprite = LoadSprite(HandFistPath); // รูปมือกำ
        var cursorRt = Rect("HandCursor", play); // สร้างเคอร์เซอร์
        cursorRt.sizeDelta = new Vector2(230, 230); // ขนาดเคอร์เซอร์
        var cursorImg = Img(cursorRt, openSprite, Color.white); // ใส่รูปมือแบ
        cursorImg.preserveAspect = true; // รักษาสัดส่วน
        var cursor = cursorRt.gameObject.AddComponent<HandCursor>(); // ใส่สคริปต์ HandCursor
        cursor.image = cursorImg; // ผูก Image
        cursor.openSprite = openSprite; // ผูกรูปแบ
        cursor.fistSprite = fistSprite; // ผูกรูปกำ
        cursor.grabRadius = 120f; // รัศมีคว้า

        // ---------- popup ตอนจบ (แพ้เงินหมด / แพ้พลังงานหมด) ----------
        // รูป popup ขนาด 1920x1080 ปุ่มวางตามตำแหน่งในภาพดีไซน์
        foreach (var p in PopupArt) MakeSingleSprite(p); // ตั้งรูปทั้งหมดเป็น Sprite เดี่ยว
        var overPanel = Rect("GameOverPanel", root); // สร้างหน้าแพ้
        Stretch(overPanel); // เต็มจอ
        Img(overPanel, null, new Color(0.1f, 0.05f, 0.2f, 0.6f)).raycastTarget = true; // พื้นมืด และบังการคลิกด้านหลัง
        const float popupScale = 0.8f; // ย่อ popup เหลือ 80% ของจอ
        var popup = Rect("Popup", overPanel); // สร้าง popup
        Place(popup, Vector2.zero, new Vector2(1920, 1080) * popupScale); // วางกลางจอ
        var popupImg = Img(popup, LoadSprite(GameOverMoneyPath), Color.white); // รูป popup เริ่มต้น
        popupImg.preserveAspect = true; // รักษาสัดส่วน
        // อ้างอิงจากดีไซน์: กลางปุ่มอยู่ประมาณ (962, 950) px บนภาพ 1920x1080 ขนาดประมาณ 530x265 px
        var btnPos = new Vector2(2, -410) * popupScale; // ตำแหน่งปุ่มกลาง
        var btnSize = new Vector2(530, 265) * popupScale; // ขนาดปุ่ม
        // หน้าแพ้มี 2 ปุ่มข้างกัน: Retry ซ้าย, Home ขวา
        var gap = new Vector2(290, 0) * popupScale; // ระยะห่างจากกลาง
        var retryBtn = MakeButton(popup, "RetryButton", LoadSprite(RetryPath), btnPos - gap, btnSize); // ปุ่ม Retry
        var homeBtn = MakeButton(popup, "HomeButton", LoadSprite(HomePath), btnPos + gap, btnSize); // ปุ่ม Home
        overPanel.gameObject.SetActive(false); // ซ่อนไว้ก่อน

        // ---------- หน้าชนะ (เต็มจอ อยู่รอดครบ 1 เดือน) ----------
        var winPanel = Rect("VictoryPanel", root); // สร้างหน้าชนะ
        Stretch(winPanel); // เต็มจอ
        winPanel.gameObject.AddComponent<CanvasGroup>(); // ใช้ปรับความโปร่งใสตอนแอนิเมชัน
        Img(winPanel, null, Color.black).raycastTarget = true; // พื้นดำ บังการคลิกด้านหลัง
        var winArt = Rect("Art", winPanel); // ภาพหน้าชนะ
        Stretch(winArt); // เต็มจอ
        var winImg = Img(winArt, LoadSprite(VictoryFullPath), Color.white); // ใส่รูปชนะ
        winImg.preserveAspect = false; // รูป 2000x1117 ใกล้ 16:9 อยู่แล้ว ยืดเต็มจอได้เลย
        // ปุ่ม Home ล่างกลาง
        var winHome = MakeButton(winPanel, "HomeButton", LoadSprite(HomePath), Vector2.zero, new Vector2(460, 230)); // สร้างปุ่ม Home
        var wh = (RectTransform)winHome.transform; // RectTransform ของปุ่ม
        wh.anchorMin = wh.anchorMax = new Vector2(0.5f, 0); // ยึดกลางล่าง
        wh.anchoredPosition = new Vector2(0, 150); // สูงจากขอบล่าง 150
        winPanel.gameObject.SetActive(false); // ซ่อนไว้ก่อน

        // ย้ายเคอร์เซอร์ไปวาดท้ายสุด ให้อยู่บน panel ด้วย root กับ PlayLayer เต็มจอและจุดเริ่มเดียวกัน
        // ตำแหน่งเคอร์เซอร์กับไอคอนจึงยังตรงกัน
        cursorRt.SetParent(root, true); // ย้ายมาอยู่ใต้ root (คงตำแหน่ง world)
        cursorRt.SetAsLastSibling(); // วาดทับทุกอย่าง

        // ---------- GameManager + ตัวปล่อยไอคอน ----------
        var gmGo = new GameObject("GameManager", typeof(GameManager), typeof(IconSpawner)); // สร้างออบเจกต์คุมเกม
        Undo.RegisterCreatedObjectUndo(gmGo, "Build Gameplay UI"); // ให้ Undo ลบได้
        var gm = gmGo.GetComponent<GameManager>(); // ดึง GameManager
        var spawner = gmGo.GetComponent<IconSpawner>(); // ดึง IconSpawner
        spawner.area = play; // พื้นที่ที่ไอคอนตก
        spawner.hand = cursor; // เคอร์เซอร์มือ
        spawner.font = font; // ฟอนต์
        spawner.items = BuildItems(); // รายการไอคอนทั้งหมด

        // ---- ผูก reference ทั้งหมดให้ GameManager ----
        gm.energyBar = energyBar; // หลอดพลังงาน
        gm.moneyBar = moneyBar; // หลอดเงิน
        gm.happinessBar = happyBar; // หลอดความสุข
        gm.timerText = timerText; // ข้อความเวลา
        gm.dayText = dayText; // ข้อความวันที่
        gm.stateText = stateText; // ข้อความเตือน
        gm.gameOverPanel = overPanel.gameObject; // หน้าแพ้
        gm.popup = popup; // popup
        gm.popupImage = popupImg; // รูปใน popup
        gm.gameOverMoneySprite = LoadSprite(GameOverMoneyPath); // รูปแพ้เงินหมด
        gm.gameOverHealthSprite = LoadSprite(GameOverHealthPath); // รูปแพ้พลังงานหมด
        gm.retryButton = retryBtn.gameObject; // ปุ่ม Retry
        gm.homeButton = homeBtn.gameObject; // ปุ่ม Home
        gm.handCursor = cursor; // เคอร์เซอร์มือ
        gm.victoryPanel = winPanel.gameObject; // หน้าชนะ
        gm.victoryArt = winArt; // ภาพหน้าชนะ
        gm.victoryHomeButton = wh; // ปุ่ม Home หน้าชนะ
        UnityEditor.Events.UnityEventTools.AddPersistentListener(winHome.onClick, gm.GoHome); // ปุ่ม Home หน้าชนะ -> GoHome (บันทึกลงฉาก)
        UnityEditor.Events.UnityEventTools.AddPersistentListener(retryBtn.onClick, gm.Retry); // ปุ่ม Retry -> Retry
        UnityEditor.Events.UnityEventTools.AddPersistentListener(homeBtn.onClick, gm.GoHome); // ปุ่ม Home หน้าแพ้ -> GoHome
        gm.spawner = spawner; // ตัวปล่อยไอคอน
        var charGo = GameObject.Find("char_normal_0"); // หาตัวละครในฉาก
        if (charGo) gm.character = charGo.GetComponent<SpriteRenderer>(); // ผูกตัวละคร
        gm.normalSprite = LoadSprite(WorkerNormalPath); // รูปหน้าปกติ
        gm.sadSprite = LoadSprite(WorkerSadPath); // รูปหน้าเศร้า
        gm.stressSprite = LoadSprite(WorkerStressPath); // รูปหน้าเครียด

        // ---- ต้องมี EventSystem ปุ่มถึงจะกดได้ ----
        if (!Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>()) // ยังไม่มี EventSystem
        {
            var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), // สร้าง EventSystem
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule)); // ใช้ input module ของ Input System ใหม่
            Undo.RegisterCreatedObjectUndo(es, "Build Gameplay UI"); // ให้ Undo ลบได้
        }

        SongkranSetup.Setup(); // ผูกของ Event สงกรานต์พักใจ
        TextShadowSetup.Apply(); // เงาดำให้ข้อความที่ไม่มีกรอบ
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene()); // mark ฉากว่ามีการแก้
        Debug.Log("[Let it Go] Gameplay UI built."); // แจ้งว่าเสร็จ
    }

    // ---- ตั้งกล้องให้ 1 หน่วย = 1 พิกเซลที่ 1920x1080 ตรงกับที่วางพื้นหลังและตัวละครไว้ ----
    static void SetupWorld()
    {
        var cam = Camera.main; // กล้องหลัก
        if (cam) // มีกล้อง
        {
            Undo.RecordObject(cam.transform, "Camera"); // บันทึกให้ Undo ได้
            Undo.RecordObject(cam, "Camera"); // บันทึกให้ Undo ได้
            cam.transform.position = new Vector3(960, 540, -10); // วางกล้องกลางภาพ 1920x1080
            cam.orthographic = true; // กล้องแบบ 2D (ไม่มี perspective)
            cam.orthographicSize = 540; // ครึ่งความสูง = 540 ทำให้เห็นสูง 1080 หน่วยพอดี
        }

        // พื้นหลังเคยเป็นลูกของ Canvas แบบ screen-space ทำให้ตำแหน่ง world ขึ้นกับขนาดหน้าต่าง
        // เลยย้ายไปอยู่ root ของฉาก โดยคงตำแหน่ง world เดิม
        var bg = GameObject.Find("Game_0"); // หาพื้นหลัง
        if (bg) // มีพื้นหลัง
        {
            if (bg.transform.parent != null) // ยังมี parent อยู่
                Undo.SetTransformParent(bg.transform, null, "Background"); // ย้ายไป root
            var bgSr = bg.GetComponent<SpriteRenderer>(); // SpriteRenderer ของพื้นหลัง
            bgSr.sortingOrder = -10; // วาดหลังสุด
            // ขยายให้เต็มภาพ 1920x1080 พอดี (แบบ cover: ไม่มีขอบว่าง อยู่กลาง)
            if (bgSr.sprite != null) // มีรูป
            {
                Undo.RecordObject(bg.transform, "Background"); // บันทึกให้ Undo ได้
                Vector2 size = bgSr.sprite.bounds.size; // ขนาดรูปเดิม
                float s = Mathf.Max(1920f / size.x, 1080f / size.y); // สเกลที่ทำให้เต็มทั้งกว้างและสูง
                bg.transform.localScale = new Vector3(s, s, 1); // ตั้งสเกล
                bg.transform.rotation = Quaternion.identity; // ไม่หมุน
                Vector3 offset = (Vector3)(Vector2)bgSr.sprite.bounds.center * s; // ชดเชย pivot ของรูป
                bg.transform.position = new Vector3(960, 540, 0) - offset; // วางกลางภาพ
            }
        }
    }

    // ---- รายการไอคอนทั้งหมดและผลของแต่ละอัน (ปรับสมดุลเกมตรงนี้) ----
    static IconSpawner.ItemDef[] BuildItems()
    {
        IconSpawner.ItemDef I(IconSpawner.Kind kind, string label, string path, float money, float happy, float energy, float weight = 1) => // ฟังก์ชันย่อยสร้าง ItemDef ให้เขียนสั้นลง
            new IconSpawner.ItemDef { label = label, kind = kind, sprite = LoadSprite("Assets/Art/Icons/" + path + ".png"), money = money, happiness = happy, energy = energy, weight = weight }; // สร้างข้อมูลไอคอนพร้อมโหลดรูป
        var work = IconSpawner.Kind.Work; // ตัวย่อประเภทงาน
        var relax = IconSpawner.Kind.Relax; // ตัวย่อประเภทพักผ่อน
        var recover = IconSpawner.Kind.Recover; // ตัวย่อประเภทฟื้นพลัง

        // ตาม Tutorial: งาน      = เงินเพิ่ม, ความสุขลด, พลังงานลด
        //              พักผ่อน   = ความสุขเพิ่ม, เงินลด, พลังงานลด
        //              กิน / นอน = พลังงานเพิ่ม
        return new[] // คืนรายการไอคอน
        {
            //            label           path                    money happy energy
            I(work,    "ทำงาน",       "Work/Work",             20, -14,  -14, 0.9f), // ทำงาน: ได้เงินเยอะสุด แต่เหนื่อยสุด (ออกน้อยลงนิด)
            I(work,    "เขียนโค้ด",    "Work/Coding",           16, -12,  -12), // เขียนโค้ด
            I(work,    "ประชุม",      "Work/Meeting",          12, -12,   -9), // ประชุม
            I(work,    "ตอบอีเมล",    "Work/Email",             8,  -7,   -6, 1.2f), // ตอบอีเมล: ได้น้อยเสียน้อย (ออกบ่อยขึ้น)
            I(relax,   "ออกกำลังกาย", "Gain Happy/Exercise",   -6,  14,  -10), // ออกกำลังกาย: สุขเยอะแต่เหนื่อย
            I(relax,   "เล่นเกม",      "Gain Happy/Game",      -14,  17,   -5), // เล่นเกม: สุขสุดแต่เสียเงินเยอะ
            I(relax,   "ฟังเพลง",      "Gain Happy/Music",      -8,  10,   -3, 1.2f), // ฟังเพลง: เบาๆ (ออกบ่อยขึ้น)
            I(recover, "กินข้าว",      "Gain Energy/Food",     -10,   2,   19, 1.2f), // กินข้าว: ได้พลังงาน เสียเงินนิดหน่อย
            I(recover, "นอนหลับ",     "Gain Energy/Sleep",      0,  -2,   22), // นอนหลับ: ได้พลังงานเยอะสุด ไม่เสียเงิน
        };
    }

    // ---- สร้างหลอดสถานะ 1 ชุด (ไอคอน + ชื่อ + หลอด + หลอดเงา + ตัวเลข) ----
    static StatusBar MakeBar(RectTransform parent, string name, float x, string label, Color color, Sprite icon, string format)
    {
        var group = Rect(name, parent); // กลุ่มของหลอดนี้
        group.anchorMin = group.anchorMax = new Vector2(0, 0.5f); // ยึดซ้าย กลางแนวตั้ง
        group.pivot = new Vector2(0, 0.5f); // pivot ซ้าย
        group.sizeDelta = new Vector2(530, 160); // ขนาดกลุ่ม
        group.anchoredPosition = new Vector2(x, 0); // ตำแหน่ง x ตามที่ส่งมา

        var iconRt = Rect("Icon", group); // ไอคอนสถานะ
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0, 0.5f); // ยึดซ้าย
        iconRt.pivot = new Vector2(0, 0.5f); // pivot ซ้าย
        iconRt.sizeDelta = new Vector2(150, 150); // ขนาดไอคอน
        Img(iconRt, icon, Color.white).preserveAspect = true; // ใส่รูปไอคอน รักษาสัดส่วน

        var title = Text(group, "Label", label, 40, color); // ชื่อสถานะ
        var tr = title.rectTransform; // RectTransform ของชื่อ
        tr.anchorMin = tr.anchorMax = new Vector2(0, 0.5f); // ยึดซ้าย
        tr.pivot = new Vector2(0, 0.5f); // pivot ซ้าย
        tr.sizeDelta = new Vector2(360, 54); // ขนาดกล่องข้อความ
        tr.anchoredPosition = new Vector2(165, 34); // อยู่ขวาไอคอน ด้านบน
        title.alignment = TextAlignmentOptions.Left; // ชิดซ้าย

        var bg = Rect("BarBG", group); // พื้นหลอด
        bg.anchorMin = bg.anchorMax = new Vector2(0, 0.5f); // ยึดซ้าย
        bg.pivot = new Vector2(0, 0.5f); // pivot ซ้าย
        bg.sizeDelta = new Vector2(360, 50); // ขนาดหลอด
        bg.anchoredPosition = new Vector2(162, -26); // อยู่ใต้ชื่อ
        Img(bg, rounded, new Color(0.29f, 0.17f, 0.42f, 0.85f)).type = Image.Type.Sliced; // พื้นม่วงเข้ม ขอบมน

        var area = Rect("FillArea", bg); // พื้นที่ด้านในหลอด
        Stretch(area); // เต็มพื้นหลอด
        area.offsetMin = new Vector2(5, 5); area.offsetMax = new Vector2(-5, -5); // เว้นขอบใน 5

        var ghost = Rect("Ghost", area); // หลอดเงา
        ghost.anchorMin = Vector2.zero; ghost.anchorMax = new Vector2(0.7f, 1); // เริ่มที่ 70%
        ghost.offsetMin = ghost.offsetMax = Vector2.zero; // ไม่มีขอบเว้น
        Img(ghost, rounded, new Color(1, 1, 1, 0.55f)).type = Image.Type.Sliced; // สีขาวโปร่งใส

        var fill = Rect("Fill", area); // หลอดหลัก
        fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0.7f, 1); // เริ่มที่ 70%
        fill.offsetMin = fill.offsetMax = Vector2.zero; // ไม่มีขอบเว้น
        Img(fill, rounded, color).type = Image.Type.Sliced; // สีตามสถานะ

        var value = Text(bg, "Value", "70", 32, Color.white); // ตัวเลขบนหลอด
        Stretch(value.rectTransform); // เต็มหลอด
        value.rectTransform.offsetMax = new Vector2(-14, 0); // เว้นขวา 14
        value.alignment = TextAlignmentOptions.Right; // ชิดขวา
        value.fontStyle = FontStyles.Bold; // ตัวหนา

        var bar = group.gameObject.AddComponent<StatusBar>(); // ใส่สคริปต์ StatusBar
        bar.fill = fill; // ผูกหลอดหลัก
        bar.ghostFill = ghost; // ผูกหลอดเงา
        bar.valueText = value; // ผูกตัวเลข
        bar.format = format; // รูปแบบตัวเลข
        return bar; // คืนค่า
    }

    // ---- สร้างปุ่มที่เป็นรูปภาพ ----
    static Button MakeButton(RectTransform parent, string name, Sprite sprite, Vector2 pos, Vector2 size)
    {
        var rt = Rect(name, parent); // สร้าง RectTransform
        Place(rt, pos, size); // วางตำแหน่งและขนาด
        var img = Img(rt, sprite, Color.white); // ใส่รูปปุ่ม
        img.preserveAspect = true; // รักษาสัดส่วน
        img.raycastTarget = true; // ต้องรับคลิก
        var btn = rt.gameObject.AddComponent<Button>(); // เพิ่ม Button
        btn.targetGraphic = img; // ให้ปุ่มเปลี่ยนสีที่รูปนี้
        var c = btn.colors; // ดึงชุดสีของปุ่ม
        c.highlightedColor = new Color(1f, 0.92f, 0.7f); // สีตอนชี้ (เหลืองอ่อน)
        c.pressedColor = new Color(0.85f, 0.75f, 0.6f); // สีตอนกด
        btn.colors = c; // ใส่ชุดสีกลับ
        return btn; // คืนค่า
    }

    // ---- รูปถูก import แบบ "Multiple" (ตัดเป็นหลายชิ้นอัตโนมัติ) แต่ popup ต้องใช้รูปเต็ม เลยตั้งเป็น Single ----
    public static void MakeSingleSprite(string path)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return; // ไม่ใช่รูปก็ข้าม
        if (ti.textureType == TextureImporterType.Sprite && ti.spriteImportMode == SpriteImportMode.Single) return; // ตั้งไว้ถูกแล้วก็ข้าม
        ti.textureType = TextureImporterType.Sprite; // ตั้งเป็น Sprite
        ti.spriteImportMode = SpriteImportMode.Single; // Sprite เดี่ยว
        ti.SaveAndReimport(); // บันทึกและ import ใหม่
    }

    // ---------- ฟังก์ชันช่วย ----------
    // ---- สร้าง RectTransform เปล่าใต้ parent ----
    static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); // สร้าง GameObject
        var rt = (RectTransform)go.transform; // ดึง RectTransform
        rt.SetParent(parent, false); // ใส่ใต้ parent
        return rt; // คืนค่า
    }

    // ---- ยืดเต็ม parent ----
    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; // anchor ทั้งกรอบ
        rt.offsetMin = rt.offsetMax = Vector2.zero; // ไม่มีขอบเว้น
    }

    // ---- วางตำแหน่งและขนาดโดยยึดกลาง parent ----
    static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); // ยึดกลาง
        rt.sizeDelta = size; // ขนาด
        rt.anchoredPosition = pos; // ตำแหน่ง
    }

    // ---- กล่องพื้นครีมขอบม่วง ----
    static void Panel(RectTransform rt)
    {
        var img = Img(rt, rounded, Hex("#FFF8E1")); // พื้นสีครีม
        img.type = Image.Type.Sliced; // 9-slice
        var o = rt.gameObject.AddComponent<Outline>(); // เพิ่มขอบ
        o.effectColor = Hex("#8E44AD"); // สีขอบม่วง
        o.effectDistance = new Vector2(6, -6); // ระยะขอบ
    }

    // ---- เพิ่ม Image ให้ RectTransform ----
    static Image Img(RectTransform rt, Sprite sprite, Color color)
    {
        var img = rt.gameObject.AddComponent<Image>(); // เพิ่ม Image
        img.sprite = sprite; // ตั้งรูป
        img.color = color; // ตั้งสี
        img.raycastTarget = false; // ค่าเริ่มต้นไม่รับคลิก (ปุ่มค่อยเปิดเอง)
        return img; // คืนค่า
    }

    // ---- สร้างข้อความ TextMeshPro ----
    static TextMeshProUGUI Text(RectTransform parent, string name, string text, float size, Color color)
    {
        var rt = Rect(name, parent); // สร้าง RectTransform
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>(); // เพิ่มคอมโพเนนต์ข้อความ
        if (font) t.font = font; // ใช้ฟอนต์ไทย
        t.text = text; // ข้อความ
        t.fontSize = size; // ขนาดตัวอักษร
        t.color = color; // สี
        t.alignment = TextAlignmentOptions.Center; // จัดกลาง
        t.raycastTarget = false; // ไม่รับคลิก
        t.textWrappingMode = TextWrappingModes.Normal; // ตัดบรรทัดอัตโนมัติ
        return t; // คืนค่า
    }

    // ---- โหลด Sprite จากไฟล์รูป ----
    static Sprite LoadSprite(string path)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) // โหลดทุก asset ในไฟล์
            if (o is Sprite s) return s; // เจอ Sprite ก็คืน
        Debug.LogWarning("[Let it Go] Sprite not found: " + path); // ไม่เจอแจ้งเตือน
        return null; // คืน null
    }

    // ---- แปลงรหัสสี hex (#RRGGBB) เป็น Color ----
    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var c); // แปลง hex
        return c; // คืนสี
    }
}

// ================= Event สงกรานต์พักใจ =================

// ผูกของสำหรับ Event "สงกรานต์พักใจ" เข้ากับ GameManager / IconSpawner ในฉาก Gameplay ที่เปิดอยู่
// เรียกเองจากเมนู Let it Go/Setup Songkran Event หรือถูกเรียกต่อท้าย Build Gameplay UI
public static class SongkranSetup
{
    const string BackgroundPath = "Assets/Art/Backgrounds/Songkran/Songkran 1 Temple.png"; // พื้นหลังสงกรานต์ อันแรก (วัด)
    static readonly string[] BackgroundPaths = { // ฉากหลังสงกรานต์ตามลำดับ เปลี่ยนทุก 20 วิ
        "Assets/Art/Backgrounds/Songkran/Songkran 1 Temple.png", // 1 วัด (อันเดิม)
        "Assets/Art/Backgrounds/Songkran/Songkran 2 Street.png", // 2 บนถนน
        "Assets/Art/Backgrounds/Songkran/Songkran 3 Countryside.png" }; // 3 บ้านต่างจังหวัด
    const string BannerPath = "Assets/Art/Popups/Songkran/Songkran Festival.png"; // ป้ายสงกรานต์พักใจ
    const string NormalPath = "Assets/Art/Characters/Songkran/Songkran Normal.png"; // ตัวละครชุดสงกรานต์ หน้าปกติ
    const string HappyPath = "Assets/Art/Characters/Songkran/Songkran Happy.png"; // ตัวละครชุดสงกรานต์ หน้ามีความสุข
    const string HandOpenPath = "Assets/Art/UI/Hand/Songkran/Songkran Open.png"; // มือแบ แขนเสื้อลายดอก
    const string HandFistPath = "Assets/Art/UI/Hand/Songkran/Songkran Fist.png"; // มือกำ แขนเสื้อลายดอก
    static readonly string[] IconFiles = { "Water Play", "Massage", "Khao Chae", "Nap", "Bathing Buddha", "Bonus", "Relatives Money" }; // ไอคอนสงกรานต์ (Assets/Art/Icons/Songkran)

    [MenuItem("Let it Go/Setup Songkran Event")] // เมนูบนแถบด้านบนของ Unity
    public static void Setup()
    {
        var gm = Object.FindAnyObjectByType<GameManager>(); // หา GameManager ในฉาก
        if (!gm) { Debug.LogWarning("[Let it Go] Open the Gameplay scene first (GameManager not found)."); return; } // ไม่เจอ
        var spawner = gm.GetComponent<IconSpawner>(); // ตัวปล่อยไอคอน

        foreach (var p in new[] { BackgroundPath, BannerPath, NormalPath, HappyPath, HandOpenPath, HandFistPath }) GameplaySceneBuilder.MakeSingleSprite(p); // ตั้งรูปเป็น Sprite เดี่ยว
        foreach (var f in IconFiles) GameplaySceneBuilder.MakeSingleSprite("Assets/Art/Icons/Songkran/" + f + ".png"); // ไอคอนสงกรานต์เป็น Sprite เดี่ยว

        Undo.RecordObject(gm, "Setup Songkran Event"); // ให้ Undo ได้
        gm.songkranBackground = Load(BackgroundPath); // พื้นหลัง
        foreach (var p in BackgroundPaths) GameplaySceneBuilder.MakeSingleSprite(p); // ตั้งเป็น Sprite เดี่ยว
        gm.songkranBackgrounds = System.Array.ConvertAll(BackgroundPaths, Load); // ฉากหลังทั้ง 3 อัน
        gm.songkranBanner = Load(BannerPath); // ป้าย
        gm.songkranNormalSprite = Load(NormalPath); // ตัวละครปกติ
        gm.songkranHappySprite = Load(HappyPath); // ตัวละครมีความสุข
        gm.songkranHandOpen = Load(HandOpenPath); // มือแบสงกรานต์
        gm.songkranHandFist = Load(HandFistPath); // มือกำสงกรานต์
        var bg = GameObject.Find("Game_0"); // พื้นหลังในฉาก
        if (bg) gm.background = bg.GetComponent<SpriteRenderer>(); // ผูกพื้นหลัง
        if (spawner) // ใส่รายการไอคอนสงกรานต์
        {
            Undo.RecordObject(spawner, "Setup Songkran Event"); // ให้ Undo ได้
            spawner.eventItems = BuildEventItems(); // ไอคอนกิจกรรมพิเศษ
        }
        EditorUtility.SetDirty(gm); // บันทึกค่า
        if (spawner) EditorUtility.SetDirty(spawner); // บันทึกค่า
        EditorSceneManager.MarkSceneDirty(gm.gameObject.scene); // mark ฉากว่ามีการแก้
        Debug.Log("[Let it Go] Songkran event wired."); // แจ้งว่าเสร็จ
    }

    // ---- กิจกรรมสงกรานต์ (เงินถูกล็อกช่วง Event เลยไม่มีผลกับเงิน) ----
    static IconSpawner.ItemDef[] BuildEventItems()
    {
        IconSpawner.ItemDef I(string label, string file, float happy, float energy, float money = 0, float weight = 1, int minCombo = 0) => // ฟังก์ชันย่อยสร้าง ItemDef
            new IconSpawner.ItemDef { label = label, kind = IconSpawner.Kind.Songkran, sprite = Load("Assets/Art/Icons/Songkran/" + file + ".png"), money = money, happiness = happy, energy = energy, weight = weight, minCombo = minCombo };
        return new[]
        {
            //  label              file                                       happy energy
            //  label              file              happy energy money weight
            I("เล่นน้ำสงกรานต์", "Water Play",        20,   -8), // เพิ่มความสุข แต่ใช้พลังงาน
            I("นวดผ่อนคลาย",    "Massage",           10,   18), // เพิ่มพลังงาน
            I("กินข้าวแช่",      "Khao Chae",          8,   20), // ฟื้นฟูพลังงาน
            I("นอนพักกลางวัน",  "Nap",                5,   22), // ฟื้นฟูพลังงานสูง
            I("สรงน้ำพระ",       "Bathing Buddha",    15,   10), // เพิ่มทั้งความสุขและพลังงาน
            I("ได้โบนัส",        "Bonus",              3,    0,   15, 0.8f, 3), // เงินโบนัส (เงินเยอะกว่าญาติ): ตกมา 1 อันทุกๆ 10 คอมโบ
            I("ญาติให้ตังค์",     "Relatives Money",    8,    0,    8, 0.6f, 5), // รดน้ำดำหัวผู้ใหญ่ได้ตังค์ (อันละน้อยกว่าโบนัส): ฉากบ้านต่างจังหวัด ทุกๆ 10 คอมโบตกมา 2 อัน
        };
    }

    static Sprite Load(string path)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Sprite s) return s; // เจอ Sprite ก็คืน
        Debug.LogWarning("[Let it Go] Sprite not found: " + path); // ไม่เจอแจ้งเตือน
        return null;
    }
}

// ================= เงาขาวให้ข้อความที่ไม่มีกรอบ =================
// ข้อความที่วางบนภาพพื้นหลังตรงๆ (ไม่มีกล่องรองหลัง) อ่านยาก จึงใช้ material ที่มีเงาดำ (TMP Underlay)
// เรียกจากเมนู Let it Go/Apply Text Shadows (ทำทุกฉากที่เปิดอยู่) และถูกเรียกต่อท้าย Build Gameplay UI
public static class TextShadowSetup
{
    const string FontPath = "Assets/Fonts/Kanit-Medium SDF.asset"; // ฟอนต์หลัก
    const string ShadowMatPath = "Assets/Fonts/Kanit-Medium SDF Shadow.mat"; // material ข้อความแบบมีเงา

    // ---- สร้าง (หรือโหลด) material เงาดำ ปรับความเข้ม/ระยะเงาได้ตรงนี้ ----
    public static Material ShadowMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ShadowMatPath); // มีอยู่แล้วก็ใช้เลย
        var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(FontPath); // ฟอนต์หลัก
        if (!font) return null; // ไม่มีฟอนต์
        if (!mat)
        {
            mat = new Material(font.material) { name = "Kanit-Medium SDF Shadow" }; // คัดลอกจาก material ของฟอนต์
            AssetDatabase.CreateAsset(mat, ShadowMatPath); // บันทึกเป็นไฟล์
        }
        mat.EnableKeyword("UNDERLAY_ON"); // เปิดเงา
        mat.SetColor("_UnderlayColor", new Color(1, 1, 1, 0.95f)); // เงาขาวเกือบทึบ (ลองแทนเงาดำ)
        mat.SetFloat("_UnderlayOffsetX", 0.6f); // เงาเยื้องขวา
        mat.SetFloat("_UnderlayOffsetY", -0.6f); // เงาเยื้องลง
        mat.SetFloat("_UnderlayDilate", 0.5f); // เงาหนาขึ้นเล็กน้อย (ช่วยเหมือนขอบ)
        mat.SetFloat("_UnderlaySoftness", 0.25f); // ขอบเงานุ่ม
        EditorUtility.SetDirty(mat); // บันทึกค่า
        return mat;
    }

    // ---- ข้อความถือว่า "มีกรอบ" เมื่อ parent มี Image ทึบรองหลังอยู่ ----
    public static bool HasBox(TMPro.TMP_Text t)
    {
        var p = t.transform.parent; // parent ของข้อความ
        var img = p ? p.GetComponent<Image>() : null; // Image ของ parent
        return img && img.enabled && img.color.a > 0.3f; // มีกล่องทึบรองหลัง
    }

    // ---- ป้ายเกจด้านบน (เงิน/ความสุข/พลังงาน) ไม่ต้องมีเงา ----
    static bool InTopBar(TMPro.TMP_Text t)
    {
        for (var p = t.transform.parent; p; p = p.parent) if (p.name == "TopBar") return true; // อยู่ใต้ TopBar
        return false;
    }

    [MenuItem("Let it Go/Apply Text Shadows")] // เมนูบนแถบด้านบนของ Unity
    public static void Apply()
    {
        var mat = ShadowMaterial(); // material เงา
        if (!mat) { Debug.LogWarning("[Let it Go] Font not found: " + FontPath); return; }
        int n = 0; // นับจำนวนที่ใส่เงา
        foreach (var t in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)) // ทุกข้อความในฉาก
        {
            if (t.font == null || t.font.material.mainTexture != mat.mainTexture) continue; // ใช้ฟอนต์อื่น ข้าม
            if (HasBox(t) || InTopBar(t)) // มีกรอบแล้ว หรือเป็นป้ายเกจด้านบน: ไม่ใส่เงา (ถ้าเคยใส่ไว้ก็เอาออก)
            {
                if (t.fontSharedMaterial == mat) { Undo.RecordObject(t, "Apply Text Shadows"); t.fontSharedMaterial = t.font.material; EditorUtility.SetDirty(t); }
                continue;
            }
            Undo.RecordObject(t, "Apply Text Shadows"); // ให้ Undo ได้
            t.fontSharedMaterial = mat; // ใส่ material เงา
            EditorUtility.SetDirty(t); // บันทึกค่า
            n++;
        }
        foreach (var s in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)) { EditorSceneManager.MarkSceneDirty(s.gameObject.scene); break; } // mark ฉากว่ามีการแก้
        AssetDatabase.SaveAssets(); // บันทึก material
        Debug.Log("[Let it Go] Text shadow applied to " + n + " text(s)."); // แจ้งผล
    }
}
