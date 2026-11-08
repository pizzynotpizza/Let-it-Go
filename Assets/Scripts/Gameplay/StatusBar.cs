using TMPro;
using UnityEngine;

// One status bar: fill width follows value / max, with smooth motion and a flash on change.
public class StatusBar : MonoBehaviour
{
    public RectTransform fill;
    public RectTransform ghostFill;   // lags behind, shows how much was lost/gained
    public TMP_Text valueText;
    public string format = "{0:0}";
    public float lowThreshold = 0.25f;

    float shown;
    float ghost;
    float target;
    float shake;
    RectTransform rt;
    Vector2 basePos;

    void Awake()
    {
        rt = (RectTransform)transform;
        basePos = rt.anchoredPosition;
    }

    public void Set(float value, float max, bool instant = false)
    {
        float t = Mathf.Clamp01(value / max);
        if (t < target - 0.001f) shake = 0.3f;
        target = t;
        if (instant) shown = ghost = t;
        if (valueText != null) valueText.text = string.Format(format, value);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        shown = Mathf.Lerp(shown, target, 1f - Mathf.Exp(-12f * dt));
        ghost = Mathf.MoveTowards(ghost, target, dt * 0.35f);
        if (fill) fill.anchorMax = new Vector2(shown, 1);
        if (ghostFill) ghostFill.anchorMax = new Vector2(Mathf.Max(ghost, shown), 1);

        shake = Mathf.MoveTowards(shake, 0, dt);
        bool low = target <= lowThreshold;
        Vector2 off = Vector2.zero;
        if (shake > 0) off.x = Mathf.Sin(Time.unscaledTime * 60f) * 6f * shake;
        rt.anchoredPosition = basePos + off;
        rt.localScale = Vector3.one * (low ? 1f + 0.03f * Mathf.Sin(Time.unscaledTime * 10f) : 1f);
    }
}
