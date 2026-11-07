using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Core rules (from the Tutorial):
//   Work icons     -> +money, -happiness, -energy
//   Rest icons     -> +happiness (and/or +energy), -money
//   Happiness 0    -> stressed: the hand moves slower
//   Money below 0  -> bankrupt, game over
//   Energy 0       -> exhausted, game over ("do not let any bar run out")
//   Survive the whole day (dayLength seconds) -> win
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Stats")]
    public float maxEnergy = 100, maxMoney = 200, maxHappiness = 100;
    public float startEnergy = 70, startMoney = 60, startHappiness = 70;

    [Header("Drain per second (cost of living)")]
    public float energyDrain = 0.6f;
    public float moneyDrain = 1.2f;
    public float happinessDrain = 0.4f;

    [Header("Day")]
    public float dayLength = 120f;
    [Tooltip("Hand speed multiplier while stressed.")]
    public float stressedHandSpeed = 0.3f;
    public float sadThreshold = 30f;

    [Header("UI")]
    public StatusBar energyBar;
    public StatusBar moneyBar;
    public StatusBar happinessBar;
    public TMP_Text timerText;
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
    public bool Stressed => Happiness <= 0f;

    float timeLeft;
    bool over;

    void Awake()
    {
        Instance = this;
        Energy = startEnergy; Money = startMoney; Happiness = startHappiness;
        timeLeft = dayLength;
        RefreshBars(true);
        if (gameOverPanel) gameOverPanel.SetActive(false);
        if (stateText) stateText.text = "";
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
        Change(-energyDrain * dt, -moneyDrain * dt, -happinessDrain * dt);

        timeLeft -= dt;
        if (timerText)
        {
            int s = Mathf.CeilToInt(Mathf.Max(0, timeLeft));
            timerText.text = $"{s / 60}:{s % 60:00}";
        }
        if (timeLeft <= 0) End(true, "รอดมาได้อีกวัน!", "คุณรักษาสมดุลชีวิตได้ดีมาก");
    }

    // Called by IconSpawner when the player catches an icon.
    public void ApplyItem(IconSpawner.ItemDef item)
    {
        if (!Playing || over) return;
        Change(item.energy, item.money, item.happiness);
    }

    void Change(float dEnergy, float dMoney, float dHappiness)
    {
        Energy = Mathf.Clamp(Energy + dEnergy, 0, maxEnergy);
        Money = Mathf.Min(Money + dMoney, maxMoney);   // money can go below 0
        Happiness = Mathf.Clamp(Happiness + dHappiness, 0, maxHappiness);
        RefreshBars(false);

        var hand = HandTrackingClient.Instance;
        if (hand) hand.speedMultiplier = Stressed ? stressedHandSpeed : 1f;

        if (stateText) stateText.text = Stressed ? "<color=#E53935>เครียด! มือช้าลง</color>" : "";
        UpdateCharacter();

        if (Money < 0) End(false, "ล้มละลาย!", "เงินติดลบ... ลองทำงานให้มากขึ้นนะ");
        else if (Energy <= 0) End(false, "หมดแรง!", "พลังงานหมด... อย่าลืมพักผ่อนบ้าง");
    }

    void RefreshBars(bool instant)
    {
        if (energyBar) energyBar.Set(Energy, maxEnergy, instant);
        if (moneyBar) moneyBar.Set(Mathf.Max(0, Money), maxMoney, instant);
        if (happinessBar) happinessBar.Set(Happiness, maxHappiness, instant);
    }

    void UpdateCharacter()
    {
        if (!character) return;
        Sprite s = Stressed ? stressSprite : (Happiness < sadThreshold || Energy < sadThreshold) ? sadSprite : normalSprite;
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
        PlayerPrefs.SetFloat("result_time", dayLength - Mathf.Max(0, timeLeft));
        PlayerPrefs.SetFloat("result_money", Money);
        PlayerPrefs.Save();

        if (gameOverPanel)
        {
            gameOverPanel.SetActive(true);
            if (gameOverTitle) gameOverTitle.text = title;
            if (gameOverBody) gameOverBody.text = body;
        }
        Invoke(nameof(GoToResult), resultDelay);
    }

    void GoToResult()
    {
        if (Application.CanStreamedLevelBeLoaded(resultScene)) SceneManager.LoadScene(resultScene);
    }
}
