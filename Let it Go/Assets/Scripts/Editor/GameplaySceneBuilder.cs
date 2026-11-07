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
    static readonly Color EnergyColor = Hex("#2E8FD6");
    static readonly Color MoneyColor = Hex("#D99A00");
    static readonly Color HappyColor = Hex("#E8457A");
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
        top.sizeDelta = new Vector2(-40, 150);
        top.anchoredPosition = new Vector2(0, -16);
        var topBg = Img(top, rounded, new Color(1, 1, 1, 0.82f));
        topBg.type = Image.Type.Sliced;
        top.gameObject.AddComponent<Outline>().effectColor = new Color(0.55f, 0.35f, 0.8f, 0.6f);

        var energyBar = MakeBar(top, "EnergyBar", 30, "พลังงาน", EnergyColor, LoadSprite("Assets/Icon/Status/Energy.png"), "{0:0}");
        var moneyBar = MakeBar(top, "MoneyBar", 580, "เงิน", MoneyColor, LoadSprite("Assets/Icon/Status/Money.png"), "฿{0:0}");
        var happyBar = MakeBar(top, "HappinessBar", 1130, "ความสุข", HappyColor, LoadSprite("Assets/Icon/Status/Happy.png"), "{0:0}");

        // Timer (right side of top bar)
        var timerBox = Rect("Timer", top);
        timerBox.anchorMin = timerBox.anchorMax = new Vector2(1, 0.5f);
        timerBox.pivot = new Vector2(1, 0.5f);
        timerBox.sizeDelta = new Vector2(180, 120);
        timerBox.anchoredPosition = new Vector2(-24, 0);
        var tbImg = Img(timerBox, rounded, Hex("#4A2C6B"));
        tbImg.type = Image.Type.Sliced;
        var timerLabel = Text(timerBox, "Label", "เวลาที่เหลือ", 24, Color.white);
        Place(timerLabel.rectTransform, new Vector2(0, 32), new Vector2(170, 36));
        var timerText = Text(timerBox, "Value", "2:00", 54, Hex("#FFD54F"));
        Place(timerText.rectTransform, new Vector2(0, -14), new Vector2(170, 70));

        // State text (stressed warning) under the top bar
        var stateText = Text(root, "StateText", "", 44, Color.white);
        var st = stateText.rectTransform;
        st.anchorMin = st.anchorMax = new Vector2(0.5f, 1);
        st.sizeDelta = new Vector2(900, 70);
        st.anchoredPosition = new Vector2(0, -210);
        stateText.outlineWidth = 0.25f; stateText.outlineColor = Color.white;

        // Controls hint, bottom left
        var hint = Text(root, "Hint", "กำมือเพื่อคว้าไอคอน  •  C = ปรับเทียบมือใหม่", 26, Ink);
        var hr = hint.rectTransform;
        hr.anchorMin = hr.anchorMax = new Vector2(0, 0);
        hr.pivot = new Vector2(0, 0);
        hr.sizeDelta = new Vector2(800, 50);
        hr.anchoredPosition = new Vector2(30, 20);
        hint.alignment = TextAlignmentOptions.Left;
        hint.outlineWidth = 0.3f; hint.outlineColor = Color.white;

        // ---------- Hand cursor ----------
        var openSprite = LoadSprite("Assets/Icon/Hand/Open Palm.png");
        var fistSprite = LoadSprite("Assets/Icon/Hand/Closed Fist.png");
        var cursorRt = Rect("HandCursor", play);
        cursorRt.sizeDelta = new Vector2(180, 180);
        var cursorImg = Img(cursorRt, openSprite, Color.white);
        cursorImg.preserveAspect = true;
        var cursor = cursorRt.gameObject.AddComponent<HandCursor>();
        cursor.image = cursorImg;
        cursor.openSprite = openSprite;
        cursor.fistSprite = fistSprite;

        // ---------- Calibration panel ----------
        var frame = LoadSprite("Assets/Icon/Tutorial.png");
        var calPanel = Rect("CalibrationPanel", root);
        Stretch(calPanel);
        Img(calPanel, null, new Color(0.1f, 0.05f, 0.2f, 0.6f));
        var calFrame = Rect("Frame", calPanel);
        calFrame.sizeDelta = new Vector2(1300, 732);
        Img(calFrame, frame, Color.white).preserveAspect = true;
        var calTitle = Text(calFrame, "Title", "ปรับเทียบมือ", 52, Ink);
        Place(calTitle.rectTransform, new Vector2(-90, 70), new Vector2(800, 70));
        var calBody = Text(calFrame, "Body", "", 40, Ink);
        Place(calBody.rectTransform, new Vector2(-90, -40), new Vector2(800, 140));
        var preview = Rect("HandPreview", calFrame);
        Place(preview, new Vector2(400, -20), new Vector2(200, 200));
        var previewImg = Img(preview, openSprite, Color.white);
        previewImg.preserveAspect = true;
        var progBg = Rect("Progress", calFrame);
        Place(progBg, new Vector2(-90, -160), new Vector2(700, 36));
        Img(progBg, rounded, new Color(0.29f, 0.17f, 0.42f, 0.25f)).type = Image.Type.Sliced;
        var progFill = Rect("Fill", progBg);
        progFill.anchorMin = Vector2.zero; progFill.anchorMax = new Vector2(0, 1);
        progFill.offsetMin = progFill.offsetMax = Vector2.zero;
        Img(progFill, rounded, Hex("#6BCB3F")).type = Image.Type.Sliced;
        var skip = Text(calFrame, "SkipHint", "กด Space เพื่อข้าม (ใช้ค่าเดิม)", 26, new Color(0.29f, 0.17f, 0.42f, 0.7f));
        Place(skip.rectTransform, new Vector2(0, -220), new Vector2(800, 40));

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
        overFrame.sizeDelta = new Vector2(1100, 620);
        Img(overFrame, frame, Color.white).preserveAspect = true;
        var overTitle = Text(overFrame, "Title", "จบเกม", 72, Hex("#E53935"));
        Place(overTitle.rectTransform, new Vector2(0, 20), new Vector2(800, 100));
        var overBody = Text(overFrame, "Body", "", 40, Ink);
        Place(overBody.rectTransform, new Vector2(0, -100), new Vector2(760, 110));
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
            bg.GetComponent<SpriteRenderer>().sortingOrder = -10;
        }
    }

    static IconSpawner.ItemDef[] BuildItems()
    {
        IconSpawner.ItemDef W(string label, string file, float money, float happy, float energy, float weight = 1) =>
            new IconSpawner.ItemDef { label = label, kind = IconSpawner.Kind.Work, sprite = LoadSprite("Assets/Icon/Work/" + file + ".png"), money = money, happiness = happy, energy = energy, weight = weight };
        IconSpawner.ItemDef H(string label, string file, float money, float happy, float energy, float weight = 1) =>
            new IconSpawner.ItemDef { label = label, kind = IconSpawner.Kind.Happiness, sprite = LoadSprite("Assets/Icon/Happiness/" + file + ".png"), money = money, happiness = happy, energy = energy, weight = weight };

        return new[]
        {
            W("ทำงาน",      "Work",    20, -12, -12, 0.8f),
            W("เขียนโค้ด",   "Coding",  15,  -8, -10),
            W("ประชุม",     "Meeting", 12, -10,  -6),
            W("ตอบอีเมล",   "Email",    8,  -4,  -5, 1.2f),
            W("เรียน",      "Study",    6,  -6,  -8),
            H("กินข้าว",     "Food",    -8,   8,  12, 1.2f),
            H("นอนหลับ",    "Sleep",   -3,   5,  25),
            H("ออกกำลังกาย", "Exercise", -4,  14,  -4),
            H("เล่นเกม",     "Game",   -12,  15,  -2),
            H("ฟังเพลง",     "Music",   -5,  10,   0, 1.2f),
        };
    }

    static StatusBar MakeBar(RectTransform parent, string name, float x, string label, Color color, Sprite icon, string format)
    {
        var group = Rect(name, parent);
        group.anchorMin = group.anchorMax = new Vector2(0, 0.5f);
        group.pivot = new Vector2(0, 0.5f);
        group.sizeDelta = new Vector2(520, 120);
        group.anchoredPosition = new Vector2(x, 0);

        var iconRt = Rect("Icon", group);
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0, 0.5f);
        iconRt.pivot = new Vector2(0, 0.5f);
        iconRt.sizeDelta = new Vector2(110, 110);
        Img(iconRt, icon, Color.white).preserveAspect = true;

        var title = Text(group, "Label", $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{label}</color>", 34, color);
        var tr = title.rectTransform;
        tr.anchorMin = tr.anchorMax = new Vector2(0, 0.5f);
        tr.pivot = new Vector2(0, 0.5f);
        tr.sizeDelta = new Vector2(380, 46);
        tr.anchoredPosition = new Vector2(125, 28);
        title.alignment = TextAlignmentOptions.Left;

        var bg = Rect("BarBG", group);
        bg.anchorMin = bg.anchorMax = new Vector2(0, 0.5f);
        bg.pivot = new Vector2(0, 0.5f);
        bg.sizeDelta = new Vector2(380, 44);
        bg.anchoredPosition = new Vector2(122, -22);
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

        var value = Text(bg, "Value", "70", 28, Color.white);
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
