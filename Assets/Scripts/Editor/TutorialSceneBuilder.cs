using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Menu: Let it Go > Build Tutorial
// Sets up the open Tutorial scene: full-screen page image (Assets/Background/Tutorial/1..9.jpg)
// plus one Next/Start button placed where the button art sits in the design.
public static class TutorialSceneBuilder
{
    [MenuItem("Let it Go/Build Tutorial")]
    public static void Build()
    {
        var pages = new Sprite[9];
        for (int i = 0; i < pages.Length; i++)
        {
            string path = $"Assets/Background/Tutorial/{i + 1}.jpg";
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti != null && (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled = false;
                ti.SaveAndReimport();
            }
            pages[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (pages[i] == null) Debug.LogWarning("[Let it Go] Missing tutorial page " + path);
        }

        var canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) { Debug.LogError("[Let it Go] No Canvas in the open scene."); return; }
        var root = (RectTransform)canvas.transform;

        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler)
        {
            Undo.RecordObject(scaler, "Build Tutorial");
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        // Background: full-screen page image.
        var bgT = root.Find("Background");
        Image bg;
        if (bgT == null)
        {
            var go = new GameObject("Background", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "Build Tutorial");
            go.transform.SetParent(root, false);
            bgT = go.transform;
        }
        bg = bgT.GetComponent<Image>();
        Undo.RecordObject(bg, "Build Tutorial");
        var fitter = bgT.GetComponent<AspectRatioFitter>();
        if (fitter) Undo.DestroyObjectImmediate(fitter);
        var bgRt = (RectTransform)bgT;
        Undo.RecordObject(bgRt, "Build Tutorial");
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        bg.sprite = pages[0];
        bg.color = Color.white;
        bg.preserveAspect = false;
        bg.raycastTarget = false;
        bgT.SetAsFirstSibling();

        // Old layout parts: text was drawn on top of a blank frame. The page images now hold the text.
        foreach (var n in new[] { "Tutorial Panel", "Tutorial Text", "Stats Page" })
        {
            var t = root.Find(n);
            if (t == null) continue;
            Undo.RecordObject(t.gameObject, "Build Tutorial");
            t.gameObject.SetActive(false);
            var oldPager = t.GetComponent<TutorialPager>();
            if (oldPager) Undo.DestroyObjectImmediate(oldPager);
        }
        var spriteObj = GameObject.Find("Tutorial_0");
        if (spriteObj) { Undo.RecordObject(spriteObj, "Build Tutorial"); spriteObj.SetActive(false); }

        // Button
        var btnT = root.Find("Next Button");
        if (btnT == null) { Debug.LogError("[Let it Go] 'Next Button' not found under the Canvas."); return; }
        btnT.SetAsLastSibling();
        var btnImg = btnT.GetComponent<Image>();
        Undo.RecordObject(btnImg, "Build Tutorial");
        btnImg.preserveAspect = true;
        btnImg.raycastTarget = true;

        // Pager lives on the canvas (always active).
        var pager = canvas.GetComponent<TutorialPager>();
        if (pager == null) pager = Undo.AddComponent<TutorialPager>(canvas.gameObject);
        Undo.RecordObject(pager, "Build Tutorial");
        pager.background = bg;
        pager.pages = pages;
        pager.nextButton = btnT.GetComponent<Button>();
        pager.nextSprite = LoadSprite("Assets/Icon/Button/Next Button.png");
        pager.startSprite = LoadSprite("Assets/Icon/Button/Start Button.png");
        btnImg.sprite = pager.nextSprite;

        var btnRt = (RectTransform)btnT;
        Undo.RecordObject(btnRt, "Build Tutorial");
        btnRt.anchorMin = btnRt.anchorMax = btnRt.pivot = new Vector2(0.5f, 0.5f);
        btnRt.localScale = Vector3.one;
        btnRt.localRotation = Quaternion.identity;
        btnRt.anchoredPosition = pager.nextPos;
        btnRt.sizeDelta = pager.nextSize;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[Let it Go] Tutorial built.");
    }

    static Sprite LoadSprite(string path)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is Sprite s) return s;
        Debug.LogWarning("[Let it Go] Sprite not found: " + path);
        return null;
    }
}
