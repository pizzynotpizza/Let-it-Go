using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Two step calibration shown before play:
//   1. Hold an open palm  -> records the "open" value
//   2. Hold a fist        -> records the "fist" value
// Each step needs the hand visible for holdSeconds. Then onFinished fires.
// Press C during play to calibrate again. Press Space to skip (uses saved/default values).
public class HandCalibration : MonoBehaviour
{
    public GameObject panel;
    public TMP_Text titleText;
    public TMP_Text bodyText;
    public RectTransform progressFill;
    public Image previewIcon;
    public Sprite openSprite;
    public Sprite fistSprite;
    public float holdSeconds = 2f;
    public float warmupSeconds = 0.5f;
    [Tooltip("Open palm must reach at least this openness (typical open palm ~1.8-2.2).")]
    public float minOpenValue = 1.5f;
    [Tooltip("Fist must be at least this much below the recorded open value.")]
    public float minRange = 0.35f;

    public event Action Finished;
    public bool IsRunning { get; private set; }

    enum Step { Open, Fist }
    Step step;
    float timer;
    float warm;
    float sum;
    int count;
    float openResult;

    public void Begin()
    {
        IsRunning = true;
        panel.SetActive(true);
        SetStep(Step.Open);
    }

    void SetStep(Step s)
    {
        step = s;
        timer = 0; warm = 0; sum = 0; count = 0;
        titleText.text = s == Step.Open ? "ปรับเทียบมือ 1/2" : "ปรับเทียบมือ 2/2";
        bodyText.text = s == Step.Open
            ? "ยกมือให้กล้องเห็น แล้ว<color=#2E8FD6><b>แบมือ</b></color>ค้างไว้"
            : "ดีมาก! ตอนนี้<color=#E8457A><b>กำมือ</b></color>ค้างไว้";
        previewIcon.sprite = s == Step.Open ? openSprite : fistSprite;
        SetProgress(0);
    }

    void SetProgress(float t)
    {
        if (progressFill != null) progressFill.anchorMax = new Vector2(Mathf.Clamp01(t), 1);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (!IsRunning)
        {
            if (kb != null && kb.cKey.wasPressedThisFrame) Begin();
            return;
        }
        if (kb != null && kb.spaceKey.wasPressedThisFrame) { End(); return; }

        var hand = HandTrackingClient.Instance;
        if (hand == null) return;

        if (hand.UsingMouse)
        {
            bodyText.text = "ไม่พบข้อมูลจากกล้อง - ใช้เมาส์แทน\n(คลิกค้าง = กำมือ)\nกด <b>Space</b> เพื่อเริ่ม";
            SetProgress(0);
            return;
        }

        if (!hand.Detected)
        {
            timer = Mathf.Max(0, timer - Time.unscaledDeltaTime);
            SetProgress(timer / holdSeconds);
            return;
        }

        // Count only while the pose looks right, so calibration cannot finish
        // before the player actually opens / closes the hand.
        bool poseOk = step == Step.Open
            ? hand.RawOpenness >= minOpenValue
            : hand.RawOpenness <= openResult - minRange;
        if (!poseOk)
        {
            warm = 0;
            timer = Mathf.Max(0, timer - Time.unscaledDeltaTime);
            SetProgress(timer / holdSeconds);
            return;
        }

        // Ignore the first moment so the player has time to settle the pose.
        warm += Time.unscaledDeltaTime;
        if (warm < warmupSeconds) return;

        timer += Time.unscaledDeltaTime;
        sum += hand.RawOpenness;
        count++;
        SetProgress(timer / holdSeconds);

        if (timer < holdSeconds) return;

        float avg = sum / Mathf.Max(1, count);
        if (step == Step.Open)
        {
            openResult = avg;
            SetStep(Step.Fist);
        }
        else
        {
            hand.SaveCalibration(openResult, avg);
            End();
        }
    }

    void End()
    {
        IsRunning = false;
        panel.SetActive(false);
        Finished?.Invoke();
    }
}
