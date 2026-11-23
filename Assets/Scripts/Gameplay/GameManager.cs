using TMPro; // ใช้ TextMeshPro สำหรับข้อความเวลา/วัน/สถานะ
using UnityEngine; // ใช้คลาสหลักของ Unity
using UnityEngine.SceneManagement; // ใช้โหลดฉาก (Retry / กลับหน้าแรก)

// กติกาเกม (ตามหน้า Tutorial ใน Assets/Art/Backgrounds/Tutorial):
//   เล่นด้วยมือเดียว กำมือที่ไอคอนเพื่อเลือกกิจกรรมนั้น
//   เล่น 3 นาที = ใช้ชีวิต 1 เดือน (30 วัน)
//   ระหว่างเกมมี Event "สงกรานต์พักใจ" 30 วินาที: เงินไม่ลด, ไอคอนกิจกรรมสงกรานต์ออกเฉพาะช่วงนี้
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
    public float happinessDrain = 0.7f; // ความสุขลดวินาทีละเท่าไหร่ (เพิ่มจาก 0.5 ให้ความสุขรักษายากขึ้นตอนทำงาน)

    // ---- ความยาวเดือน และกติกาพิเศษ ----
    [Header("Month")] // หัวข้อใน Inspector
    [Tooltip("Seconds of play for one month.")] // คำอธิบายใน Inspector
    public float monthLength = 180f; // 1 เดือน = กี่วินาทีของการเล่น
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

    // ---- Event สงกรานต์พักใจ ----
    public const string SongkranMusic = "BGM_Songkran"; // เพลงช่วง Event (Assets/Sound/Resources/Music)
    public const string EventCharacters = "สงกรานต์พักใจ! เงินไม่ลด เหลือ วิ กินข้าวแช่ คอมโบx จบสงกรานต์ สูงสุด ได้โบนัส+ หลุด ความสุขขึ้นง่ายขึ้น% ทำงานรอบต่อไป"; // ตัวอักษรที่ใช้ในข้อความ Event (ให้ IconSpawner วาดลงฟอนต์ล่วงหน้า)
    [Header("Songkran Event")] // หัวข้อใน Inspector
    [Tooltip("Seconds after the game starts when the Songkran event begins.")] // คำอธิบายใน Inspector
    public float eventStart = 60f; // เริ่ม Event หลังเล่นไปกี่วินาที (เกม 3 นาที: ตัวจับเวลา 2:00)
    public float eventDuration = 60f; // Event ยาวกี่วินาที (ตัวจับเวลา 2:00 -> 1:00)
    public SpriteRenderer background; // พื้นหลังในฉาก (เปลี่ยนรูปช่วง Event)
    public Sprite songkranBackground; // พื้นหลังบรรยากาศสงกรานต์
    public Sprite songkranBanner; // ป้าย "สงกรานต์พักใจ" ที่เด้งขึ้นตอนเริ่ม Event
    public Sprite songkranNormalSprite; // ตัวละครชุดสงกรานต์ หน้าปกติ
    public Sprite songkranHappySprite; // ตัวละครชุดสงกรานต์ หน้ามีความสุข
    public Sprite songkranHandOpen; // เคอร์เซอร์มือแบช่วง Event (แขนเสื้อลายดอก)
    public Sprite songkranHandFist; // เคอร์เซอร์มือกำช่วง Event
    Sprite workHandOpen, workHandFist; // รูปมือปกติ (คืนค่าหลังจบ Event)
    [Tooltip("Happiness at or above this shows the happy emote and the character keeps jumping.")] // คำอธิบายใน Inspector
    public float happyThreshold = 70f; // ความสุขถึงเท่านี้ = มีความสุขมาก
    [Tooltip("Jump height in screen pixels (world units).")] // คำอธิบายใน Inspector
    public float jumpHeight = 45f; // ความสูงที่กระโดด (พิกเซลบนจอ)
    public float jumpSpeed = 2.2f; // กระโดดกี่ครั้งต่อวินาที
    public bool InEvent { get; private set; } // อยู่ในช่วง Event ไหม

    // ---- คอมโบสงกรานต์: คว้าไอคอนสงกรานต์ต่อเนื่องภายใน comboWindow วินาที ผลบวกจะคูณเพิ่ม ----
    [Header("Songkran Combo")] // หัวข้อใน Inspector
    [Tooltip("Seconds allowed between Songkran grabs to keep the combo.")] // คำอธิบายใน Inspector
    public float comboWindow = 2.5f; // ต้องคว้าอันถัดไปภายในกี่วินาทีคอมโบถึงไม่หลุด
    public float comboStep = 0.25f; // คอมโบเพิ่มทีละเท่าไหร่ (x1, x1.25, x1.5 ...)
    public float comboMax = 2f; // ตัวคูณสูงสุด
    [Tooltip("Money bonus per best-combo step, paid when the event ends.")] // คำอธิบายใน Inspector
    public float bonusPerCombo = 2f; // โบนัสเงินตอนจบ Event = คอมโบสูงสุด x ค่านี้
    public float bonusMax = 25f; // โบนัสเงินสูงสุด
    [Tooltip("Every N combo, 1-2 money icons (bonus / relatives) drop.")] // คำอธิบายใน Inspector
    [Min(1)] public int moneyDropEvery = 10; // ทุกๆ กี่คอมโบ ไอคอนเงินจะตกมา 1 อัน
    [Tooltip("Max money icons (bonus / relatives) per Songkran event.")] // คำอธิบายใน Inspector
    [Min(0)] public int moneyDropMax = 2; // ทั้ง Event ไอคอนเงินตกได้ไม่เกินกี่อัน (คอมโบ 10 = 1 อัน, คอมโบ 20 = 2 อัน)
    int moneyDropped; // ไอคอนเงินที่ตกไปแล้วใน Event นี้
    [Tooltip("After the event, each best-combo step makes happiness easier in the 2nd work round (0.05 = +5%).")] // คำอธิบายใน Inspector
    public float happyBuffPerCombo = 0.02f; // คอมโบสูงสุด 1 ครั้ง = ความสุขขึ้นง่ายขึ้นกี่ % (2% ต่อคอมโบ ได้ยากขึ้น)
    public float happyBuffMax = 0.3f; // บัฟสูงสุด (+30% ต้องคอมโบ x15 ขึ้นไป)
    public float HappyBuff { get; private set; } // บัฟความสุขหลัง Event (0 = ไม่มี) ใช้ตอนทำงานรอบ 2
    [Tooltip("Combo tiers above the normal cap: reach the combo to get that bonus (0.4 = +40%).")] // คำอธิบายใน Inspector
    public Vector2[] happyBuffTiers = { new Vector2(20, 0.4f), new Vector2(30, 0.5f), new Vector2(40, 0.6f), new Vector2(50, 1f) }; // ขั้นบัฟ (คอมโบ, %) : x20 = 40%, x30 = 50%, x40 = 60%, x50 ขึ้นไป = 100%

    // ---- คำนวณบัฟความสุขจากคอมโบสูงสุด: ต่ำกว่า x20 ได้คอมโบละ 2% (สูงสุด 30% ที่ x15) แล้วเป็นขั้นตาม happyBuffTiers ----
    public float BuffForCombo(int best)
    {
        float buff = Mathf.Min(happyBuffMax, best * happyBuffPerCombo); // ช่วงแรก: คอมโบละ 2% ไม่เกิน 30%
        foreach (var t in happyBuffTiers) if (best >= t.x) buff = Mathf.Max(buff, t.y); // ถึงขั้นไหนได้ขั้นนั้น
        return buff; // คืน % บัฟ
    }
    public int Combo { get; private set; } // คอมโบปัจจุบัน
    public float ComboMultiplier => Mathf.Min(comboMax, 1f + Mathf.Max(0, Combo - 1) * comboStep); // ตัวคูณจากคอมโบ
    int bestCombo; // คอมโบสูงสุดในรอบ Event นี้
    float comboTimer; // เวลาที่เหลือก่อนคอมโบหลุด
    float messageTimer; // เวลาที่ข้อความพิเศษ (โบนัส) ค้างบนจอ

    Sprite originalBackground; // พื้นหลังเดิม (คืนค่าหลังจบ Event)
    Vector3 originalBgScale; // สเกลพื้นหลังเดิม
    bool eventDone; // Event จบไปแล้ว
    float elapsed; // เวลาที่เล่นไปแล้ว
    AudioSource sfx; // ตัวเล่นเสียงเอฟเฟกต์
    [Header("Sound")] // หัวข้อใน Inspector
    [Tooltip("Volume of all sound effects (grab sounds, water splash, coins). Can be changed while playing.")] // คำอธิบายใน Inspector
    [Range(0, 1)] public float sfxVolume = 1f; // ความดังเสียงเอฟเฟกต์ทั้งหมด (ปรับระหว่างเล่นได้)
    AudioClip splashClip; // เสียงน้ำสาด
    AudioClip workClip, relaxClip, recoverClip, coinClip; // เสียงตอนคว้าไอคอนแต่ละประเภท (+ เหรียญสำหรับไอคอนเงิน)
    int shownEventSeconds = -1; // วินาทีของ Event ที่แสดงล่าสุด

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
        sfx = gameObject.AddComponent<AudioSource>(); // ตัวเล่นเสียงเอฟเฟกต์
        sfx.playOnAwake = false; // ไม่เล่นเอง
        splashClip = Resources.Load<AudioClip>("SFX/SFX_Splash"); // โหลดเสียงน้ำสาด (Assets/Sound/Resources/SFX)
        workClip = Resources.Load<AudioClip>("SFX/SFX_Work"); // เสียงพิมพ์คีย์บอร์ด + ปั๊มตรา = งาน
        coinClip = Resources.Load<AudioClip>("SFX/SFX_Coin"); // เสียงเหรียญ = ไอคอนได้เงินช่วงสงกรานต์
        relaxClip = Resources.Load<AudioClip>("SFX/SFX_Relax"); // เสียงกระดิ่งร่าเริง = พักผ่อน
        recoverClip = Resources.Load<AudioClip>("SFX/SFX_Recover"); // เสียงป๊อบนุ่มๆ = ฟื้นพลัง
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
        if (spawner) spawner.Paused = !Playing || transitioning; // ไม่ได้เล่นอยู่/กำลังเปลี่ยนฉาก ให้หยุดปล่อยไอคอน
        if (!Playing || over || transitioning) return; // ไม่ได้เล่นหรือจบแล้ว ไม่ต้องคำนวณต่อ

        float dt = Time.deltaTime; // เวลาระหว่างเฟรม
        elapsed += dt; // นับเวลาที่เล่น
        UpdateEvent(); // เช็คเริ่ม/จบ Event สงกรานต์
        float k = Mathf.Lerp(1f, endDrainMultiplier, 1f - Mathf.Max(0, timeLeft) / monthLength); // ตัวคูณค่าลด ไต่จาก 1 ตอนต้นเดือน ถึง endDrainMultiplier ตอนท้ายเดือน
        Change(InEvent ? 0f : -energyDrain * k * dt, InEvent ? 0f : -moneyDrain * k * dt, -happinessDrain * k * dt / (1f + HappyBuff)); // ลดค่าสถานะตามเวลา (บัฟหลังสงกรานต์: ความสุขลดช้าลง) (ช่วง Event เงินและพลังงานไม่ลดเอง ลดได้แค่จากกิจกรรม เช่น เล่นน้ำ)

        if (comboTimer > 0 && (comboTimer -= dt) <= 0) Combo = 0; // นานเกินไม่คว้าต่อ คอมโบหลุด
        if (messageTimer > 0) messageTimer -= dt; // นับเวลาข้อความพิเศษ
        timeLeft -= dt; // ลดเวลาที่เหลือ
        UpdateClock(); // อัปเดตเวลาบนจอ
        if (!hurryPlaying && !InEvent && timeLeft <= hurryTime) // เวลาใกล้หมดและยังไม่เปิดเพลงเร่ง
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
        if (!InEvent && HappyBuff > 0 && d.z > 0) d.z *= 1f + HappyBuff; // บัฟหลังสงกรานต์: กิจกรรมได้ความสุขเพิ่มขึ้น
        if (item.kind == IconSpawner.Kind.Songkran) // ไอคอนสงกรานต์: นับคอมโบ
        {
            Combo = comboTimer > 0 ? Combo + 1 : 1; // คว้าต่อเนื่อง = คอมโบเพิ่ม
            comboTimer = comboWindow; // รีเซ็ตเวลา
            bestCombo = Mathf.Max(bestCombo, Combo); // จำคอมโบสูงสุด
            if (Combo % moneyDropEvery == 0 && spawner && moneyDropped < moneyDropMax) { spawner.DropMoneyIcons(1); moneyDropped++; } // ทุกๆ 10 คอมโบ: ไอคอนเงินตก 1 อัน (ทั้ง Event ได้ไม่เกิน moneyDropMax อัน)
            float m = ComboMultiplier; // ตัวคูณ
            if (d.x > 0) d.x *= m; // คูณเฉพาะผลบวก (ค่าลบอย่างพลังงานที่ใช้เล่นน้ำ ไม่คูณ)
            if (d.z > 0) d.z *= m;
        }
        Change(d.x, d.y, d.z); // เปลี่ยนค่าสถานะ
        return d; // คืนค่าที่เปลี่ยน
    }

    // ---- เปลี่ยนค่าสถานะ + อัปเดตข้อความเตือน/ตัวละคร + เช็คแพ้ ----
    void Change(float dEnergy, float dMoney, float dHappiness)
    {
        Energy = Mathf.Clamp(Energy + dEnergy, 0, maxEnergy); // บวกพลังงาน แล้วบีบให้อยู่ในช่วง 0-max
        if (InEvent && dMoney < 0) dMoney = 0; // ช่วง Event เงินถูกล็อกไม่ให้ลด
        Money = Mathf.Clamp(Money + dMoney, 0, maxMoney); // บวกเงิน แล้วบีบให้อยู่ในช่วง
        Happiness = Mathf.Clamp(Happiness + dHappiness, 0, maxHappiness); // บวกความสุข แล้วบีบให้อยู่ในช่วง
        RefreshBars(false); // อัปเดตหลอดแบบค่อยๆ ขยับ

        if (stateText) // ข้อความเตือนตามสถานะ (เรียงตามความสำคัญ)
        {
            if (messageTimer > 0) { } // กำลังแสดงข้อความโบนัส ไม่ต้องทับ
            else if (InEvent) { if (Energy < lowThreshold) stateText.text = "<color=#E53935>พลังงานใกล้หมด! กินข้าวแช่หรือนอนพัก</color>"; else ShowEventText(true); } // ช่วง Event แสดงเวลาที่เหลือ
            else if (BurnedOut) stateText.text = "<color=#E53935>หมดไฟ! ทำงานได้เงินน้อยลง</color>"; // ความสุขหมด
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
        if (InEvent && songkranNormalSprite) // ช่วง Event ใช้ตัวละครชุดสงกรานต์
        {
            mood = Mood.Calm; // ไม่สั่น
            Sprite e = Happiness >= happyThreshold && songkranHappySprite ? songkranHappySprite : songkranNormalSprite; // มีความสุขมาก = emote ยิ้มกว้าง
            if (character.sprite != e) character.sprite = e; // เปลี่ยนรูป
            return; // ไม่ต้องใช้รูปปกติ
        }
        bool stressed = BurnedOut || Energy < lowThreshold * 0.5f; // เครียด = หมดไฟ หรือพลังงานต่ำมาก
        bool sad = !stressed && (Happiness < lowThreshold || Energy < lowThreshold || Money < lowThreshold); // เศร้า = มีค่าใดค่าหนึ่งต่ำ
        mood = stressed ? Mood.Stressed : sad ? Mood.Sad : Mood.Calm; // เลือกแบบการขยับตามอารมณ์
        Sprite s = stressed ? stressSprite : sad ? sadSprite : normalSprite; // เลือกรูปตามอารมณ์
        if (s && character.sprite != s) character.sprite = s; // เปลี่ยนรูปเฉพาะตอนต่างจากเดิม
    }

    // ---- ค่าการสั่นของตัวละคร ปรับความแรง/ความเร็วได้ ----
    [Header("Character shake")] // หัวข้อใน Inspector
    [Tooltip("Sad: gentle left-right sway distance (screen pixels).")] // คำอธิบายใน Inspector
    public float sadSwayPx = 10f; // เศร้า: โยกซ้ายขวาเบาๆ ระยะกี่พิกเซล
    public float sadSwaySpeed = 1.2f; // เศร้า: โยกกี่รอบต่อวินาที (ช้าๆ)
    [Tooltip("Stressed: strong shake distance (screen pixels).")] // คำอธิบายใน Inspector
    public float stressShakePx = 26f; // เครียด: สั่นแรงระยะกี่พิกเซล
    public float stressShakeSpeed = 11f; // เครียด: สั่นกี่รอบต่อวินาที (เร็ว)
    enum Mood { Calm, Sad, Stressed } // อารมณ์ที่ใช้เลือกแบบการขยับ
    Mood mood; // อารมณ์ตอนนี้
    Vector3 characterBase; // ตำแหน่งปกติของตัวละคร
    bool characterBaseSet; // เก็บตำแหน่งปกติไปแล้วหรือยัง

    // ---- หลังทุกอย่างอัปเดตแล้ว: สั่นตัวละครด้วย Perlin noise ----
    Vector3 characterBaseScale; // สเกลปกติของตัวละคร

    void LateUpdate()
    {
        UpdateEventTextBox(); // กรอบข้อความ Event
        if (!character) return; // ไม่มีตัวละครก็ข้าม
        var tr = character.transform; // transform ตัวละคร
        if (!characterBaseSet) { characterBase = tr.localPosition; characterBaseScale = tr.localScale; characterBaseSet = true; } // จำตำแหน่ง/สเกลเดิมครั้งแรก

        // ---- ตอนดีใจช่วง Event: รูปเต็มตัว (ไม่ crop ขา) ขนาดเท่าเดิม หัวอยู่ที่เดิม แล้วกระโดดขึ้นลงตลอด ----
        if (InEvent && songkranHappySprite && character.sprite == songkranHappySprite)
        {
            if (tr.localScale != characterBaseScale) tr.localScale = characterBaseScale; // ขนาดเท่าเดิม ไม่ย่อ
            float extra = songkranNormalSprite ? songkranHappySprite.bounds.max.y - songkranNormalSprite.bounds.max.y : 0f; // ให้ขอบบน (หัว) ตรงกับรูปปกติ ขาจะยื่นลงล่าง
            float hop = Playing && !over && !transitioning ? Mathf.Abs(Mathf.Sin(Time.time * jumpSpeed * Mathf.PI)) : 0f; // |sin| = ลอยขึ้นแล้วตกลงพื้น เหมือนกระโดด
            var parent = tr.parent; // แปลงพิกเซลบนจอเป็นหน่วย local
            float up = hop * jumpHeight / (parent ? Mathf.Max(0.0001f, parent.lossyScale.y) : 1f); // ระยะกระโดด (local)
            tr.localPosition = characterBase + new Vector3(0f, up - extra * characterBaseScale.y, 0f); // วางตำแหน่ง
            return; // ไม่ต้องสั่น
        }
        if (tr.localScale != characterBaseScale) tr.localScale = characterBaseScale; // คืนสเกลปกติ

        Vector3 offset = Vector3.zero; // ระยะเลื่อนจากการขยับ
        if (Playing && !over && !transitioning && mood != Mood.Calm) // ขยับเฉพาะตอนกำลังเล่น
        {
            float px = 1f / (tr.parent ? Mathf.Max(0.0001f, tr.parent.lossyScale.x) : 1f); // แปลงพิกเซลบนจอเป็นหน่วย local
            if (mood == Mood.Sad) // เศร้า: โยกซ้ายขวาช้าๆ เบาๆ
                offset.x = Mathf.Sin(Time.time * sadSwaySpeed * 2f * Mathf.PI) * sadSwayPx * px;
            else // เครียด: สั่นซ้ายขวาแรงและเร็ว (ผสม noise ให้ดูกระตุกไม่สม่ำเสมอ)
            {
                float t = Time.time * stressShakeSpeed * 2f * Mathf.PI; // เวลาคูณความเร็ว
                float jitter = 0.65f + 0.35f * Mathf.PerlinNoise(Time.time * 6f, 0f); // ความแรงแกว่งขึ้นลงนิดๆ
                offset.x = Mathf.Sin(t) * stressShakePx * jitter * px; // ซ้ายขวาแรงๆ
                offset.y = (Mathf.PerlinNoise(0f, Time.time * 20f) - 0.5f) * stressShakePx * 0.25f * px; // ขึ้นลงนิดหน่อย
            }
        }
        var pos = characterBase + offset; // ตำแหน่งใหม่
        if (tr.localPosition != pos) tr.localPosition = pos; // ย้ายเฉพาะตอนตำแหน่งเปลี่ยน
    }

    // ---- Event สงกรานต์พักใจ: เริ่มที่ eventStart ยาว eventDuration วินาที ----
    // ก่อนเข้า/ออก Event จะหยุดเวลา (transitioning) แล้วเล่นฉากเปลี่ยน จบแล้วค่อยเล่นต่อ
    [Header("Songkran Transition")] // หัวข้อใน Inspector
    public float fadeTime = 0.6f; // เวลา fade ดำลง / สว่างขึ้น (วินาที)
    public float bannerHold = 1.8f; // ป้ายค้างกลางจอกี่วินาที
    public float unblurTime = 0.9f; // เวลาที่ภาพเบลอค่อยๆ ชัดขึ้น
    public Vector2 bannerSize = new Vector2(1400, 700); // ขนาดป้ายตอนขยายใหญ่ (สัดส่วน 2:1)
    public float bannerY = -40f; // ตำแหน่งแนวตั้งของป้าย (ติดลบ = ต่ำกว่ากลางจอ)
    bool transitioning; // กำลังเล่นฉากเปลี่ยน (หยุดจับเวลา)

    void UpdateEvent()
    {
        if (transitioning) return; // กำลังเปลี่ยนฉากอยู่
        if (!InEvent && !eventDone && elapsed >= eventStart) StartCoroutine(EnterEvent()); // ถึงเวลาเริ่ม
        else if (InEvent && elapsed >= eventStart + eventDuration) StartCoroutine(ExitEvent()); // ครบเวลาแล้ว
        else if (InEvent) ShowEventText(false); // อัปเดตเวลาที่เหลือของ Event
    }

    // ---- เข้า Event: หยุดเวลา -> fade ดำ -> เปลี่ยนฉาก -> ภาพเบลอ + ป้ายเด้ง -> ค่อยชัด -> เล่นต่อ ----
    System.Collections.IEnumerator EnterEvent()
    {
        transitioning = true; // หยุดจับเวลา
        var black = MakeOverlay("FadeBlack", Color.black); // ชั้นดำสำหรับ fade
        yield return FadeAlpha(black, 0f, 1f, fadeTime); // fade ดำลง

        StartEvent(); // เปลี่ยนพื้นหลัง/ตัวละคร/เพลง ตอนจอดำ
        yield return null; // รอ 1 เฟรมให้ภาพใหม่พร้อม
        var blur = MakeBlurOverlay(black); // ถ่ายภาพฉากใหม่แบบเบลอมาปิดหน้าจอ
        var banner = MakeBanner(); // ป้ายสงกรานต์พักใจ (ยังไม่โผล่)
        yield return FadeAlpha(black, 1f, 0f, fadeTime * 0.7f); // จอดำค่อยหาย เห็นภาพเบลอ
        Destroy(black.gameObject); // ไม่ใช้แล้ว

        PlaySplash(); // เสียงน้ำสาดตอนป้ายเด้ง
        var brt = banner ? (RectTransform)banner.transform : null; // RectTransform ของป้าย
        var popGroup = banner ? banner.GetComponent<CanvasGroup>() : null; // ใช้ให้ป้ายโผล่แบบจางเข้าเร็วๆ
        for (float t = 0; t < popupDuration + bannerHold && brt; t += Time.unscaledDeltaTime) // ป้ายเด้งแล้วค้าง
        {
            float x = Mathf.Clamp01(t / popupDuration); // ความคืบหน้าการเด้ง
            float c1 = 1.70158f, c3 = c1 + 1f; // easeOutBack
            float e = 1f + c3 * Mathf.Pow(x - 1f, 3) + c1 * Mathf.Pow(x - 1f, 2); // ขยายเกินแล้วกลับ
            brt.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, e) * (1f + 0.025f * Mathf.Sin(t * 7f)); // เริ่มใหญ่เกือบเต็ม (ไม่มีช่วงโลโก้เล็ก) แล้วเด้งเกินนิดก่อนเข้าที่ + ดุ๊กดิ๊กเบาๆ
            if (popGroup) popGroup.alpha = Mathf.Clamp01(t / 0.12f); // โผล่เร็วใน 0.12 วิ
            yield return null; // รอเฟรมถัดไป
        }

        var bannerGroup = banner ? banner.GetComponent<CanvasGroup>() : null; // ใช้จางป้าย
        for (float t = 0; t < unblurTime; t += Time.unscaledDeltaTime) // ป้ายจางออก + ภาพค่อยชัด
        {
            float k = t / unblurTime; // 0-1
            if (blur) blur.color = new Color(1, 1, 1, 1f - k); // ภาพเบลอจางหาย = ภาพชัดขึ้น
            if (bannerGroup) bannerGroup.alpha = 1f - k; // ป้ายจางออก
            if (brt) brt.localScale = Vector3.one * (1f + 0.15f * k); // ป้ายขยายออกนิดตอนจาง
            yield return null; // รอเฟรมถัดไป
        }
        if (blur) { Destroy(blur.texture); Destroy(blur.gameObject); } // ลบภาพเบลอ
        if (banner) Destroy(banner); // ลบป้าย
        ShowEventText(true); // แสดงเวลาที่เหลือของ Event
        transitioning = false; // เล่นต่อ
    }

    // ---- ออก Event: หยุดเวลา -> fade ดำ -> คืนฉากเดิม -> fade สว่าง -> เล่นต่อ ----
    System.Collections.IEnumerator ExitEvent()
    {
        transitioning = true; // หยุดจับเวลา
        var black = MakeOverlay("FadeBlack", Color.black); // ชั้นดำ
        yield return FadeAlpha(black, 0f, 1f, fadeTime); // fade ดำลง
        int best = bestCombo; // คอมโบสูงสุดของรอบนี้
        StopEvent(); // คืนพื้นหลัง/ตัวละคร/เพลง/ไอคอนปกติ
        float bonus = Mathf.Min(bonusMax, best * bonusPerCombo); // โบนัสเงินตามคอมโบสูงสุด
        HappyBuff = BuffForCombo(best); // คอมโบสูงสุด -> ความสุขขึ้นง่ายขึ้นตอนทำงานรอบ 2
        if (bonus > 0) Change(0, bonus, 0); // ได้โบนัส: ให้เงิน
        Combo = 0; comboTimer = 0; // ล้างคอมโบ
        yield return FadeAlpha(black, 1f, 0.45f, fadeTime); // สว่างขึ้นครึ่งทาง (ยังมืดอยู่ให้ป้ายสรุปเด่น)
        if (best > 0) yield return SummaryPopup(best, bonus); // ป้ายสรุปผลสงกรานต์เด้งขึ้น ก่อนกลับไปทำงาน
        yield return FadeAlpha(black, black.color.a, 0f, fadeTime * 0.7f); // สว่างเต็ม
        Destroy(black.gameObject); // ลบชั้นดำ
        transitioning = false; // เล่นต่อ
    }

    // ---- ป้ายสรุปผลตอนจบสงกรานต์: เด้งเข้าแบบเดียวกับโลโก้สงกรานต์ ค้างไว้ แล้วจางออก ----
    [Tooltip("Seconds the Songkran result popup stays before work resumes.")] // คำอธิบายใน Inspector
    public float summaryHold = 2.8f; // ป้ายสรุปผลค้างกี่วินาที
    System.Collections.IEnumerator SummaryPopup(int best, float bonus)
    {
        var root = HudRoot(); // root canvas
        if (!root) yield break; // ไม่มี canvas ก็ข้าม
        var font = stateText ? stateText.font : null; // ฟอนต์ไทยของเกม
        var go = new GameObject("SongkranSummary", typeof(RectTransform), typeof(CanvasGroup), typeof(UnityEngine.UI.Image)); // กล่องป้าย
        var rt = (RectTransform)go.transform; // RectTransform
        rt.SetParent(root, false); // ใส่ใต้ canvas
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f); // กลางจอ
        rt.sizeDelta = new Vector2(1000, 440); // ขนาดกล่อง
        rt.anchoredPosition = new Vector2(0, -60); // ต่ำกว่ากลางนิดหน่อย (เหลือที่ให้โลโก้ด้านบน)
        var box = go.GetComponent<UnityEngine.UI.Image>(); // พื้นกล่อง
        box.color = new Color(1f, 1f, 1f, 0.96f); box.raycastTarget = false; // พื้นขาว ไม่รับคลิก
        foreach (var d in new[] { new Vector2(8, -8), new Vector2(-8, 8) }) // ขอบฟ้าหนา 2 ทิศ
        {
            var o = go.AddComponent<UnityEngine.UI.Outline>(); // ขอบ
            o.effectColor = new Color32(0x1E, 0x88, 0xE5, 0xFF); o.effectDistance = d; // สีฟ้าน้ำ
        }
        var group = go.GetComponent<CanvasGroup>(); // ใช้จางทั้งป้าย
        group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false; // ซ่อนไว้ก่อน ไม่บังการกด

        if (songkranBanner) // โลโก้สงกรานต์พักใจด้านบนกล่อง
        {
            var logo = new GameObject("Logo", typeof(RectTransform), typeof(UnityEngine.UI.Image)); // รูปโลโก้
            var lrt = (RectTransform)logo.transform; lrt.SetParent(rt, false); // อยู่ในป้าย
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 1f); lrt.pivot = new Vector2(0.5f, 0.5f); // ยึดขอบบนกล่อง
            lrt.sizeDelta = new Vector2(640, 320); lrt.anchoredPosition = new Vector2(0, 10); // ขนาด/ตำแหน่ง (ทับขอบบนครึ่งหนึ่ง)
            var li = logo.GetComponent<UnityEngine.UI.Image>(); li.sprite = songkranBanner; li.preserveAspect = true; li.raycastTarget = false; // ใส่รูป
        }
        void Line(string text, float size, float y) // ฟังก์ชันย่อย: ข้อความ 1 บรรทัดในป้าย
        {
            var t = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)); // สร้างข้อความ
            var trt = (RectTransform)t.transform; trt.SetParent(rt, false); // อยู่ในป้าย
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f); trt.sizeDelta = new Vector2(920, size * 1.6f); trt.anchoredPosition = new Vector2(0, y); // ตำแหน่ง
            var tm = t.GetComponent<TextMeshProUGUI>(); // คอมโพเนนต์ข้อความ
            if (font) tm.font = font; // ฟอนต์ไทย
            tm.fontSize = size; tm.alignment = TextAlignmentOptions.Center; tm.raycastTarget = false; // ขนาด/จัดกลาง
            tm.color = new Color(0.29f, 0.17f, 0.42f); tm.text = text; // สีม่วงเข้ม + ข้อความ
        }
        Line($"คอมโบสูงสุด <color=#F57C00><b>x{best}</b></color>", 56, 20); // บรรทัด 1: คอมโบสูงสุด
        Line($"โบนัส <color=#2E9E3E><b>+{bonus:0}</b></color> เงิน", 46, -55); // บรรทัด 2: เงินโบนัส
        Line($"ทำงานรอบต่อไป ความสุขขึ้นง่ายขึ้น <color=#E8457A><b>+{HappyBuff * 100f:0}%</b></color>", 38, -125); // บรรทัด 3: บัฟความสุข
        if (handCursor) handCursor.transform.SetAsLastSibling(); // มืออยู่บนสุด
        PlayCoin(); // เสียงเหรียญตอนป้ายเด้ง

        for (float t = 0; t < popupDuration + summaryHold && go; t += Time.unscaledDeltaTime) // เด้งเข้า แล้วค้าง
        {
            float x = Mathf.Clamp01(t / popupDuration); // ความคืบหน้าการเด้ง
            float c1 = 1.70158f, c3 = c1 + 1f; // easeOutBack แบบเดียวกับโลโก้สงกรานต์
            float e = 1f + c3 * Mathf.Pow(x - 1f, 3) + c1 * Mathf.Pow(x - 1f, 2); // เด้งเกินนิดแล้วเข้าที่
            rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, e) * (1f + 0.02f * Mathf.Sin(t * 7f)); // เริ่มใหญ่เกือบเต็ม + ดุ๊กดิ๊กเบาๆ
            group.alpha = Mathf.Clamp01(t / 0.12f); // โผล่เร็ว
            yield return null; // รอเฟรมถัดไป
        }
        for (float t = 0; t < 0.4f && go; t += Time.unscaledDeltaTime) // จางออก
        {
            group.alpha = 1f - t / 0.4f; // ค่อยๆ หาย
            rt.localScale = Vector3.one * (1f + 0.1f * t / 0.4f); // ขยายออกนิดตอนหาย
            yield return null; // รอเฟรมถัดไป
        }
        if (go) Destroy(go); // ลบป้าย
    }

    void StartEvent()
    {
        InEvent = true; // เข้าสู่ Event
        Combo = 0; bestCombo = 0; comboTimer = 0; moneyDropped = 0; // เริ่มคอมโบใหม่ / นับไอคอนเงินใหม่
        if (spawner) { spawner.EventMode = true; spawner.ClearIcons(); } // ปล่อยเฉพาะไอคอนสงกรานต์ ล้างไอคอนเก่าบนจอ
        if (background && songkranBackground) // เปลี่ยนพื้นหลัง
        {
            originalBackground = background.sprite; originalBgScale = background.transform.localScale; // จำของเดิม
            Vector2 a = originalBackground ? (Vector2)originalBackground.bounds.size : (Vector2)songkranBackground.bounds.size; // ขนาดรูปเดิม
            Vector2 b = songkranBackground.bounds.size; // ขนาดรูปใหม่
            float s = Mathf.Max(a.x / b.x, a.y / b.y); // สเกลให้เต็มเท่าพื้นที่เดิม
            background.sprite = songkranBackground; // ใส่รูปสงกรานต์
            background.transform.localScale = new Vector3(originalBgScale.x * s, originalBgScale.y * s, originalBgScale.z); // ปรับสเกล
        }
        MusicPlayer.PlayTrack(SongkranMusic); // เปลี่ยนเป็นเพลงสงกรานต์
        if (stateText) stateText.text = ""; // ข้อความจะขึ้นหลังฉากเปลี่ยนจบ
        UpdateCharacter(); // เปลี่ยนเป็นชุดสงกรานต์
        if (handCursor && songkranHandOpen && songkranHandFist) // เปลี่ยนรูปมือเป็นชุดสงกรานต์
        {
            workHandOpen = handCursor.openSprite; workHandFist = handCursor.fistSprite; // จำรูปเดิม
            handCursor.openSprite = songkranHandOpen; handCursor.fistSprite = songkranHandFist; // ใส่รูปสงกรานต์
        }
    }

    void StopEvent()
    {
        InEvent = false; eventDone = true; // จบ Event
        if (spawner) { spawner.EventMode = false; spawner.ClearIcons(); } // กลับไปปล่อยไอคอนปกติ ล้างไอคอนสงกรานต์ที่ค้าง
        if (background && originalBackground) { background.sprite = originalBackground; background.transform.localScale = originalBgScale; } // คืนพื้นหลังเดิม
        bool hurry = timeLeft <= hurryTime; // เวลาใกล้หมดแล้วหรือยัง
        MusicPlayer.PlayTrack(hurry ? MusicPlayer.Hurry : MusicPlayer.Gameplay); // คืนเพลงเดิม
        if (hurry) hurryPlaying = true; // เปิดเพลงเร่งไปแล้ว
        if (stateText) stateText.text = ""; // ล้างข้อความ Event
        shownEventSeconds = -1; // รีเซ็ต
        UpdateCharacter(); // กลับเป็นตัวละครปกติ
        if (handCursor && workHandOpen) { handCursor.openSprite = workHandOpen; handCursor.fistSprite = workHandFist; } // คืนรูปมือเดิม
    }

    // ---- ข้อความ "สงกรานต์พักใจ! เงินไม่ลด เหลือ 25 วิ" ----
    void ShowEventText(bool force)
    {
        if (!stateText || Energy < lowThreshold || transitioning) return; // ไม่มีช่องข้อความ / เตือนพลังงานสำคัญกว่า / กำลังเปลี่ยนฉาก
        int s = Mathf.CeilToInt(Mathf.Max(0, eventStart + eventDuration - elapsed)); // วินาทีที่เหลือ
        if (!force && s == shownEventSeconds) return; // เปลี่ยนเฉพาะตอนวินาทีเปลี่ยน
        if (messageTimer > 0) return; // กำลังแสดงข้อความโบนัส
        shownEventSeconds = s; // จำไว้
        string combo = Combo > 1 ? $"  <color=#F57C00>คอมโบ x{Combo}</color>" : ""; // แสดงคอมโบถ้ามี
        stateText.text = $"<color=#1E88E5>สงกรานต์พักใจ! เหลือ {s} วิ</color>{combo}"; // แสดงข้อความ
    }

    // ---- เสียงเหรียญ (ไอคอนได้เงินช่วงสงกรานต์) ----
    public void PlayCoin() { if (sfx && coinClip) sfx.PlayOneShot(coinClip, 0.8f * sfxVolume); }

    // ---- ไอคอนสงกรานต์ตกพ้นจอโดยไม่ได้คว้า = คอมโบหลุด (IconSpawner เรียก) ----
    public void ComboMiss()
    {
        if (!InEvent || Combo == 0) return; // ไม่มีคอมโบอยู่
        Combo = 0; comboTimer = 0; // คอมโบหลุด
        ShowEventText(true); // อัปเดตข้อความ
    }

    // ---- กรอบหลังข้อความ "สงกรานต์พักใจ! เหลือ .. วิ คอมโบ x.." ให้อ่านง่าย (แสดงเฉพาะช่วง Event) ----
    UnityEngine.UI.Image eventTextBox; // กล่องพื้นหลังข้อความ
    void UpdateEventTextBox()
    {
        if (!stateText) return; // ไม่มีช่องข้อความ
        bool show = InEvent && !transitioning && !string.IsNullOrEmpty(stateText.text); // แสดงเมื่ออยู่ใน Event และมีข้อความ
        if (!eventTextBox) // สร้างครั้งแรก
        {
            if (!show) return;
            var go = new GameObject("EventTextBox", typeof(RectTransform), typeof(UnityEngine.UI.Image)); // สร้างกล่อง
            var rt = (RectTransform)go.transform; var srt = stateText.rectTransform; // RectTransform กล่อง / ข้อความ
            rt.SetParent(srt.parent, false); // อยู่ระดับเดียวกับข้อความ
            rt.SetSiblingIndex(srt.GetSiblingIndex()); // วาดก่อน (อยู่หลัง) ข้อความ
            rt.anchorMin = srt.anchorMin; rt.anchorMax = srt.anchorMax; rt.pivot = new Vector2(0.5f, 0.5f); // ยึดแบบเดียวกับข้อความ
            eventTextBox = go.GetComponent<UnityEngine.UI.Image>();
            eventTextBox.color = new Color(1f, 1f, 1f, 0.92f); // พื้นขาวเกือบทึบ
            eventTextBox.raycastTarget = false; // ไม่รับคลิก
            var o = go.AddComponent<UnityEngine.UI.Outline>(); // ขอบสีฟ้าน้ำ
            o.effectColor = new Color32(0x1E, 0x88, 0xE5, 0xFF); o.effectDistance = new Vector2(5, -5);
            var o2 = go.AddComponent<UnityEngine.UI.Outline>(); // ขอบอีกทิศ ให้หนารอบกล่อง
            o2.effectColor = o.effectColor; o2.effectDistance = new Vector2(-5, 5);
        }
        if (eventTextBox.gameObject.activeSelf != show) eventTextBox.gameObject.SetActive(show); // เปิด/ปิดกล่อง
        if (!show) return;
        var b = stateText.textBounds; // ขอบเขตตัวอักษรจริง (local)
        var r = (RectTransform)eventTextBox.transform; var s2 = stateText.rectTransform;
        r.sizeDelta = new Vector2(b.size.x + 48f, b.size.y + 20f); // ขนาดพอดีข้อความ + ระยะขอบ
        Vector2 center = (Vector2)s2.localPosition + (Vector2)b.center; // กลางข้อความ (local ของ parent)
        r.localPosition = center; // วางกล่องตรงข้อความ
    }

    // ---- เสียงตอนกำมือคว้าไอคอน: แต่ละประเภทเสียงไม่เหมือนกัน (IconSpawner เรียก) ----
    public void PlayGrab(IconSpawner.Kind kind)
    {
        if (kind == IconSpawner.Kind.Songkran) { PlaySplash(); return; } // สงกรานต์ = เสียงน้ำสาด
        var clip = kind == IconSpawner.Kind.Work ? workClip : kind == IconSpawner.Kind.Relax ? relaxClip : recoverClip; // เลือกเสียงตามประเภท
        if (sfx && clip) sfx.PlayOneShot(clip, 0.8f * sfxVolume); // เล่นเสียง
    }

    // ---- เสียงน้ำสาด (ใช้ตอนคว้าไอคอนสงกรานต์ และตอนป้ายเด้ง) ----
    public void PlaySplash()
    {
        if (sfx && splashClip) sfx.PlayOneShot(splashClip, 0.9f * sfxVolume); // เล่นเสียงทับกันได้
    }

    // ---------- ตัวช่วยของฉากเปลี่ยน ----------
    RectTransform HudRoot() // root canvas ของ HUD
    {
        var c = timerText ? timerText.canvas : null; // Canvas ที่ข้อความเวลาอยู่
        return c ? (RectTransform)c.rootCanvas.transform : null; // คืน root
    }

    // ---- สร้างชั้นสีเต็มจอ บนสุด (ใต้เคอร์เซอร์มือ) ----
    UnityEngine.UI.Image MakeOverlay(string name, Color color)
    {
        var root = HudRoot(); // root canvas
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)); // สร้างชั้น
        var rt = (RectTransform)go.transform; // RectTransform
        rt.SetParent(root, false); // ใส่ใต้ canvas
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; // เต็มจอ
        var img = go.GetComponent<UnityEngine.UI.Image>(); // Image
        img.color = new Color(color.r, color.g, color.b, 0f); // เริ่มโปร่งใส
        img.raycastTarget = false; // ไม่บังการกด
        if (handCursor) handCursor.transform.SetAsLastSibling(); // มืออยู่บนสุด
        return img; // คืนชั้น
    }

    // ---- ค่อยๆ เปลี่ยนความทึบของ Graphic (ไม่ขึ้นกับ timeScale) ----
    System.Collections.IEnumerator FadeAlpha(UnityEngine.UI.Graphic g, float from, float to, float dur)
    {
        for (float t = 0; t < dur && g; t += Time.unscaledDeltaTime) // วนจนครบเวลา
        {
            var c = g.color; c.a = Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, t / dur)); g.color = c; // ตั้งความทึบแบบนุ่ม
            yield return null; // รอเฟรมถัดไป
        }
        if (g) { var c = g.color; c.a = to; g.color = c; } // ตั้งค่าสุดท้ายให้ตรง
    }

    // ---- ถ่ายภาพฉากจากกล้องแบบความละเอียดต่ำ แล้วขยายเต็มจอ = ภาพเบลอ ----
    UnityEngine.UI.RawImage MakeBlurOverlay(UnityEngine.UI.Image hideWhileCapturing)
    {
        var cam = Camera.main; // กล้องหลัก
        var root = HudRoot(); // root canvas
        if (!cam || !root) return null; // ไม่มีกล้อง/canvas ก็ข้าม
        bool wasOn = hideWhileCapturing && hideWhileCapturing.enabled; // ซ่อนชั้นดำตอนถ่าย
        if (hideWhileCapturing) hideWhileCapturing.enabled = false;
        var full = RenderTexture.GetTemporary(960, 540, 24); // ถ่ายที่ครึ่งความละเอียด
        var prev = cam.targetTexture; // จำ target เดิม
        cam.targetTexture = full; cam.Render(); cam.targetTexture = prev; // ถ่ายภาพ
        if (hideWhileCapturing) hideWhileCapturing.enabled = wasOn; // เปิดชั้นดำคืน
        var mid = RenderTexture.GetTemporary(240, 135, 0); // ย่อ 2 ขั้นให้เบลอนุ่ม
        mid.filterMode = FilterMode.Bilinear;
        Graphics.Blit(full, mid); // 960 -> 240
        var small = new RenderTexture(96, 54, 0) { filterMode = FilterMode.Bilinear }; // ภาพเล็กสุด (ขยายแล้วเบลอ)
        Graphics.Blit(mid, small); // 240 -> 96
        RenderTexture.ReleaseTemporary(full); RenderTexture.ReleaseTemporary(mid); // คืนหน่วยความจำ

        var go = new GameObject("BlurOverlay", typeof(RectTransform), typeof(UnityEngine.UI.RawImage)); // ชั้นภาพเบลอ
        var rt = (RectTransform)go.transform; // RectTransform
        rt.SetParent(root, false); // ใส่ใต้ canvas
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; // เต็มจอ
        var img = go.GetComponent<UnityEngine.UI.RawImage>(); // RawImage
        img.texture = small; img.raycastTarget = false; // ใส่ภาพเบลอ
        if (hideWhileCapturing) hideWhileCapturing.transform.SetAsLastSibling(); // ชั้นดำอยู่บนภาพเบลอ
        if (handCursor) handCursor.transform.SetAsLastSibling(); // มืออยู่บนสุด
        return img; // คืนชั้นเบลอ
    }

    // ---- ป้าย "สงกรานต์พักใจ" ขนาดใหญ่ ต่ำกว่ากลางจอนิดหน่อย (เริ่มที่สเกล 0) ----
    GameObject MakeBanner()
    {
        var root = HudRoot(); // root canvas
        if (!root || !songkranBanner) return null; // ไม่มีป้ายก็ข้าม
        var go = new GameObject("SongkranBanner", typeof(RectTransform), typeof(CanvasGroup), typeof(UnityEngine.UI.Image)); // สร้างป้าย
        var rt = (RectTransform)go.transform; // RectTransform
        rt.SetParent(root, false); // ใส่ใต้ canvas
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f); // ยึดกลางจอ
        rt.sizeDelta = bannerSize; // ขนาดใหญ่
        rt.anchoredPosition = new Vector2(0, bannerY); // ขยับลงล่างนิดนึง
        rt.localScale = Vector3.one * 0.85f; // เริ่มที่ 85% (ซ่อนด้วย alpha 0 จนถึงตอนเด้ง)
        var img = go.GetComponent<UnityEngine.UI.Image>(); // Image
        img.sprite = songkranBanner; img.preserveAspect = true; img.raycastTarget = false; // ตั้งรูป
        var group = go.GetComponent<CanvasGroup>(); // ไม่บังการกด
        group.blocksRaycasts = false; group.interactable = false;
        group.alpha = 0f; // ซ่อนไว้ก่อน ไม่ให้เห็นป้ายเล็กตอนจอยังมืด
        if (handCursor) handCursor.transform.SetAsLastSibling(); // มืออยู่บนสุด
        return go; // คืนป้าย
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
        if (spawner) spawner.EventMode = false; // ปิดโหมด Event
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
