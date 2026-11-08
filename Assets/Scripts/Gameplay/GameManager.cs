using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Rules (from the Tutorial pages, Assets/Background/Tutorial):
//   Play with one hand. Close the hand (fist) on an icon to pick that activity.
//   3 minutes of play = 1 month of life (30 days).
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
    public float startEnergy = 70, startMoney = 40, startHappiness = 60;

    [Header("Drain per second (cost of living)")]
    public float energyDrain = 0.75f;
    public float moneyDrain = 0.6f;
    public float happinessDrain = 0.5f;

    [Header("Month")]
    [Tooltip("Seconds of play for one month.")]
    public float monthLength = 180f;
    public int daysInMonth = 30;
    [Tooltip("Money multiplier for work while happiness is 0.")]
    [Range(0, 1)] public float burnoutPay = 0.3f;
    public float lowThreshold = 30f;
    [Tooltip("Drain multiplier at the end of the month (ramps up from 1). Bills grow, the body gets tired.")]
    public float endDrainMultiplier = 1.6f;

    [Header("UI")]
    public StatusBar energyBar;
    public StatusBar moneyBar;
    public StatusBar happinessBar;
    public TMP_Text timerText;
    public TMP_Text dayText;
    public TMP_Text stateText;
    public GameObject gameOverPanel;
    public TMP_Text gameOverTitle;
    public TMP_Text gameOverBody;
    public HandCalibration calibration;
    public IconSpawner spawner;

    [Header("Character")]
    public SpriteRenderer character;
    public Sprite normalSprite;
    public Sprite sadSprite;
    public Sprite stressSprite;

    [Header("Flow")]
    public string resultScene = "Result";
    public float resultDelay = 3f;

    public float Energy { get; private set; }
    public float Money { get; private set; }
    public float Happiness { get; private set; }
    public bool Playing { get; private set; }
    public bool BurnedOut => Happiness <= 0f;
    public float EnergyFraction => Energy / maxEnergy;

    float timeLeft;
    bool over;

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

    void Start()
    {
        if (calibration != null)
        {
            calibration.Finished += OnCalibrated;
            calibration.Begin();
        }
        else OnCalibrated();
    }

    void OnCalibrated()
    {
        if (over) return;
        Playing = true;
        if (spawner) spawner.enabled = true;
    }

    void Update()
    {
        bool paused = calibration != null && calibration.IsRunning;
        if (spawner) spawner.Paused = paused || !Playing;
        if (!Playing || over || paused) return;

        float dt = Time.deltaTime;
        float k = Mathf.Lerp(1f, endDrainMultiplier, 1f - Mathf.Max(0, timeLeft) / monthLength);
        Change(-energyDrain * k * dt, -moneyDrain * k * dt, -happinessDrain * k * dt);

        timeLeft -= dt;
        UpdateClock();
        if (timeLeft <= 0) End(true, "รอดมาได้ 1 เดือน!", "คุณรักษาสมดุลชีวิตได้ดีมาก");
    }

    void UpdateClock()
    {
        float left = Mathf.Max(0, timeLeft);
        if (timerText)
        {
            int s = Mathf.CeilToInt(left);
            timerText.text = $"{s / 60}:{s % 60:00}";
        }
        if (dayText)
        {
            int day = Mathf.Clamp(Mathf.FloorToInt((1f - left / monthLength) * daysInMonth) + 1, 1, daysInMonth);
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

        if (Money <= 0) End(false, "เงินหมด!", "Game Over... ลองทำงานให้มากขึ้นนะ");
        else if (Energy <= 0) End(false, "พลังงานหมด!", "Game Over... อย่าลืมกินข้าวและนอนพักบ้าง");
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
        Sprite s = BurnedOut || Energy < lowThreshold * 0.5f ? stressSprite
                 : (Happiness < lowThreshold || Energy < lowThreshold || Money < lowThreshold) ? sadSprite
                 : normalSprite;
        if (s && character.sprite != s) character.sprite = s;
    }

    void End(bool win, string title, string body)
    {
        if (over) return;
        over = true;
        Playing = false;
        if (spawner) spawner.Paused = true;

        PlayerPrefs.SetInt("result_win", win ? 1 : 0);
        PlayerPrefs.SetString("result_title", title);
        PlayerPrefs.SetFloat("result_time", monthLength - Mathf.Max(0, timeLeft));
        PlayerPrefs.SetFloat("result_money", Money);
        PlayerPrefs.Save();

        if (gameOverPanel)
        {
            gameOverPanel.SetActive(true);
            if (gameOverTitle)
            {
                gameOverTitle.text = title;
                gameOverTitle.color = win ? new Color(0.26f, 0.63f, 0.28f) : new Color(0.9f, 0.22f, 0.21f);
            }
            if (gameOverBody) gameOverBody.text = body;
        }
        Invoke(nameof(GoToResult), resultDelay);
    }

    void GoToResult()
    {
        if (Application.CanStreamedLevelBeLoaded(resultScene)) SceneManager.LoadScene(resultScene);
    }
}
