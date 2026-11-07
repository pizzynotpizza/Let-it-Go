using UnityEngine;
using UnityEngine.UI;

// UI hand icon that follows HandTrackingClient. Swaps Open Palm / Closed Fist sprite.
[RequireComponent(typeof(RectTransform))]
public class HandCursor : MonoBehaviour
{
    public Image image;
    public Sprite openSprite;
    public Sprite fistSprite;
    [Tooltip("Radius in canvas units used to catch icons.")]
    public float grabRadius = 110f;
    public Color lostColor = new Color(1, 1, 1, 0.35f);

    RectTransform rt;
    RectTransform parentRt;
    float punch;

    public Vector2 CanvasPosition => rt.anchoredPosition;

    void Awake()
    {
        rt = (RectTransform)transform;
        parentRt = (RectTransform)rt.parent;
        if (image == null) image = GetComponent<Image>();
        // Anchor at bottom-left of parent so anchoredPosition = canvas units from bottom-left.
        rt.anchorMin = rt.anchorMax = Vector2.zero;
    }

    void Update()
    {
        var hand = HandTrackingClient.Instance;
        if (hand == null) return;

        Vector2 size = parentRt.rect.size;
        rt.anchoredPosition = Vector2.Scale(hand.ScreenPos01, size);

        image.sprite = hand.IsFist ? fistSprite : openSprite;
        image.color = hand.Detected ? Color.white : lostColor;

        if (hand.FistDown) punch = 1f;
        punch = Mathf.MoveTowards(punch, 0f, Time.unscaledDeltaTime * 6f);
        rt.localScale = Vector3.one * (1f - 0.15f * punch);
    }
}
