using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Menu: Let it Go > Build Gameplay UI
// Builds (or rebuilds) the HUD, hand tracking, calibration and spawner in the open Gameplay scene.
// Leaves the background and the character where they were placed.
public static class GameplaySceneBuilder
{
    const string FontPath = "Assets/Fonts/Kanit-Medium SDF.asset";
    // Colours from the Tutorial pages: money green, happiness yellow, energy red.
    static readonly Color MoneyColor = Hex("#2E9E3E");
    static readonly Color HappyColor = Hex("#E0A800");
    static readonly Color EnergyColor = Hex("#D32F2F");
    static readonly Color Ink = Hex("#4A2C6B");

    static TMP_FontAsset font;
    static Sprite rounded;

    [MenuItem("Let it Go/Build Gameplay UI")]
    public static void Build()
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        foreach (var n in new[] { "HUD Canvas", "GameManager", "HandTracking" })
        {
            var old = GameObject.Find(n);
            if (old) Undo.DestroyObjectImmediate(old);
        }

        SetupWorld();

        // ---------- Hand tracking ----------
        var trackingGo = new GameObject("HandTracking", typeof(HandTrackingClient));
        Undo.RegisterCreatedObjectUndo(trackingGo, "Build Gameplay UI");

        // ---------- Canvas ----------
        var canvasGo = new GameObject("HUD Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGo, "Build Gameplay UI");
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)canvasGo.transform;

        // Play layer: falling icons + hand cursor.
        var play = Rect("PlayLayer", root);
        Stretch(play);

        // ---------- Top bar ----------
        var top = Rect("TopBar", root);
        top.anchorMin = new Vector2(0, 1); top.anchorMax = new Vector2(1, 1);
        top.pivot = new Vector2(0.5f, 1);
        top.sizeDelta = new Vector2(-40, 180);
        top.anchoredPosition = new Vector2(0, -16);
        var topBg = Img(top, rounded, new Color(1, 1, 1, 0.82f));
        topBg.type = Image.Type.Sliced;
        top.gameObject.AddComponent<Outline>().effectColor = new Color(0.55f, 0.35f, 0.8f, 0.6f);

        // Order matches the Tutorial: money, happiness, energy.
        var moneyBar = MakeBar(top, "MoneyBar", 24, "เงิน", MoneyColor, LoadSprite("Assets/Icon/Status/Money.png"), "{0:0}");
        var happyBar = MakeBar(top, "HappinessBar", 574, "ความสุข", HappyColor, LoadSprite("Assets/Icon/Status/Happy.png"), "{0:0}");
        var energyBar = MakeBar(top, "EnergyBar", 1124, "พลังงาน", EnergyColor, LoadSprite("Assets/Icon/Status/Energy.png"), "{0:0}");

        // Day + timer (right side of top bar). 3 minutes = 1 month.
        var timerBox = Rect("Timer", top);
        timerBox.anchorMin = timerBox.anchorMax = new Vector2(1, 0.5f);
        timerBox.pivot = new Vector2(1, 0.5f);
        timerBox.sizeDelta = new Vector2(200, 150);
        timerBox.anchoredPosition = new Vector2(-20, 0);
        var tbImg = Img(timerBox, rounded, Ink);
        tbImg.type = Image.Type.Sliced;
        var dayText = Text(timerBox, "Day", "วันที่ 1/30", 28, Color.white);
        Place(dayText.rectTransform, new Vector2(0, 40), new Vector2(190, 40));
        var timerText = Text(timerBox, "Value", "3:00", 60, Hex("#FFD54F"));
        Place(timerText.rectTransform, new Vector2(0, -16), new Vector2(190, 76));

        // Warning text under the top bar
        var stateText = Text(root, "StateText", "", 46, Color.white);
        var st = stateText.rectTransform;
        st.anchorMin = st.anchorMax = new Vector2(0.5f, 1);
        st.sizeDelta = new Vector2(1100, 70);
        st.anchoredPosition = new Vector2(0, -240);
        stateText.outlineWidth = 0.25f; stateText.outlineColor = Color.white;

        // Controls hint, bottom left
        var hint = Text(root, "Hint", "กำมือที่ไอคอนเพื่อเลือกกิจกรรม  •  C = ปรับเทียบมือใหม่", 28, Ink);
        var hr = hint.rectTransform;
        hr.anchorMin = hr.anchorMax = new Vector2(0, 0);
        hr.pivot = new Vector2(0, 0);
        hr.sizeDelta = new Vector2(900, 50);
        hr.anchoredPosition = new Vector2(30, 20);
        hint.alignment = TextAlignmentOptions.Left;
        hint.outlineWidth = 0.3f; hint.outlineColor = Color.white;

        // ---------- Hand cursor ----------
        var openSprite = LoadSprite("Assets/Icon/Hand/Open Palm.png");
        var fistSprite = LoadSprite("Assets/Icon/Hand/Closed Fist.png");
        var cursorRt = Rect("HandCursor", play);
        cursorRt.sizeDelta = new Vector2(250, 250);
        var cursorImg = Img(cursorRt, openSprite, Color.white);
        cursorImg.preserveAspect = true;
        var cursor = cursorRt.gameObject.AddComponent<HandCursor>();
        cursor.image = cursorImg;
        cursor.openSprite = openSprite;
        cursor.fistSprite = fistSprite;
        cursor.grabRadius = 130f;

        // ---------- Calibration panel ----------
        // Only shown with an old tracker (no gestures) or when the player presses C.
        var calPanel = Rect("CalibrationPanel", root);
        Stretch(calPanel);
        Img(calPanel, null, new Color(0.1f, 0.05f, 0.2f, 0.6f));
        var calFrame = Rect("Frame", calPanel);
        calFrame.sizeDelta = new Vector2(1100, 560);
        Panel(calFrame);
        var calTitle = Text(calFrame, "Title", "ปรับเทียบมือ", 52, Ink);
        Place(calTitle.rectTransform, new Vector2(-110, 150), new Vector2(800, 70));
        var calBody = Text(calFrame, "Body", "", 40, Ink);
        Place(calBody.rectTransform, new Vector2(-110, 30), new Vector2(800, 140));
        var preview = Rect("HandPreview", calFrame);
        Place(preview, new Vector2(380, 30), new Vector2(240, 240));
        var previewImg = Img(preview, openSprite, Color.white);
        previewImg.preserveAspect = true;
        var progBg = Rect("Progress", calFrame);
        Place(progBg, new Vector2(-110, -110), new Vector2(700, 36));
        Img(progBg, rounded, new Color(0.29f, 0.17f, 0.42f, 0.25f)).type = Image.Type.Sliced;
        var progFill = Rect("Fill", progBg);
        progFill.anchorMin = Vector2.zero; progFill.anchorMax = new Vector2(0, 1);
        progFill.offsetMin = progFill.offsetMax = Vector2.zero;
        Img(progFill, rounded, Hex("#6BCB3F")).type = Image.Type.Sliced;
        var skip = Text(calFrame, "SkipHint", "กด Space เพื่อข้าม (ใช้ค่าเดิม)", 26, new Color(0.29f, 0.17f, 0.42f, 0.7f));
        Place(skip.rectTransform, new Vector2(0, -200), new Vector2(800, 40));

        // Script lives on the canvas (always active) so C can reopen the hidden panel.
        var calibration = canvasGo.AddComponent<HandCalibration>();
        calibration.panel = calPanel.gameObject;
        calibration.titleText = calTitle;
        calibration.bodyText = calBody;
        calibration.progressFill = progFill;
        calibration.previewIcon = previewImg;
        calibration.openSprite = openSprite;
        calibration.fistSprite = fistSprite;

        // ---------- Game over panel ----------
        var overPanel = Rect("GameOverPanel", root);
        Stretch(overPanel);
        Img(overPanel, null, new Color(0.1f, 0.05f, 0.2f, 0.6f));
        var overFrame = Rect("Frame", overPanel);
        overFrame.sizeDelta = new Vector2(1000, 480);
        Panel(overFrame);
        var overTitle = Text(overFrame, "Title", "Game Over", 80, Hex("#E53935"));
        Place(overTitle.rectTransform, new Vector2(0, 70), new Vector2(900, 110));
        var overBody = Text(overFrame, "Body", "", 40, Ink);
        Place(overBody.rectTransform, new Vector2(0, -70), new Vector2(860, 120));
        overPanel.gameObject.SetActive(false);

        // Cursor drawn last so it is above panels too. Root and PlayLayer are both
        // full-screen with the same origin, so cursor and icon positions match.
        cursorRt.SetParent(root, true);
        cursorRt.SetAsLastSibling();

        // ---------- Game manager + spawner ----------
        var gmGo = new GameObject("GameManager", typeof(GameManager), typeof(IconSpawner));
        Undo.RegisterCreatedObjectUndo(gmGo, "Build Gameplay UI");
        var gm = gmGo.GetComponent<GameManager>();
        var spawner = gmGo.GetComponent<IconSpawner>();
        spawner.area = play;
        spawner.hand = cursor;
        spawner.font = font;
        spawner.items = BuildItems();

        gm.energyBar = energyBar;
        gm.moneyBar = moneyBar;
        gm.happinessBar = happyBar;
        gm.timerText = timerText;
        gm.dayText = dayText;
        gm.stateText = stateText;
        gm.gameOverPanel = overPanel.gameObject;
        gm.gameOverTitle = overTitle;
        gm.gameOverBody = overBody;
        gm.calibration = calibration;
        gm.spawner = spawner;
        var charGo = GameObject.Find("char_normal_0");
        if (charGo) gm.character = charGo.GetComponent<SpriteRenderer>();
        gm.normalSprite = LoadSprite("Assets/Icon/Character/char_normal.png");
        gm.sadSprite = LoadSprite("Assets/Icon/Character/char_sad.png");
        gm.stressSprite = LoadSprite("Assets/Icon/Character/char_stress.png");

        if (!Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>())
        {
            var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Build Gameplay UI");
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[Let it Go] Gameplay UI built.");
    }

    // Camera shows the world in pixel units (1 unit = 1 px at 1920x1080),
    // matching where the background and character were placed by hand.
    static void SetupWorld()
    {
        var cam = Camera.main;
        if (cam)
        {
            Undo.RecordObject(cam.transform, "Camera");
            Undo.RecordObject(cam, "Camera");
            cam.transform.position = new Vector3(960, 540, -10);
            cam.orthographic = true;
            cam.orthographicSize = 540;
        }

        // Background was a child of the screen-space canvas; its world position then
        // depends on window size. Move it to the scene root, same world position.
        var bg = GameObject.Find("Game_0");
        if (bg)
        {
            if (bg.transform.parent != null)
                Undo.SetTransformParent(bg.transform, null, "Background");
            var bgSr = bg.GetComponent<SpriteRenderer>();
            bgSr.sortingOrder = -10;
            // Fill the 1920x1080 view exactly (cover: no empty border, centred).
            if (bgSr.sprite != null)
            {
                Undo.RecordObject(bg.transform, "Background");
                Vector2 size = bgSr.sprite.bounds.size;
                float s = Mathf.Max(1920f / size.x, 1080f / size.y);
                bg.transform.localScale = new Vector3(s, s, 1);
                bg.transform.rotation = Quaternion.identity;
                Vector3 offset = (Vector3)(Vector2)bgSr.sprite.bounds.center * s;
                bg.transform.position = new Vector3(960, 540, 0) - offset;
            }
        }
    }

    static IconSpawner.ItemDef[] BuildItems()
    {
        IconSpawner.ItemDef I(IconSpawner.Kind kind, string label, string path, float money, float happy, float energy, float weight = 1) =>
            new IconSpawner.ItemDef { label = label, kind = kind, sprite = LoadSprite("Assets/Icon/" + path + ".png"), money = money, happiness = happy, energy = energy, weight = weight };
        var work = IconSpawner.Kind.Work;
        var relax = IconSpawner.Kind.Relax;
        var recover = IconSpawner.Kind.Recover;

        // Tutorial: work  = +money, -happiness, -energy
        //           relax = +happiness, -money, -energy
        //           food / sleep = +energy
        return new[]
        {
            //            label           path                    money happy energy
            I(work,    "ทำงาน",       "Work/Work",             20, -12,  -14, 0.9f),
            I(work,    "เขียนโค้ด",    "Work/Coding",           16, -10,  -12),
            I(work,    "ประชุม",      "Work/Meeting",          12, -10,   -9),
            I(work,    "ตอบอีเมล",    "Work/Email",             8,  -6,   -6, 1.2f),
            I(relax,   "ออกกำลังกาย", "Gain Happy/Exercise",   -6,  14,  -10),
            I(relax,   "เล่นเกม",      "Gain Happy/Game",      -14,  18,   -5),
            I(relax,   "ฟังเพลง",      "Gain Happy/Music",      -8,  10,   -3, 1.2f),
            I(recover, "กินข้าว",      "Gain Energy/Food",     -10,   2,   16, 1.2f),
            I(recover, "นอนหลับ",     "Gain Energy/Sleep",      0,  -2,   22),
        };
    }

    static StatusBar MakeBar(RectTransform parent, string name, float x, string label, Color color, Sprite icon, string format)
    {
        var group = Rect(name, parent);
        group.anchorMin = group.anchorMax = new Vector2(0, 0.5f);
        group.pivot = new Vector2(0, 0.5f);
        group.sizeDelta = new Vector2(530, 160);
        group.anchoredPosition = new Vector2(x, 0);

        var iconRt = Rect("Icon", group);
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0, 0.5f);
        iconRt.pivot = new Vector2(0, 0.5f);
        iconRt.sizeDelta = new Vector2(150, 150);
        Img(iconRt, icon, Color.white).preserveAspect = true;

        var title = Text(group, "Label", label, 40, color);
        var tr = title.rectTransform;
        tr.anchorMin = tr.anchorMax = new Vector2(0, 0.5f);
        tr.pivot = new Vector2(0, 0.5f);
        tr.sizeDelta = new Vector2(360, 54);
        tr.anchoredPosition = new Vector2(165, 34);
        title.alignment = TextAlignmentOptions.Left;

        var bg = Rect("BarBG", group);
        bg.anchorMin = bg.anchorMax = new Vector2(0, 0.5f);
        bg.pivot = new Vector2(0, 0.5f);
        bg.sizeDelta = new Vector2(360, 50);
        bg.anchoredPosition = new Vector2(162, -26);
        Img(bg, rounded, new Color(0.29f, 0.17f, 0.42f, 0.85f)).type = Image.Type.Sliced;

        var area = Rect("FillArea", bg);
        Stretch(area);
        area.offsetMin = new Vector2(5, 5); area.offsetMax = new Vector2(-5, -5);

        var ghost = Rect("Ghost", area);
        ghost.anchorMin = Vector2.zero; ghost.anchorMax = new Vector2(0.7f, 1);
        ghost.offsetMin = ghost.offsetMax = Vector2.zero;
        Img(ghost, rounded, new Color(1, 1, 1, 0.55f)).type = Image.Type.Sliced;

        var fill = Rect("Fill", area);
        fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0.7f, 1);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        Img(fill, rounded, color).type = Image.Type.Sliced;

        var value = Text(bg, "Value", "70", 32, Color.white);
        Stretch(value.rectTransform);
        value.rectTransform.offsetMax = new Vector2(-14, 0);
        value.alignment = TextAlignmentOptions.Right;
        value.fontStyle = FontStyles.Bold;

        var bar = group.gameObject.AddComponent<StatusBar>();
        bar.fill = fill;
        bar.ghostFill = ghost;
        bar.valueText = value;
        bar.format = format;
        return bar;
    }

    // ---------- helpers ----------
    static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }

    static void Panel(RectTransform rt)
    {
        var img = Img(rt, rounded, Hex("#FFF8E1"));
        img.type = Image.Type.Sliced;
        var o = rt.gameObject.AddComponent<Outline>();
        o.effectColor = Hex("#8E44AD");
        o.effectDistance = new Vector2(6, -6);
    }

    static Image Img(RectTransform rt, Sprite sprite, Color color)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static TextMeshProUGUI Text(RectTransform parent, string name, string text, float size, Color color)
    {
        var rt = Rect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    static Sprite LoadSprite(string path)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is Sprite s) return s;
        Debug.LogWarning("[Let it Go] Sprite not found: " + path);
        return null;
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        return c;
    }
}
