using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Hand cursor for menu scenes (Title, Tutorial): point at a UI button with the hand
// and make a fist to click it. Hovered buttons get their normal highlight.
// Starts the MediaPipe tracker if it is not running yet.
// Builds its own overlay canvas, so it only needs the two hand sprites.
public class HandUIPointer : MonoBehaviour
{
    public Sprite openSprite;
    public Sprite fistSprite;
    public Vector2 cursorSize = new Vector2(200, 200);
    [Tooltip("Ignore fists for this long after the scene opens, so a fist from the last scene does not click.")]
    public float startDelay = 0.6f;
    [Tooltip("Hide the cursor while this object is active (for example a panel with its own cursor).")]
    public TitleHandCheck hideWhileRunning;

    RectTransform canvasRt, cursor;
    Image cursorImg;
    GameObject hovered;
    PointerEventData ped;
    readonly List<RaycastResult> hits = new List<RaycastResult>();
    float age;

    void Start()
    {
        HandTrackingClient.Ensure();
        Build();
    }

    void Update()
    {
        age += Time.unscaledDeltaTime;
        var hand = HandTrackingClient.Instance;
        bool hide = hand == null || (hideWhileRunning != null && hideWhileRunning.IsRunning);
        cursor.gameObject.SetActive(!hide);
        if (hide) { SetHover(null); return; }

        Vector2 screen = Vector2.Scale(hand.ScreenPos01, new Vector2(Screen.width, Screen.height));
        cursor.anchoredPosition = Vector2.Scale(hand.ScreenPos01, canvasRt.rect.size);
        cursorImg.sprite = hand.IsFist ? fistSprite : openSprite;
        cursorImg.color = hand.Detected ? Color.white : new Color(1, 1, 1, 0.35f);

        // With the mouse fallback the normal UI input already clicks buttons.
        if (hand.UsingMouse || EventSystem.current == null) { SetHover(null); return; }

        GameObject target = hand.Detected ? ButtonUnder(screen) : null;
        SetHover(target);

        if (target != null && hand.FistDown && age >= startDelay)
        {
            ExecuteEvents.Execute(target, Ped(screen), ExecuteEvents.pointerClickHandler);
            punch = 1f;
        }
        punch = Mathf.MoveTowards(punch, 0f, Time.unscaledDeltaTime * 6f);
        cursor.localScale = Vector3.one * (1f - 0.15f * punch);
    }

    float punch;

    PointerEventData Ped(Vector2 screen)
    {
        if (ped == null) ped = new PointerEventData(EventSystem.current);
        ped.position = screen;
        return ped;
    }

    // Top-most interactable Button (or other click handler) under the hand.
    GameObject ButtonUnder(Vector2 screen)
    {
        hits.Clear();
        EventSystem.current.RaycastAll(Ped(screen), hits);
        foreach (var h in hits)
        {
            var go = ExecuteEvents.GetEventHandler<IPointerClickHandler>(h.gameObject);
            if (go == null) continue;
            var sel = go.GetComponent<Selectable>();
            if (sel != null && !sel.IsInteractable()) continue;
            return go;
        }
        return null;
    }

    void SetHover(GameObject go)
    {
        if (go == hovered) return;
        if (hovered != null) ExecuteEvents.Execute(hovered, Ped(Vector2.zero), ExecuteEvents.pointerExitHandler);
        hovered = go;
        if (hovered != null) ExecuteEvents.Execute(hovered, ped, ExecuteEvents.pointerEnterHandler);
    }

    void OnDisable() => SetHover(null);

    void Build()
    {
        var canvasGo = new GameObject("HandPointerCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRt = (RectTransform)canvasGo.transform;

        var go = new GameObject("HandPointer", typeof(RectTransform), typeof(Image));
        cursor = (RectTransform)go.transform;
        cursor.SetParent(canvasRt, false);
        cursor.anchorMin = cursor.anchorMax = Vector2.zero;
        cursor.sizeDelta = cursorSize;
        cursorImg = go.GetComponent<Image>();
        cursorImg.sprite = openSprite;
        cursorImg.preserveAspect = true;
        cursorImg.raycastTarget = false;   // never block the buttons below
    }
}
