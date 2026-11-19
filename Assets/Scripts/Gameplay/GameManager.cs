using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Rules (from the Tutorial pages, Assets/Art/Backgrounds/Tutorial):
//   Play with one hand. Close the hand (fist) on an icon to pick that activity.
//   2 minutes of play = 1 month of life (30 days).
//   Work icons     (Work, Coding, Meeting, Email) -> +money,  -happiness, -energy
//   Relax icons    (Exercise, Game, Music)        -> +happiness, -money, -energy
//   Recover icons  (Food, Sleep)                  -> +energy (food costs a little money)
//   Happiness 0    -> burned out: work pays less money
//   Energy 0 or money 0 -> game over at once
//   Survive the whole month -> win
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Stats")]
    public float maxEnergy = 100, maxMoney = 100, maxHappiness = 100;
    public float startEnergy = 70, startMoney = 50, startHappiness = 60;

    [Header("Drain per second (cost of living)")]
    // Tuned with a play simulation (2026-11-09, 2-minute month): a player who catches
    // 50 / 70 / 85 % of the useful icons wins about 50 / 67 / 77 % of runs.
    public float energyDrain = 0.8f;
    public float moneyDrain = 0.6f;
    public float happinessDrain = 0.5f;

    [Header("Month")]
    [Tooltip("Seconds of play for one month.")]
    public float monthLength = 120f;
    public int daysInMonth = 30;
    [Tooltip("Money multiplier for work while happiness is 0.")]
    [Range(0, 1)] public float burnoutPay = 0.5f;
    public float lowThreshold = 30f;
    [Tooltip("Drain multiplier at the end of the month (ramps up from 1). Bills grow, the body gets tired.")]
    public float endDrainMultiplier = 1.4f;

    [Header("UI")]
    public StatusBar energyBar;
    public StatusBar moneyBar;
    public StatusBar happinessBar;
    public TMP_Text timerText;
    public TMP_Text dayText;
    public TMP_Text stateText;
    [Tooltip("Dim layer + popup root. Shown when the run ends.")]
    public GameObject gameOverPanel;
    public RectTransform popup;          // scaled in by the pop animation
    public UnityEngine.UI.Image popupImage;
    public Sprite gameOverMoneySprite;
    public Sprite gameOverHealthSprite;
    public GameObject retryButton;       // shown on game over
    public GameObject homeButton;        // shown on game over, next to Retry
    public HandCursor handCursor;
    [Tooltip("Full-screen victory art (survived 1 month) with its own Home button.")]
    public GameObject victoryPanel;
    public RectTransform victoryArt;
    public RectTransform victoryHomeButton;
    public IconSpawner spawner;

    [Header("Character")]
    public SpriteRenderer character;
    public Sprite normalSprite;
    public Sprite sadSprite;
    public Sprite stressSprite;

    [Header("Flow")]
    public string gameplayScene = "Gameplay";
    public string homeScene = "Title";
    public float popupDuration = 0.6f;

    public float Energy { get; private set; }
    public float Money { get; private set; }
    public float Happiness { get; private set; }
    public bool Playing { get; private set; }
    public bool BurnedOut => Happiness <= 0f;
    public float EnergyFraction => Energy / maxEnergy;

    [Tooltip("Seconds left when the hurry music starts.")]
    public float hurryTime = 20f;

    float timeLeft;
    bool over;
    bool hurryPlaying;

    void Awake()
    {
        Instance = this;
        Energy = startEnergy; Money = startMoney; Happiness = startHappiness;
        timeLeft = monthLength;
        RefreshBars(true);
        UpdateClock();
        if (gameOverPanel) gameOverPanel.SetActive(false);
        if (stateText) stateText.text = "";
        var hand = HandTrackingClient.Instance;
        if (hand) hand.speedMultiplier = 1f;
    }

    // Hand check now runs on the Title screen, so play starts at once.
    void Start()
    {
        if (over) return;
        Playing = true;
        if (spawner) spawner.enabled = true;
    }

    void Update()
    {
        if (spawner) spawner.Paused = !Playing;
        if (!Playing || over) return;

        float dt = Time.deltaTime;
        float k = Mathf.Lerp(1f, endDrainMultiplier, 1f - Mathf.Max(0, timeLeft) / monthLength);
        Change(-energyDrain * k * dt, -moneyDrain * k * dt, -happinessDrain * k * dt);

        timeLeft -= dt;
        UpdateClock();
        if (!hurryPlaying && timeLeft <= hurryTime)
        {
            hurryPlaying = true;
            MusicPlayer.PlayTrack(MusicPlayer.Hurry);
        }
        if (timeLeft <= 0) End(EndKind.Victory);
    }

    // Texts change once a second / once a day, so build the strings only then.
    int shownSeconds = -1, shownDay = -1;

    void UpdateClock()
    {
        float left = Mathf.Max(0, timeLeft);
        int s = Mathf.CeilToInt(left);
        if (timerText && s != shownSeconds)
        {
            shownSeconds = s;
            timerText.text = $"{s / 60}:{s % 60:00}";
        }
        int day = Mathf.Clamp(Mathf.FloorToInt((1f - left / monthLength) * daysInMonth) + 1, 1, daysInMonth);
        if (dayText && day != shownDay)
        {
            shownDay = day;
            dayText.text = $"วันที่ {day}/{daysInMonth}";
        }
    }

    // Called by IconSpawner when the player picks an icon. Returns the change actually applied
    // (x = energy, y = money, z = happiness) so the popup can show it.
    public Vector3 ApplyItem(IconSpawner.ItemDef item)
    {
        if (!Playing || over) return Vector3.zero;
        float money = item.money;
        if (item.kind == IconSpawner.Kind.Work && BurnedOut && money > 0) money *= burnoutPay;
        var d = new Vector3(item.energy, money, item.happiness);
        Change(d.x, d.y, d.z);
        return d;
    }

    void Change(float dEnergy, float dMoney, float dHappiness)
    {
        Energy = Mathf.Clamp(Energy + dEnergy, 0, maxEnergy);
        Money = Mathf.Clamp(Money + dMoney, 0, maxMoney);
        Happiness = Mathf.Clamp(Happiness + dHappiness, 0, maxHappiness);
        RefreshBars(false);

        if (stateText)
        {
            if (BurnedOut) stateText.text = "<color=#E53935>หมดไฟ! ทำงานได้เงินน้อยลง</color>";
            else if (Energy < lowThreshold) stateText.text = "<color=#E53935>พลังงานใกล้หมด! กินข้าวหรือนอนพัก</color>";
            else if (Money < lowThreshold * 0.6f) stateText.text = "<color=#E53935>เงินใกล้หมด! ไปทำงานกันเถอะ</color>";
            else stateText.text = "";
        }
        UpdateCharacter();

        if (Money <= 0) End(EndKind.NoMoney);
        else if (Energy <= 0) End(EndKind.NoHealth);
    }

    void RefreshBars(bool instant)
    {
        if (energyBar) energyBar.Set(Energy, maxEnergy, instant);
        if (moneyBar) moneyBar.Set(Money, maxMoney, instant);
        if (happinessBar) happinessBar.Set(Happiness, maxHappiness, instant);
    }

    void UpdateCharacter()
    {
        if (!character) return;
        bool stressed = BurnedOut || Energy < lowThreshold * 0.5f;
        bool sad = !stressed && (Happiness < lowThreshold || Energy < lowThreshold || Money < lowThreshold);
        shakeAmount = stressed ? stressShake : sad ? sadShake : 0f;
        Sprite s = stressed ? stressSprite : sad ? sadSprite : normalSprite;
        if (s && character.sprite != s) character.sprite = s;
    }

    [Header("Character shake")]
    [Tooltip("Shake distance (world units) while sad.")]
    public float sadShake = 0.03f;
    [Tooltip("Shake distance (world units) while stressed.")]
    public float stressShake = 0.1f;
    public float shakeSpeed = 25f;
    float shakeAmount;
    Vector3 characterBase;
    bool characterBaseSet;

    void LateUpdate()
    {
        if (!character) return;
        if (!characterBaseSet) { characterBase = character.transform.localPosition; characterBaseSet = true; }
        float a = Playing && !over ? shakeAmount : 0f;
        Vector3 offset = Vector3.zero;
        if (a > 0f)
        {
            float t = Time.time * shakeSpeed;
            offset = new Vector3((Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f * a,
                                 (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f * a, 0f);
        }
        var pos = characterBase + offset;
        if (character.transform.localPosition != pos) character.transform.localPosition = pos;
    }

    enum EndKind { Victory, NoMoney, NoHealth }

    void End(EndKind kind)
    {
        if (over) return;
        over = true;
        Playing = false;
        HandTrackingClient.ResetHandLock();  // next game picks its own Hand 1
        if (spawner) spawner.Paused = true;
        if (stateText) stateText.text = "";
        MusicPlayer.PlayTrack(kind == EndKind.Victory ? MusicPlayer.Victory : MusicPlayer.GameOver);

        if (kind == EndKind.Victory && victoryPanel)
        {
            victoryPanel.SetActive(true);
            StartCoroutine(VictoryAnim());
        }
        else if (gameOverPanel)
        {
            gameOverPanel.SetActive(true);
            if (popupImage)
                popupImage.sprite = kind == EndKind.NoMoney ? gameOverMoneySprite : gameOverHealthSprite;
            if (retryButton) retryButton.SetActive(true);
            if (homeButton) homeButton.SetActive(true);
            StartCoroutine(PopIn());
        }
        EnableMenuPointer();
    }

    // Scale from 0 with a small overshoot, dim layer fades in.
    System.Collections.IEnumerator PopIn()
    {
        var dim = gameOverPanel.GetComponent<UnityEngine.UI.Image>();
        Color dimColor = dim ? dim.color : Color.clear;
        for (float t = 0; t < popupDuration; t += Time.unscaledDeltaTime)
        {
            float x = t / popupDuration;
            float c1 = 1.70158f, c3 = c1 + 1f;   // easeOutBack
            float s = 1f + c3 * Mathf.Pow(x - 1f, 3) + c1 * Mathf.Pow(x - 1f, 2);
            if (popup) popup.localScale = Vector3.one * Mathf.Max(0, s);
            if (dim) dim.color = new Color(dimColor.r, dimColor.g, dimColor.b, dimColor.a * Mathf.Clamp01(x * 2f));
            yield return null;
        }
        if (popup) popup.localScale = Vector3.one;
        if (dim) dim.color = dimColor;
    }

    // Victory: art pops in once (scale up with a small overshoot), then stays still.
    System.Collections.IEnumerator VictoryAnim()
    {
        var group = victoryPanel.GetComponent<CanvasGroup>();
        for (float t = 0; t < popupDuration; t += Time.unscaledDeltaTime)
        {
            float x = t / popupDuration;
            float c1 = 1.70158f, c3 = c1 + 1f;   // easeOutBack
            float e = 1f + c3 * Mathf.Pow(x - 1f, 3) + c1 * Mathf.Pow(x - 1f, 2);
            if (group) group.alpha = Mathf.Clamp01(x * 3f);
            victoryPanel.transform.localScale = Vector3.one * Mathf.Max(0, e);
            yield return null;
        }
        if (group) group.alpha = 1f;
        victoryPanel.transform.localScale = Vector3.one;
    }

    // Gameplay uses the grab cursor; for the popup buttons switch to the menu pointer
    // (point + fist clicks a UI button).
    void EnableMenuPointer()
    {
        if (handCursor) handCursor.gameObject.SetActive(false);
        var pointer = gameObject.AddComponent<HandUIPointer>();
        if (handCursor)
        {
            pointer.openSprite = handCursor.openSprite;
            pointer.fistSprite = handCursor.fistSprite;
        }
        pointer.cursorSize = new Vector2(230, 230);
        pointer.startDelay = popupDuration + 0.4f;
    }

    // Retry: straight back into play, no tutorial.
    public void Retry() => SceneManager.LoadScene(gameplayScene);
    public void GoHome() => SceneManager.LoadScene(homeScene);
}
