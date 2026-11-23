using UnityEngine; // ใช้คลาสหลักของ Unity (AudioSource, Resources ฯลฯ)
using UnityEngine.SceneManagement; // ใช้ SceneManager เพื่อรู้ว่าตอนนี้โหลดฉากไหนอยู่

// ตัวเล่นเพลงพื้นหลังของทุกฉาก สร้างตัวเองอัตโนมัติตอนเกมเริ่ม ไม่ต้องลากใส่ฉาก
// ไฟล์เพลงอยู่ที่ Assets/Sound/Resources/Music  เครดิตเพลงดูที่ Assets/Sound/CREDITS.txt
public class MusicPlayer : MonoBehaviour // คลาสจัดการเพลง BGM แบบ singleton
{
    public static MusicPlayer Instance { get; private set; } // ตัวแปร singleton ให้สคริปต์อื่นเรียกใช้ได้

    // ---- ชื่อไฟล์เพลงแต่ละแทร็ก ----
    // ถ้าเปลี่ยนชื่อไฟล์เพลงใน Resources/Music ต้องมาแก้ชื่อตรงนี้ให้ตรงกัน
    public const string Title = "BGM_Title"; // เพลงหน้าแรก
    public const string Tutorial = "BGM_Tutorial"; // เพลงหน้าสอนเล่น
    public const string Gameplay = "BGM_Gameplay"; // เพลงตอนเล่นเกม
    public const string GameOver = "BGM_GameOver"; // เพลงตอนแพ้
    public const string Victory = "BGM_Victory"; // เพลงตอนชนะ
    public const string Hurry = "BGM_Hurry"; // เพลงเร่งตอนเวลาใกล้หมด

    // ---- ค่าที่ปรับได้: ความดังและเวลาเฟดเพลง ----
    [Range(0, 1)] public float volume = 0.4f; // ความดังเพลง 0-1
    [Range(0, 1)] public float songkranVolume = 0.4f; // ความดังเพลงช่วง Event สงกรานต์ (ดังกว่าเพลงอื่น)
    float target; // ความดังของเพลงที่กำลังเล่น
    public float fadeTime = 0.6f; // เวลา (วินาที) ที่ใช้เฟดเพลงเก่าออก/เพลงใหม่เข้า

    // ---- ตัวแปรภายใน ----
    AudioSource source; // ตัวเล่นเสียงที่ใช้เล่นเพลง
    string current; // ชื่อเพลงที่กำลังเล่นอยู่ ใช้กันเล่นเพลงเดิมซ้ำ
    Coroutine fade; // coroutine ที่กำลังเฟดอยู่ เก็บไว้เพื่อหยุดได้ถ้ามีเพลงใหม่มาแทรก

    // ---- สร้าง MusicPlayer อัตโนมัติก่อนโหลดฉากแรก ----
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] // ให้ Unity เรียกฟังก์ชันนี้เองตอนเริ่มเกม
    static void Boot()
    {
        if (Instance) return; // ถ้ามีอยู่แล้วไม่ต้องสร้างซ้ำ
        var go = new GameObject("MusicPlayer"); // สร้าง GameObject เปล่าสำหรับเล่นเพลง
        DontDestroyOnLoad(go); // ไม่ให้ถูกลบตอนเปลี่ยนฉาก เพลงจะได้เล่นต่อเนื่อง
        Instance = go.AddComponent<MusicPlayer>(); // ใส่คอมโพเนนต์นี้แล้วเก็บเป็น Instance
    }

    // ---- ตั้งค่า AudioSource และผูก event เปลี่ยนฉาก ----
    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>(); // เพิ่ม AudioSource ไว้เล่นเพลง
        source.playOnAwake = false; // ยังไม่เล่นทันที รอสั่งเล่นเอง
        source.loop = true; // เพลงพื้นหลังให้วนซ้ำ
        source.volume = 0f; // เริ่มที่เสียง 0 แล้วค่อยเฟดขึ้น
        SceneManager.sceneLoaded += OnSceneLoaded; // ทุกครั้งที่โหลดฉากใหม่ ให้เรียก OnSceneLoaded
    }

    // ใช้ฟังก์ชันที่มีชื่อแทน lambda เพราะต้องถอดออกใน OnDestroy ได้
    // โปรเจกต์เปิด "Enter Play Mode Options" (ไม่ reload domain) ถ้าใช้ lambda ค้างไว้ใน event แบบ static
    // มันจะติดไปถึงรอบ Play ถัดไป แล้วไปเรียกออบเจกต์ที่ถูกลบไปแล้ว
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayForScene(scene.name); // เลือกเพลงตามชื่อฉากที่เพิ่งโหลด
    }

    // ---- ล้างค่าเมื่อออบเจกต์ถูกลบ ----
    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded; // ถอด event ออก กัน error ตอน Play รอบถัดไป
        if (Instance == this) Instance = null; // เคลียร์ singleton ถ้าเป็นตัวเรา
    }

    // ---- จับคู่ชื่อฉาก กับเพลงที่จะเล่น ----
    // ถ้าเพิ่มฉากใหม่แล้วอยากให้มีเพลง ให้เพิ่ม case ตรงนี้
    void PlayForScene(string scene)
    {
        switch (scene) // เช็คชื่อฉาก
        {
            case "Title": Play(Title); break; // ฉาก Title เล่นเพลงหน้าแรก
            case "Tutorial": Play(Tutorial); break; // ฉาก Tutorial เล่นเพลงสอนเล่น
            case "Gameplay": Play(Gameplay); break; // ฉาก Gameplay เล่นเพลงในเกม
        }
    }

    // ฟังก์ชัน static ไว้ให้สคริปต์อื่นเรียกได้เลย ไม่ต้องเช็ค null เอง
    public static void PlayTrack(string name, bool loop = true)
    {
        if (Instance) Instance.Play(name, loop); // ถ้ามี MusicPlayer อยู่ก็สั่งเล่น
    }

    // ---- สั่งเล่นเพลงตามชื่อ (เฟดเปลี่ยนเพลง) ----
    public void Play(string name, bool loop = true)
    {
        if (name == current && source.isPlaying) return; // เพลงเดิมเล่นอยู่แล้ว ไม่ต้องเริ่มใหม่
        var clip = Resources.Load<AudioClip>("Music/" + name); // โหลดไฟล์เพลงจากโฟลเดอร์ Resources/Music
        if (!clip) { Debug.LogWarning("MusicPlayer: missing clip " + name); return; } // หาไฟล์ไม่เจอ แจ้งเตือนแล้วออก
        current = name; // จำชื่อเพลงปัจจุบัน
        if (fade != null) StopCoroutine(fade); // ถ้ากำลังเฟดเพลงก่อนหน้าอยู่ ให้หยุดก่อน
        target = name == "BGM_Songkran" ? songkranVolume : volume; // เพลงสงกรานต์ดังกว่า
        fade = StartCoroutine(Swap(clip, loop)); // เริ่มเฟดเปลี่ยนเป็นเพลงใหม่
    }

    // ---- coroutine เฟดเพลงเก่าออก แล้วเฟดเพลงใหม่เข้า ----
    System.Collections.IEnumerator Swap(AudioClip clip, bool loop)
    {
        for (float v = source.volume; v > 0f && source.isPlaying; v -= Time.unscaledDeltaTime / fadeTime * Mathf.Max(volume, source.volume)) // ค่อยๆ ลดเสียงลงจนเป็น 0
        {
            source.volume = v; // ตั้งความดังตามรอบนี้
            yield return null; // รอเฟรมถัดไป
        }
        source.clip = clip; // เปลี่ยนเป็นเพลงใหม่
        source.loop = loop; // ตั้งว่าจะวนเพลงหรือไม่
        source.Play(); // เริ่มเล่นเพลงใหม่
        for (float v = 0f; v < target; v += Time.unscaledDeltaTime / fadeTime * target) // ค่อยๆ เพิ่มเสียงขึ้นจนถึงความดังของเพลงนี้
        {
            source.volume = v; // ตั้งความดังตามรอบนี้
            yield return null; // รอเฟรมถัดไป
        }
        source.volume = target; // จบแล้วตั้งความดังให้ตรงค่าเป๊ะ
    }
}
