using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Short hand check shown after the Start button on the Title screen.
//   Step 1: move the hand around (fills a bar, records the open-hand value).
//   Step 2: make a fist to continue (records the fist value, then loads Tutorial).
// One pass only, no extra screens. Space skips (keeps the saved values).
// The UI is built at runtime on the canvas this component sits on.
public class TitleHandCheck : MonoBehaviour
{
    public string nextScene = "Tutorial";
    public TMP_FontAsset font;
    public Sprite openSprite;
    public Sprite fistSprite;

    [Tooltip("How far the hand must travel, in screen widths, to finish step 1.")]
    public float moveDistance = 2.5f;
    [Tooltip("Seconds the fist must be held to continue.")]
    public float fistHold = 0.3f;

    public bool IsRunning { get; private set; }

    enum Step { Move, Fist, Done }
    Step step;
    RectTransform root, cursor, fill;
    Image cursorImg;
    TMP_Text title, body;
    Vector2 lastPos;
    float moved, fistTimer, openSum, fistSum;
    int openCount, fistCount;

    static readonly Color Ink = new Color(0.29f, 0.17f, 0.42f);

    public void Begin()
    {
        if (IsRunning) return;
        HandTrackingClient.Ensure();
        if (root == null) Build();
        root.gameObject.SetActive(true);
        root.SetAsLastSibling();
        IsRunning = true;
        SetStep(Step.Move);
    }

    void SetStep(Step s)
    {
        step = s;
        moved = 0; fistTimer = 0;
        var hand = HandTrackingClient.Instance;
        if (hand) lastPos = hand.ScreenPos01;
        if (s == Step.Move)
        {
            title.text = "เตรียมมือให้พร้อม";
            body.text = "ยกมือให้กล้องเห็น แล้ว<color=#2E8FD6><b>ขยับมือไปมา</b></color>";
        }
        else if (s == Step.Fist)
        {
            title.text = "ดีมาก!";
            body.text = "<color=#E8457A><b>กำมือ</b></color>เพื่อไปต่อ";
        }
        SetFill(0);
    }

    void Update()
    {
        if (!IsRunning) return;
        var hand = HandTrackingClient.Instance;
        var kb = Keyboard.current;
        if (kb != null && kb.spaceKey.wasPressedThisFrame) { Finish(false); return; }
        if (hand == null) return;

        // Cursor follows the hand so the player sees the tracking working.
        cursor.anchoredPosition = Vector2.Scale(hand.ScreenPos01, root.rect.size);
        cursorImg.sprite = hand.IsFist ? fistSprite : openSprite;
        cursorImg.color = hand.Detected ? Color.white : new Color(1, 1, 1, 0.35f);

        if (hand.UsingMouse && step == Step.Move)
            body.text = "ยังไม่พบกล้อง - ใช้เมาส์แทน\nขยับเมาส์ไปมา (คลิกค้าง = กำมือ)";

        float dt = Time.unscaledDeltaTime;
        if (step == Step.Move)
        {
            if (hand.Detected && !hand.IsFist)
            {
                moved += (hand.ScreenPos01 - lastPos).magnitude;
                openSum += hand.RawOpenness; openCount++;
            }
            lastPos = hand.ScreenPos01;
            SetFill(moved / moveDistance);
            if (moved >= moveDistance) SetStep(Step.Fist);
        }
        else if (step == Step.Fist)
        {
            if (hand.Detected && hand.IsFist)
            {
                fistTimer += dt;
                fistSum += hand.RawOpenness; fistCount++;
            }
            else fistTimer = Mathf.Max(0, fistTimer - dt);
            SetFill(fistTimer / fistHold);
            if (fistTimer >= fistHold) Finish(!hand.UsingMouse);
        }
    }

    void Finish(bool save)
    {
        step = Step.Done;
        IsRunning = false;
        var hand = HandTrackingClient.Instance;
        if (save && hand && openCount > 0 && fistCount > 0)
            hand.SaveCalibration(openSum / openCount, fistSum / fistCount);
        SceneManager.LoadScene(nextScene);
    }

    void SetFill(float t)
    {
        fill.anchorMax = new Vector2(Mathf.Clamp01(t), 1);
    }

    // ---------------------------------------------------------------- UI
    void Build()
    {
        root = NewRect("HandCheck", (RectTransform)transform);
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        root.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.05f, 0.2f, 0.55f);

        var frame = NewRect("Frame", root);
        frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
        frame.sizeDelta = new Vector2(1000, 420);
        frame.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.95f);

        title = NewText(frame, "Title", 60, new Vector2(0, 120), new Vector2(900, 80));
        body = NewText(frame, "Body", 44, new Vector2(0, 10), new Vector2(900, 130));
        var skip = NewText(frame, "Skip", 26, new Vector2(0, -170), new Vector2(900, 40));
        skip.text = "กด Space เพื่อข้าม";
        skip.color = new Color(Ink.r, Ink.g, Ink.b, 0.6f);

        var bar = NewRect("Progress", frame);
        bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0.5f);
        bar.anchoredPosition = new Vector2(0, -100);
        bar.sizeDelta = new Vector2(700, 36);
        bar.gameObject.AddComponent<Image>().color = new Color(Ink.r, Ink.g, Ink.b, 0.2f);
        fill = NewRect("Fill", bar);
        fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0, 1);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        fill.gameObject.AddComponent<Image>().color = new Color(0.42f, 0.8f, 0.25f);

        cursor = NewRect("HandCursor", root);
        cursor.anchorMin = cursor.anchorMax = Vector2.zero;
        cursor.sizeDelta = new Vector2(220, 220);
        cursorImg = cursor.gameObject.AddComponent<Image>();
        cursorImg.sprite = openSprite;
        cursorImg.preserveAspect = true;
        cursorImg.raycastTarget = false;
    }

    static RectTransform NewRect(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    TMP_Text NewText(RectTransform parent, string name, float size, Vector2 pos, Vector2 box)
    {
        var rt = NewRect(name, parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font) t.font = font;
        t.fontSize = size;
        t.color = Ink;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }
}
