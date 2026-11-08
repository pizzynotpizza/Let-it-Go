using UnityEngine;

// Makes a UI logo bob up and down, tilt side to side and pulse in size.
public class LogoFloat : MonoBehaviour
{
    public float floatHeight = 20f;
    public float floatSpeed = 1.5f;
    public float tiltAngle = 3f;
    public float tiltSpeed = 1.2f;
    public float pulseScale = 0.04f;
    public float pulseSpeed = 2f;

    RectTransform rt;
    Vector2 startPos;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        startPos = rt.anchoredPosition;
    }

    void Update()
    {
        float t = Time.unscaledTime;
        rt.anchoredPosition = startPos + Vector2.up * Mathf.Sin(t * floatSpeed) * floatHeight;
        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * tiltSpeed) * tiltAngle);
        rt.localScale = Vector3.one * (1f + Mathf.Sin(t * pulseSpeed) * pulseScale);
    }
}
