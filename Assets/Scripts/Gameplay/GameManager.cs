using TMPro; // ใช้ TextMeshPro สำหรับข้อความเวลา/วัน/สถานะ
using UnityEngine; // ใช้คลาสหลักของ Unity
using UnityEngine.SceneManagement; // ใช้โหลดฉาก (Retry / กลับหน้าแรก)

// กติกาเกม (ตามหน้า Tutorial ใน Assets/Art/Backgrounds/Tutorial):
//   เล่นด้วยมือเดียว กำมือที่ไอคอนเพื่อเลือกกิจกรรมนั้น
//   เล่น 2 นาที = ใช้ชีวิต 1 เดือน (30 วัน)
//   ไอคอนงาน     (Work, Coding, Meeting, Email) -> เงินเพิ่ม, ความสุขลด, พลังงานลด
//   ไอคอนพักผ่อน  (Exercise, Game, Music)        -> ความสุขเพิ่ม, เงินลด, พลังงานลด
//   ไอคอนฟื้นพลัง (Food, Sleep)                  -> พลังงานเพิ่ม (กินข้าวเสียเงินนิดหน่อย)
//   ความสุขเหลือ 0 -> หมดไฟ: ทำงานได้เงินน้อยลง
//   พลังงาน 0 หรือ เงิน 0 -> แพ้ทันที
//   อยู่รอดครบเดือน -> ชนะ
public class GameManager : MonoBehaviour // คลาสคุมกติกาและสถานะของเกม
{
    public static GameManager Instance { get; private set; } // singleton ให้สคริปต์อื่นเรียกใช้

    // ---- ค่าสถานะ: ค่าสูงสุด และค่าเริ่มต้นของ พลังงาน/เงิน/ความสุข ----
    [Header("Stats")] // หัวข้อใน Inspector
    public float maxEnergy = 100, maxMoney = 100, maxHappiness = 100; // ค่าสูงสุดของแต่ละหลอด
    public float startEnergy = 70, startMoney = 50, startHappiness = 60; // ค่าตอนเริ่มเกม

    // ---- ค่าลดลงต่อวินาที (ค่าครองชีพ) ปรับความยากของเกมตรงนี้ ----
    [Header("Drain per second (cost of living)")] // หัวข้อใน Inspector
    // จูนจากการจำลองการเล่น (2026-11-09, เดือนละ 2 นาที): ผู้เล่นที่คว้าไอคอนที่มีประโยชน์ได้
    // 50 / 70 / 85 % จะชนะประมาณ 50 / 67 / 77 % ของรอบที่เล่น
    public float energyDrain = 0.8f; // พลังงานลดวินาทีละเท่าไหร่
    public float moneyDrain = 0.6f; // เงินลดวินาทีละเท่าไหร่
    public float happinessDrain = 0.5f; // ความสุขลดวินาทีละเท่าไหร่

    // ---- ความยาวเดือน และกติกาพิเศษ ----
    [Header("Month")] // หัวข้อใน Inspector
    [Tooltip("Seconds of play for one month.")] // คำอธิบายใน Inspector
    public float monthLength = 120f; // 1 เดือน = กี่วินาทีของการเล่น
    public int daysInMonth = 30; // จำนวนวันใน 1 เดือน
    [Tooltip("Money multiplier for work while happiness is 0.")] // คำอธิบายใน Inspector
    [Range(0, 1)] public float burnoutPay = 0.5f; // ตอนหมดไฟ ทำงานได้เงินกี่เท่า (0.5 = ครึ่งเดียว)
    public float lowThreshold = 30f; // ค่าที่ถือว่า "ต่ำ" ใช้เตือนผู้เล่นและเปลี่ยนหน้าตัวละคร
    [Tooltip("Drain multiplier at the end of the month (ramps up from 1). Bills grow, the body gets tired.")] // คำอธิบายใน Inspector
    public float endDrainMultiplier = 1.4f; // ปลายเดือนค่าลดจะเร็วขึ้นถึงกี่เท่า (ไต่จาก 1 ขึ้นไป)

    // ---- อ้างอิง UI ทั้งหมดในฉาก (ลากใส่ใน Inspector) ----
    [Header("UI")] // หัวข้อใน Inspector
    public StatusBar energyBar; // หลอดพลังงาน
    public StatusBar moneyBar; // หลอดเงิน
    public StatusBar happinessBar; // หลอดความสุข
    public TMP_Text timerText; // ข้อความเวลาที่เหลือ
    public TMP_Text dayText; // ข้อความวันที่
    public TMP_Text stateText; // ข้อความเตือนสถานะ
    [Tooltip("Dim layer + popup root. Shown when the run ends.")] // คำอธิบายใน Inspector
    public GameObject gameOverPanel; // หน้าจอแพ้ (พื้นมืด + popup)
    public RectTransform popup; // popup ที่จะเด้งขยายเข้ามา
    public UnityEngine.UI.Image popupImage; // รูปใน popup (เปลี่ยนตามสาเหตุที่แพ้)
    public Sprite gameOverMoneySprite; // รูปแพ้เพราะเงินหมด
    public Sprite gameOverHealthSprite; // รูปแพ้เพราะพลังงานหมด
    public GameObject retryButton; // ปุ่มเล่นใหม่ (โชว์ตอนแพ้)
    public GameObject homeButton; // ปุ่มกลับหน้าแรก (โชว์ตอนแพ้ ข้างปุ่ม Retry)
    public HandCursor handCursor; // เคอร์เซอร์มือที่ใช้ตอนเล่น
    [Tooltip("Full-screen victory art (survived 1 month) with its own Home button.")] // คำอธิบายใน Inspector
    public GameObject victoryPanel; // หน้าจอชนะแบบเต็มจอ
    public RectTransform victoryArt; // ภาพหน้าชนะ
    public RectTransform victoryHomeButton; // ปุ่มกลับหน้าแรกในหน้าชนะ
    public IconSpawner spawner; // ตัวปล่อยไอคอน

    // ---- รูปตัวละครตามอารมณ์ ----
    [Header("Character")] // หัวข้อใน Inspector
    public SpriteRenderer character; // ตัวละครกลางจอ
    public Sprite normalSprite; // หน้าปกติ
    public Sprite sadSprite; // หน้าเศร้า
    public Sprite stressSprite; // หน้าเครียด

    // ---- ชื่อฉาก และเวลาแอนิเมชัน popup ----
    [Header("Flow")] // หัวข้อใน Inspector
    public string gameplayScene = "Gameplay"; // ฉากที่โหลดตอนกด Retry
    public string homeScene = "Title"; // ฉากที่โหลดตอนกด Home
    public float popupDuration = 0.6f; // เวลาแอนิเมชันเด้ง popup (วินาที)

    // ---- ค่าสถานะปัจจุบัน (สคริปต์อื่นอ่านได้ แต่แก้ได้แค่ในคลาสนี้) ----
    public float Energy { get; private set; } // พลังงานปัจจุบัน
    public float Money { get; private set; } // เงินปัจจุบัน
    public float Happiness { get; private set; } // ความสุขปัจจุบัน
    public bool Playing { get; private set; } // กำลังเล่นอยู่ไหม
    public bool BurnedOut => Happiness <= 0f; // หมดไฟ = ความสุขเหลือ 0
    public float EnergyFraction => Energy / maxEnergy; // พลังงานเป็นสัดส่วน 0-1 (IconSpawner ใช้ปรับความเร็ว)

    [Tooltip("Seconds left when the hurry music starts.")] // คำอธิบายใน Inspector
    public float hurryTime = 20f; // เหลือเวลากี่วินาทีให้เปลี่ยนเป็นเพลงเร่ง

    // ---- ตัวแปรภายใน ----
    float timeLeft; // เวลาที่เหลือในเดือนนี้ (วินาที)
    bool over; // เกมจบแล้วหรือยัง
    bool hurryPlaying; // เปิดเพลงเร่งไปแล้วหรือยัง

    // ---- เริ่มต้น: ตั้งค่าสถานะเริ่มเกม ----
    void Awake()
    {
        Instance = this; // ตั้ง singleton
        Energy = startEnergy; Money = startMoney; Happiness = startHappiness; // ใส่ค่าเริ่มต้น
        timeLeft = monthLength; // เริ่มนับเวลาเต็มเดือน
        RefreshBars(true); // อัปเดตหลอดทันทีแบบไม่ต้องค่อยๆ ขยับ
        UpdateClock(); // อัปเดตเวลา/วันที่บนจอ
        if (gameOverPanel) gameOverPanel.SetActive(false); // ซ่อนหน้าจอแพ้
        if (stateText) stateText.text = ""; // ล้างข้อความเตือน
        var hand = HandTrackingClient.Instance; // ดึงตัวรับข้อมูลมือ
        if (hand) hand.speedMultiplier = 1f; // รีเซ็ตความเร็วเคอร์เซอร์มือเป็นปกติ
    }

    // หน้าเช็คมือย้ายไปอยู่ที่หน้า Title แล้ว เลยเริ่มเล่นได้ทันที
    void Start()
    {
        if (over) return; // ถ้าจบไปแล้วไม่ต้องเริ่ม
        Playing = true; // เริ่มเล่น
        if (spawner) spawner.enabled = true; // เปิดตัวปล่อยไอคอน
    }

    // ---- ทุกเฟรม: ลดค่าสถานะตามเวลา นับเวลาถอยหลัง เช็คชนะ ----
    void Update()
    {
        if (spawner) spawner.Paused = !Playing; // ไม่ได้เล่นอยู่ ให้หยุดปล่อยไอคอน
        if (!Playing || over) return; // ไม่ได้เล่นหรือจบแล้ว ไม่ต้องคำนวณต่อ

        float dt = Time.deltaTime; // เวลาระหว่างเฟรม
        float k = Mathf.Lerp(1f, endDrainMultiplier, 1f - Mathf.Max(0, timeLeft) / monthLength); // ตัวคูณค่าลด ไต่จาก 1 ตอนต้นเดือน ถึง endDrainMultiplier ตอนท้ายเดือน
        Change(-energyDrain * k * dt, -moneyDrain * k * dt, -happinessDrain * k * dt); // ลดค่าสถานะทั้ง 3 ตามเวลา

        timeLeft -= dt; // ลดเวลาที่เหลือ
        UpdateClock(); // อัปเดตเวลาบนจอ
        if (!hurryPlaying && timeLeft <= hurryTime) // เวลาใกล้หมดและยังไม่เปิดเพลงเร่ง
        {
            hurryPlaying = true; // จำว่าเปิดแล้ว
            MusicPlayer.PlayTrack(MusicPlayer.Hurry); // เปลี่ยนเป็นเพลงเร่ง
        }
        if (timeLeft <= 0) End(EndKind.Victory); // หมดเวลา = อยู่รอดครบเดือน ชนะ
    }

    // ข้อความเปลี่ยนแค่วินาทีละครั้ง/วันละครั้ง เลยสร้าง string ใหม่เฉพาะตอนนั้น
    int shownSeconds = -1, shownDay = -1; // วินาทีและวันที่แสดงล่าสุด

    // ---- อัปเดตข้อความเวลาและวันที่ ----
    void UpdateClock()
    {
        float left = Mathf.Max(0, timeLeft); // เวลาที่เหลือ (ไม่ติดลบ)
        int s = Mathf.CeilToInt(left); // ปัดขึ้นเป็นวินาทีเต็ม
        if (timerText && s != shownSeconds) // อัปเดตเฉพาะตอนวินาทีเปลี่ยน
        {
            shownSeconds = s; // จำวินาทีที่แสดง
            timerText.text = $"{s / 60}:{s % 60:00}"; // แสดงแบบ นาที:วินาที
        }
        int day = Mathf.Clamp(Mathf.FloorToInt((1f - left / monthLength) * daysInMonth) + 1, 1, daysInMonth); // แปลงเวลาที่ผ่านไปเป็นวันที่ 1-30
        if (dayText && day != shownDay) // อัปเดตเฉพาะตอนวันเปลี่ยน
        {
            shownDay = day; // จำวันที่แสดง
            dayText.text = $"วันที่ {day}/{daysInMonth}"; // แสดงวันที่
        }
    }

    // ---- IconSpawner เรียกตอนผู้เล่นคว้าไอคอน ----
    // คืนค่าที่เปลี่ยนจริง (x = พลังงาน, y = เงิน, z = ความสุข) ให้ popup ตัวเลขเอาไปแสดง
    public Vector3 ApplyItem(IconSpawner.ItemDef item)
    {
        if (!Playing || over) return Vector3.zero; // ไม่ได้เล่นอยู่ ไม่มีผล
        float money = item.money; // เงินที่ไอคอนนี้ให้
        if (item.kind == IconSpawner.Kind.Work && BurnedOut && money > 0) money *= burnoutPay; // หมดไฟอยู่ ทำงานได้เงินลดลง
        var d = new Vector3(item.energy, money, item.happiness); // รวมค่าที่จะเปลี่ยน
        Change(d.x, d.y, d.z); // เปลี่ยนค่าสถานะ
        return d; // คืนค่าที่เปลี่ยน
    }

    // ---- เปลี่ยนค่าสถานะ + อัปเดตข้อความเตือน/ตัวละคร + เช็คแพ้ ----
    void Change(float dEnergy, float dMoney, float dHappiness)
    {
        Energy = Mathf.Clamp(Energy + dEnergy, 0, maxEnergy); // บวกพลังงาน แล้วบีบให้อยู่ในช่วง 0-max
        Money = Mathf.Clamp(Money + dMoney, 0, maxMoney); // บวกเงิน แล้วบีบให้อยู่ในช่วง
        Happiness = Mathf.Clamp(Happiness + dHappiness, 0, maxHappiness); // บวกความสุข แล้วบีบให้อยู่ในช่วง
        RefreshBars(false); // อัปเดตหลอดแบบค่อยๆ ขยับ

        if (stateText) // ข้อความเตือนตามสถานะ (เรียงตามความสำคัญ)
        {
            if (BurnedOut) stateText.text = "<color=#E53935>หมดไฟ! ทำงานได้เงินน้อยลง</color>"; // ความสุขหมด
            else if (Energy < lowThreshold) stateText.text = "<color=#E53935>พลังงานใกล้หมด! กินข้าวหรือนอนพัก</color>"; // พลังงานต่ำ
            else if (Money < lowThreshold * 0.6f) stateText.text = "<color=#E53935>เงินใกล้หมด! ไปทำงานกันเถอะ</color>"; // เงินต่ำ
            else stateText.text = ""; // ปกติ ไม่ต้องเตือน
        }
        UpdateCharacter(); // เปลี่ยนหน้าตัวละครตามสถานะ

        if (Money <= 0) End(EndKind.NoMoney); // เงินหมด แพ้
        else if (Energy <= 0) End(EndKind.NoHealth); // พลังงานหมด แพ้
    }

    // ---- ส่งค่าปัจจุบันไปให้หลอดสถานะทั้ง 3 ----
    void RefreshBars(bool instant)
    {
        if (energyBar) energyBar.Set(Energy, maxEnergy, instant); // หลอดพลังงาน
        if (moneyBar) moneyBar.Set(Money, maxMoney, instant); // หลอดเงิน
        if (happinessBar) happinessBar.Set(Happiness, maxHappiness, instant); // หลอดความสุข
    }

    // ---- เลือกหน้าตัวละคร (ปกติ/เศร้า/เครียด) และความแรงการสั่น ----
    void UpdateCharacter()
    {
        if (!character) return; // ไม่มีตัวละครก็ข้าม
        bool stressed = BurnedOut || Energy < lowThreshold * 0.5f; // เครียด = หมดไฟ หรือพลังงานต่ำมาก
        bool sad = !stressed && (Happiness < lowThreshold || Energy < lowThreshold || Money < lowThreshold); // เศร้า = มีค่าใดค่าหนึ่งต่ำ
        shakeAmount = stressed ? stressShake : sad ? sadShake : 0f; // ตั้งความแรงการสั่นตามอารมณ์
        Sprite s = stressed ? stressSprite : sad ? sadSprite : normalSprite; // เลือกรูปตามอารมณ์
        if (s && character.sprite != s) character.sprite = s; // เปลี่ยนรูปเฉพาะตอนต่างจากเดิม
    }

    // ---- ค่าการสั่นของตัวละคร ปรับความแรง/ความเร็วได้ ----
    [Header("Character shake")] // หัวข้อใน Inspector
    [Tooltip("Shake distance (world units) while sad.")] // คำอธิบายใน Inspector
    public float sadShake = 0.03f; // ระยะสั่นตอนเศร้า (หน่วย world)
    [Tooltip("Shake distance (world units) while stressed.")] // คำอธิบายใน Inspector
    public float stressShake = 0.1f; // ระยะสั่นตอนเครียด (หน่วย world)
    public float shakeSpeed = 25f; // ความเร็วการสั่น
    float shakeAmount; // ความแรงการสั่นตอนนี้
    Vector3 characterBase; // ตำแหน่งปกติของตัวละคร
    bool characterBaseSet; // เก็บตำแหน่งปกติไปแล้วหรือยัง

    // ---- หลังทุกอย่างอัปเดตแล้ว: สั่นตัวละครด้วย Perlin noise ----
    void LateUpdate()
    {
        if (!character) return; // ไม่มีตัวละครก็ข้าม
        if (!characterBaseSet) { characterBase = character.transform.localPosition; characterBaseSet = true; } // จำตำแหน่งเดิมครั้งแรก
        float a = Playing && !over ? shakeAmount : 0f; // สั่นเฉพาะตอนกำลังเล่น
        Vector3 offset = Vector3.zero; // ระยะเลื่อนจากการสั่น
        if (a > 0f) // มีการสั่น
        {
            float t = Time.time * shakeSpeed; // เวลาคูณความเร็ว
            offset = new Vector3((Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f * a, // แกน X สุ่มแบบนุ่มด้วย Perlin noise ช่วง -a ถึง a
                                 (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f * a, 0f); // แกน Y ใช้ noise อีกเส้น
        }
        var pos = characterBase + offset; // ตำแหน่งใหม่
        if (character.transform.localPosition != pos) character.transform.localPosition = pos; // ย้ายเฉพาะตอนตำแหน่งเปลี่ยน
    }

    enum EndKind { Victory, NoMoney, NoHealth } // แบบของการจบเกม: ชนะ / เงินหมด / พลังงานหมด

    // ---- จบเกม: หยุดเล่น เปลี่ยนเพลง แสดงหน้าชนะหรือแพ้ ----
    void End(EndKind kind)
    {
        if (over) return; // จบไปแล้วไม่ต้องทำซ้ำ
        over = true; // ตั้งว่าจบแล้ว
        Playing = false; // หยุดเล่น
        HandTrackingClient.ResetHandLock(); // ปลดล็อกมือ เกมถัดไปจะเลือก Hand 1 ใหม่
        if (spawner) spawner.Paused = true; // หยุดปล่อยไอคอน
        if (stateText) stateText.text = ""; // ล้างข้อความเตือน
        MusicPlayer.PlayTrack(kind == EndKind.Victory ? MusicPlayer.Victory : MusicPlayer.GameOver); // เล่นเพลงชนะหรือแพ้

        if (kind == EndKind.Victory && victoryPanel) // ชนะ
        {
            victoryPanel.SetActive(true); // แสดงหน้าชนะ
            StartCoroutine(VictoryAnim()); // เล่นแอนิเมชันหน้าชนะ
        }
        else if (gameOverPanel) // แพ้
        {
            gameOverPanel.SetActive(true); // แสดงหน้าแพ้
            if (popupImage) // มีรูปใน popup
                popupImage.sprite = kind == EndKind.NoMoney ? gameOverMoneySprite : gameOverHealthSprite; // เลือกรูปตามสาเหตุที่แพ้
            if (retryButton) retryButton.SetActive(true); // แสดงปุ่ม Retry
            if (homeButton) homeButton.SetActive(true); // แสดงปุ่ม Home
            StartCoroutine(PopIn()); // เล่นแอนิเมชัน popup เด้ง
        }
        EnableMenuPointer(); // เปลี่ยนเป็นเคอร์เซอร์แบบกดปุ่มเมนู
    }

    // ---- แอนิเมชัน popup แพ้: ขยายจาก 0 แบบเด้งเกินนิดนึง และพื้นมืดค่อยๆ ขึ้น ----
    System.Collections.IEnumerator PopIn()
    {
        var dim = gameOverPanel.GetComponent<UnityEngine.UI.Image>(); // Image พื้นมืด
        Color dimColor = dim ? dim.color : Color.clear; // จำสีพื้นมืดเดิม
        for (float t = 0; t < popupDuration; t += Time.unscaledDeltaTime) // วนจนครบเวลาแอนิเมชัน
        {
            float x = t / popupDuration; // ความคืบหน้า 0-1
            float c1 = 1.70158f, c3 = c1 + 1f; // ค่าคงที่ของสูตร easeOutBack
            float s = 1f + c3 * Mathf.Pow(x - 1f, 3) + c1 * Mathf.Pow(x - 1f, 2); // easeOutBack: ขยายเกิน 1 นิดนึงแล้วกลับมา
            if (popup) popup.localScale = Vector3.one * Mathf.Max(0, s); // ตั้งสเกล popup
            if (dim) dim.color = new Color(dimColor.r, dimColor.g, dimColor.b, dimColor.a * Mathf.Clamp01(x * 2f)); // พื้นมืดค่อยๆ ทึบขึ้น
            yield return null; // รอเฟรมถัดไป
        }
        if (popup) popup.localScale = Vector3.one; // จบแล้วตั้งสเกลให้ตรง 1
        if (dim) dim.color = dimColor; // คืนสีพื้นมืดเต็ม
    }

    // ---- แอนิเมชันหน้าชนะ: เด้งขยายเข้ามาครั้งเดียว แล้วอยู่นิ่ง ----
    System.Collections.IEnumerator VictoryAnim()
    {
        var group = victoryPanel.GetComponent<CanvasGroup>(); // CanvasGroup ไว้ปรับความโปร่งใสทั้งหน้า
        for (float t = 0; t < popupDuration; t += Time.unscaledDeltaTime) // วนจนครบเวลาแอนิเมชัน
        {
            float x = t / popupDuration; // ความคืบหน้า 0-1
            float c1 = 1.70158f, c3 = c1 + 1f; // ค่าคงที่ของสูตร easeOutBack
            float e = 1f + c3 * Mathf.Pow(x - 1f, 3) + c1 * Mathf.Pow(x - 1f, 2); // easeOutBack
            if (group) group.alpha = Mathf.Clamp01(x * 3f); // ค่อยๆ ชัดขึ้น (เร็วกว่าการขยาย 3 เท่า)
            victoryPanel.transform.localScale = Vector3.one * Mathf.Max(0, e); // ตั้งสเกลหน้าชนะ
            yield return null; // รอเฟรมถัดไป
        }
        if (group) group.alpha = 1f; // จบแล้วให้ทึบเต็ม
        victoryPanel.transform.localScale = Vector3.one; // ตั้งสเกลให้ตรง 1
    }

    // ---- ตอนเล่นใช้เคอร์เซอร์แบบคว้าไอคอน พอจบเกมเปลี่ยนเป็นเคอร์เซอร์เมนู (ชี้ + กำมือ = กดปุ่ม) ----
    void EnableMenuPointer()
    {
        if (handCursor) handCursor.gameObject.SetActive(false); // ปิดเคอร์เซอร์ตอนเล่น
        var pointer = gameObject.AddComponent<HandUIPointer>(); // เพิ่มเคอร์เซอร์เมนู
        if (handCursor) // ใช้รูปมือเดียวกับตอนเล่น
        {
            pointer.openSprite = handCursor.openSprite; // รูปมือแบ
            pointer.fistSprite = handCursor.fistSprite; // รูปมือกำ
        }
        pointer.cursorSize = new Vector2(230, 230); // ขนาดเคอร์เซอร์
        pointer.startDelay = popupDuration + 0.4f; // รอ popup เด้งเสร็จก่อนค่อยรับการกด กันกดพลาด
    }

    // ---- ฟังก์ชันให้ปุ่มเรียก ----
    public void Retry() => SceneManager.LoadScene(gameplayScene); // Retry: กลับไปเล่นใหม่ทันที ไม่ต้องผ่าน tutorial
    public void GoHome() => SceneManager.LoadScene(homeScene); // Home: กลับหน้าแรก
}
