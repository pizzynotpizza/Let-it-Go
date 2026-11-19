using System; // ใช้ Exception, Array
using System.Diagnostics; // ใช้ Process สำหรับเปิดโปรแกรม Python
using System.IO; // ใช้ File, Path, Directory
using System.Net; // ใช้ IPEndPoint, IPAddress
using System.Net.Sockets; // ใช้ UdpClient รับส่งข้อมูลผ่าน UDP
using System.Text; // ใช้ Encoding แปลง byte เป็นข้อความ
using System.Threading; // ใช้ Thread สำหรับรับข้อมูลเบื้องหลัง
using UnityEngine; // ใช้คลาสหลักของ Unity
using UnityEngine.InputSystem; // ใช้ Input System อ่านเมาส์/คีย์บอร์ด
using Debug = UnityEngine.Debug; // ให้ Debug หมายถึงของ Unity (ไม่ชนกับ System.Diagnostics.Debug)

// เปิดตัวจับมือ MediaPipe ที่เขียนด้วย Python แล้วรับข้อมูลมือผ่าน UDP
// ข้อความแต่ละเฟรม: "detected,x,y,openness,gesture[,พิกัด x,y ของ 21 จุดบนมือ]" (ดู StreamingAssets/HandTracker/hand_tracker_core.py)
// หน้า Title จะสร้างตัวนี้ด้วย Ensure() ตอนกด Start ทำให้ tracker พร้อมแล้วตอนเข้า Tutorial กับ Gameplay
// (ออบเจกต์นี้ไม่ถูกลบตอนเปลี่ยนฉาก)
//
// ค่าที่ส่วนอื่นของเกมใช้:
//   Detected     - เห็นมือในกล้องหรือไม่
//   ScreenPos01  - ตำแหน่งมือที่ทำให้นุ่มแล้ว ช่วง 0..1 (x ซ้าย->ขวา, y ล่าง->บน)
//   IsFist       - กำมืออยู่หรือไม่ (ใช้ threshold ที่ calibrate ไว้ + hysteresis)
//   FistDown     - true เฉพาะเฟรมที่เพิ่งกำมือ
//
// ถ้า Python ไม่ทำงาน (หรือเปิด useMouseFallback) จะใช้เมาส์แทนมือ:
// ขยับเมาส์ = ขยับมือ, กดคลิกซ้ายค้าง = กำมือ ใช้เทสต์ตอนไม่มีกล้องได้
// โหมดเมาส์ (กด M, บันทึกใน PlayerPrefs): ไม่เปิด Python กล้องจะไม่ถูกเปิด
// กด M อีกครั้งเพื่อเปิด tracker แล้วเล่นด้วยมือ
[DefaultExecutionOrder(-100)] // ให้สคริปต์นี้ Update ก่อนสคริปต์อื่น ข้อมูลมือจะพร้อมก่อนถูกใช้
public class HandTrackingClient : MonoBehaviour // คลาสรับข้อมูลมือจาก Python
{
    public static HandTrackingClient Instance { get; private set; } // singleton ให้สคริปต์อื่นเรียกใช้

    // ---- คืนตัวที่ทำงานอยู่ ถ้ายังไม่มีให้สร้างใหม่ด้วยค่า default ----
    public static HandTrackingClient Ensure()
    {
        if (Instance != null) return Instance; // มีอยู่แล้วใช้เลย
        var found = FindAnyObjectByType<HandTrackingClient>(); // ลองหาในฉาก
        if (found != null) return found; // เจอก็ใช้ตัวนั้น
        return new GameObject("HandTrackingClient").AddComponent<HandTrackingClient>(); // ไม่เจอเลยสร้างใหม่
    }

    // ---- ตั้งค่าการเปิด Python และพอร์ต UDP ----
    [Header("Python")] // หัวข้อใน Inspector
    public bool launchPython = true; // ให้ Unity เปิด Python เองไหม
    [Tooltip("Relative to the Unity project folder (parent of Assets).")] // คำอธิบายใน Inspector
    public string venvPython = "HandTrackerEnv/Scripts/python.exe"; // path ของ python.exe ใน venv (นับจากโฟลเดอร์โปรเจกต์)
    public string scriptRelativeToStreamingAssets = "HandTracker/hand_tracker.py"; // path ของสคริปต์ tracker ใน StreamingAssets
    public bool showPythonPreviewWindow = false; // เปิดหน้าต่างพรีวิวกล้องของ Python ไหม (ไว้ debug)
    public int receivePort = 5065; // พอร์ตที่ Unity รับข้อมูลมือ
    public int heartbeatPort = 5066; // พอร์ตที่ส่ง heartbeat ไปบอก Python ว่าเกมยังเปิดอยู่

    // ---- การแปลงตำแหน่งมือในกล้อง เป็นตำแหน่งบนจอ + ความนุ่มของเคอร์เซอร์ ----
    [Header("Mapping")] // หัวข้อใน Inspector
    [Tooltip("Camera border ignored at the left and right. Larger = a smaller left/right hand move covers the whole screen width.")] // คำอธิบายใน Inspector
    [Range(0f, 0.45f)] public float edgeMargin = 0.3f; // ตัดขอบกล้องซ้ายขวาทิ้ง ยิ่งมาก ขยับมือนิดเดียวก็ไปสุดจอ
    [Tooltip("Camera border ignored at the top and bottom. Larger = a smaller up/down hand move covers the whole screen height.")] // คำอธิบายใน Inspector
    [Range(0f, 0.45f)] public float edgeMarginY = 0.3f; // ตัดขอบกล้องบนล่างทิ้ง ยิ่งมาก ขยับขึ้นลงนิดเดียวก็สุดจอ
    [Tooltip("Smoothing while the hand is still (low = steady, less jitter).")] // คำอธิบายใน Inspector
    [Range(1f, 40f)] public float followSharpness = 14f; // ความไวตอนมือนิ่ง (ต่ำ = นิ่ง ไม่สั่น)
    [Tooltip("Smoothing while the hand moves fast (high = less lag).")] // คำอธิบายใน Inspector
    [Range(1f, 80f)] public float fastSharpness = 30f; // ความไวตอนมือขยับเร็ว (สูง = หน่วงน้อย)
    [Tooltip("Hand speed (screen widths per second) that uses fastSharpness.")] // คำอธิบายใน Inspector
    public float fastSpeed = 1.5f; // ความเร็วมือ (ความกว้างจอ/วินาที) ที่ถือว่าเร็วเต็มที่
    [Tooltip("Multiplier on follow speed. GameManager lowers it while the player is stressed.")] // คำอธิบายใน Inspector
    public float speedMultiplier = 1f; // ตัวคูณความไวเคอร์เซอร์ (GameManager ปรับได้)

    // ---- โหมดสำรองใช้เมาส์ ----
    [Header("Fallback")] // หัวข้อใน Inspector
    public bool useMouseFallback = false; // บังคับใช้เมาส์แทนมือ
    [Tooltip("Seconds without UDP data before the mouse takes over.")] // คำอธิบายใน Inspector
    public float dataTimeout = 1.5f; // ไม่มีข้อมูลจาก Python นานกี่วินาทีถึงสลับไปใช้เมาส์

    // ---- ค่า calibrate ความแบ/กำมือ (บันทึกใน PlayerPrefs) ----
    [Header("Calibration (saved in PlayerPrefs)")] // หัวข้อใน Inspector
    public float openValue = 1.9f; // ค่า openness ตอนแบมือ
    public float fistValue = 1.0f; // ค่า openness ตอนกำมือ
    [Range(0.05f, 0.45f)] public float hysteresis = 0.15f; // ช่วงกันสถานะกระพริบ (กำ/แบ สลับไปมา)

    // ---- ค่าการตัดสินว่ากำมือ เมื่อ tracker ส่ง gesture มาด้วย ----
    [Header("Fist with gestures")] // หัวข้อใน Inspector
    [Tooltip("Openness below this counts as a fist even if the gesture is not Closed_Fist yet.")] // คำอธิบายใน Inspector
    public float gestureFistOpenness = 1.35f; // openness ต่ำกว่านี้ถือว่ากำ แม้ gesture ยังไม่ขึ้น Closed_Fist
    [Tooltip("Openness above this counts as open (release).")] // คำอธิบายใน Inspector
    public float gestureOpenOpenness = 1.6f; // openness สูงกว่านี้ถือว่าแบมือ (ปล่อย)
    [Tooltip("Hand must look open this long before the fist releases.")] // คำอธิบายใน Inspector
    public float releaseDelay = 0.08f; // ต้องแบมือค้างนานเท่านี้ถึงจะนับว่าปล่อย
    [Tooltip("Keep the fist this long when the hand is lost for a moment.")] // คำอธิบายใน Inspector
    public float lostHold = 0.4f; // มือหายจากกล้องชั่วคราว ยังถือว่ากำอยู่ได้นานเท่านี้
    [Tooltip("Hand counts as found this long after the tracker loses it (stops the cursor blinking).")] // คำอธิบายใน Inspector
    public float detectGrace = 0.35f; // มือหายแป๊บเดียวยังถือว่าเจออยู่ กันเคอร์เซอร์กระพริบ
    [Tooltip("Longest time (s) the cursor takes to glide to a new tracker position.")] // คำอธิบายใน Inspector
    public float maxInterp = 0.06f; // เวลาสูงสุดที่เคอร์เซอร์ใช้เลื่อนไปตำแหน่งใหม่
    Vector2 prevRaw, interpFrom, interpPos; // ตำแหน่งดิบล่าสุด, จุดเริ่ม interpolate, ตำแหน่งที่ interpolate แล้ว
    float interpTime = 0.035f; // ระยะเวลา interpolate รอบปัจจุบัน
    double prevRawTime; // เวลาที่ได้ตำแหน่งดิบล่าสุด
    float openTimer; // นับเวลาที่มือแบค้าง (ใช้กับ releaseDelay)
    float lostTimer = 999f; // นับเวลาที่มือหายไป

    // ---- ค่าสถานะมือที่สคริปต์อื่นอ่านได้ ----
    public bool Detected { get; private set; } // เจอมือไหม
    public Vector2 ScreenPos01 { get; private set; } = new Vector2(0.5f, 0.5f); // ตำแหน่งมือ 0-1 (เริ่มกลางจอ)
    public float RawOpenness { get; private set; } // ค่าความแบมือดิบจาก tracker
    public bool IsFist { get; private set; } // กำมืออยู่ไหม
    public bool FistDown { get; private set; } // เพิ่งกำมือในเฟรมนี้
    public bool FistUp { get; private set; } // เพิ่งแบมือในเฟรมนี้
    public bool UsingMouse { get; private set; } // กำลังใช้เมาส์แทนมือไหม
    // ชื่อท่ามือจาก MediaPipe (Closed_Fist, Open_Palm, None, ...) เป็น null ถ้า tracker เวอร์ชันเก่า
    public string Gesture { get; private set; } // ท่ามือล่าสุด
    // tracker ส่ง gesture มาด้วย: ใช้ Closed_Fist เป็นสัญญาณคว้า ไม่ต้อง calibrate
    public bool HasGestures => !UsingMouse && Gesture != null; // มีข้อมูล gesture ไหม
    public bool IsCalibrated => PlayerPrefs.HasKey(PrefOpen); // เคย calibrate แล้วหรือยัง
    // ค่าสำหรับ debug: สถานะมือใน packet ล่าสุด (ไม่มีช่วง grace) และจำนวน packet ที่ได้รับ
    public bool RawDetected { get; private set; } // เจอมือใน packet ล่าสุดไหม
    public int PacketCount => packetCount; // จำนวน packet ทั้งหมด
    volatile int packetCount; // ตัวนับ packet (volatile เพราะถูกแก้จาก thread อื่น)

    // ---- ชื่อ key ที่ใช้เก็บใน PlayerPrefs ----
    const string PrefOpen = "hand_open"; // key ค่าแบมือ
    const string PrefFist = "hand_fist"; // key ค่ากำมือ
    const string PrefMouse = "hand_use_mouse"; // key โหมดเมาส์

    // true เมื่อผู้เล่นเลือกใช้เมาส์: จะไม่เปิด tracker (และกล้อง)
    public bool MouseMode => useMouseFallback; // อยู่ในโหมดเมาส์ไหม

    // ---- เปิด/ปิดโหมดเมาส์ (กด M) ----
    public void SetMouseMode(bool on)
    {
        useMouseFallback = on; // ตั้งโหมด
        PlayerPrefs.SetInt(PrefMouse, on ? 1 : 0); // บันทึกไว้ เปิดเกมครั้งหน้ายังจำได้
        PlayerPrefs.Save(); // เขียนลงดิสก์
        if (on) StopPython(); // โหมดเมาส์: ปิด Python (ปิดกล้อง)
        else TryStartPython(); // โหมดมือ: เปิด Python
        Debug.Log("[Hand] Mouse mode " + (on ? "ON - webcam off" : "OFF - starting tracker")); // log สถานะ
    }

    // ---- จุดบนมือ 21 จุดของ MediaPipe ช่วง 0..1 (x ซ้าย->ขวา, y ล่าง->บน) ใช้ได้เมื่อ HasLandmarks ----
    public const int LandmarkCount = 21; // จำนวนจุดบนมือ
    public Vector2[] Landmarks { get; } = new Vector2[LandmarkCount]; // พิกัดจุดบนมือ (ฝั่ง main thread)
    public bool HasLandmarks { get; private set; } // มีข้อมูลจุดบนมือไหม
    readonly Vector2[] recvLandmarks = new Vector2[LandmarkCount]; // บัฟเฟอร์ที่ thread รับข้อมูลใช้
    readonly Vector2[] latestLandmarks = new Vector2[LandmarkCount]; // ค่าล่าสุดที่รอส่งต่อให้ main thread
    bool latestHasLandmarks; // ค่าล่าสุดมีจุดบนมือไหม

    // ---- ตัวแปรภายในสำหรับ UDP / thread / Python ----
    UdpClient udp; // ตัวรับ UDP
    Thread recvThread; // thread รับข้อมูล
    volatile bool running; // thread ยังทำงานอยู่ไหม
    readonly object lockObj = new object(); // ตัวล็อกกันสอง thread แก้ข้อมูลพร้อมกัน
    bool latestDetected; // เจอมือใน packet ล่าสุด
    Vector2 latestPos; // ตำแหน่งมือใน packet ล่าสุด
    float latestOpen; // openness ใน packet ล่าสุด
    string latestGesture; // gesture ใน packet ล่าสุด
    double lastPacketTime = -999; // เวลาที่ได้ packet ล่าสุด
    double nowTime; // เวลาปัจจุบัน (อัปเดตใน Update ให้ thread อ่าน)
    Process python; // process ของ Python ที่เปิดไว้
    UdpClient heartbeat; // ตัวส่ง heartbeat
    float nextPing; // เวลาส่ง heartbeat ครั้งถัดไป

    // fps = ความถี่จอ (VSync) ถ้าล็อก 60 fps บนจอ 144 Hz เฟรมจะค้างบนจอไม่เท่ากัน (2,3,2,3 รอบรีเฟรช)
    // ทำให้ของที่ขยับดูกระตุก ถ้าไม่ล็อกเลย (~200 fps) ก็เปลือง CPU ที่ MediaPipe ต้องใช้ ใช้ VSync แก้ได้ทั้งสองอย่าง
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] // ให้ Unity เรียกเองก่อนโหลดฉากแรก
    static void SyncFrameRate()
    {
        QualitySettings.vSyncCount = 1; // เปิด VSync
        Application.targetFrameRate = -1; // ไม่ล็อก fps เอง ปล่อยให้ตาม VSync
    }

    // ---- เริ่มต้น: ตั้ง singleton โหลดค่าที่บันทึกไว้ เปิดตัวรับข้อมูลและ Python ----
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; } // มีตัวอื่นอยู่แล้ว ลบตัวซ้ำทิ้ง
        Instance = this; // ตั้ง singleton
        DontDestroyOnLoad(gameObject); // ไม่ลบตอนเปลี่ยนฉาก

        if (PlayerPrefs.HasKey(PrefOpen)) // เคย calibrate ไว้
        {
            openValue = PlayerPrefs.GetFloat(PrefOpen); // โหลดค่าแบมือ
            fistValue = PlayerPrefs.GetFloat(PrefFist); // โหลดค่ากำมือ
        }

        if (PlayerPrefs.HasKey(PrefMouse)) useMouseFallback = PlayerPrefs.GetInt(PrefMouse) == 1; // โหลดโหมดเมาส์ที่เลือกไว้

        StartReceiver(); // เปิดตัวรับ UDP
        if (!useMouseFallback) TryStartPython(); // ไม่ใช่โหมดเมาส์ เปิด Python
        heartbeat = new UdpClient(); // สร้างตัวส่ง heartbeat
    }

    // ---- เปิด Python ถ้ายังไม่มี tracker ทำงานอยู่ ----
    void TryStartPython()
    {
        if (!launchPython || (python != null && !python.HasExited)) return; // ปิดการเปิด Python หรือเปิดอยู่แล้ว
        // run_hand_tracker.py (ที่เปิดก่อน Unity) จะจองพอร์ต heartbeat ไว้
        // ถ้าพอร์ตถูกใช้อยู่ ให้ใช้ tracker ตัวนั้น ไม่เปิดตัวที่สองซ้อน
        if (PortInUse(heartbeatPort)) // พอร์ตถูกใช้แล้ว
            Debug.Log("[Hand] External tracker already running - not starting Python."); // แจ้งว่าใช้ tracker ภายนอก
        else // ยังไม่มี tracker
            StartPython(); // เปิด Python
    }

    // ---- ปิด Python ----
    void StopPython()
    {
        try { if (python != null && !python.HasExited) python.Kill(); } catch { } // kill process ถ้ายังทำงาน (ไม่สนใจ error)
        python = null; // เคลียร์ตัวแปร
    }

    // ---- เช็คว่าพอร์ต UDP ถูกใช้อยู่ไหม (ลอง bind ดู) ----
    static bool PortInUse(int port)
    {
        try
        {
            using (new UdpClient(new IPEndPoint(IPAddress.Loopback, port))) { } // ลองเปิดพอร์ตแล้วปิดทันที
            return false; // เปิดได้ = ว่าง
        }
        catch (SocketException) // เปิดไม่ได้
        {
            return true; // มีคนใช้อยู่
        }
    }

    // ---- เปิดตัวรับ UDP และ thread รับข้อมูล ----
    void StartReceiver()
    {
        try
        {
            udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, receivePort)); // เปิดพอร์ตรับบน localhost
            running = true; // ตั้งสถานะทำงาน
            recvThread = new Thread(ReceiveLoop) { IsBackground = true }; // สร้าง thread เบื้องหลัง (ปิดเกมแล้วจบเอง)
            recvThread.Start(); // เริ่ม thread
        }
        catch (Exception e) // เปิดพอร์ตไม่ได้
        {
            Debug.LogWarning("[Hand] Cannot open UDP port " + receivePort + ": " + e.Message); // แจ้งเตือน
        }
    }

    // ---- loop รับข้อมูลใน thread แยก (ไม่บล็อกเกม) ----
    void ReceiveLoop()
    {
        var ep = new IPEndPoint(IPAddress.Any, 0); // รับจากต้นทางไหนก็ได้
        while (running) // วนจนกว่าจะสั่งหยุด
        {
            try
            {
                byte[] data = udp.Receive(ref ep); // รอรับ packet (บล็อกจนกว่าจะมีข้อมูล)
                string[] p = Encoding.ASCII.GetString(data).Split(','); // แปลงเป็นข้อความแล้วแยกด้วย comma
                if (p.Length < 4) continue; // ข้อมูลไม่ครบ ข้าม
                var inv = System.Globalization.CultureInfo.InvariantCulture; // ใช้จุดทศนิยมแบบสากล (กันเครื่องที่ใช้ comma)
                bool det = p[0] == "1"; // ช่องที่ 1: เจอมือไหม
                float x = float.Parse(p[1], inv); // ช่องที่ 2: ตำแหน่ง x
                float y = float.Parse(p[2], inv); // ช่องที่ 3: ตำแหน่ง y
                float o = float.Parse(p[3], inv); // ช่องที่ 4: ความแบมือ
                string g = p.Length >= 5 ? p[4].Trim() : null; // ช่องที่ 5: ชื่อ gesture (ถ้ามี)
                bool hasPts = det && p.Length >= 5 + LandmarkCount * 2; // มีพิกัด 21 จุดครบไหม
                if (hasPts) // มีจุดบนมือ
                    for (int i = 0; i < LandmarkCount; i++) // อ่านทีละจุด
                        recvLandmarks[i] = new Vector2(float.Parse(p[5 + i * 2], inv), 1f - float.Parse(p[6 + i * 2], inv)); // กลับแกน y ให้ล่าง->บน แบบ Unity
                lock (lockObj) // ล็อกก่อนเขียนค่าที่ main thread จะอ่าน
                {
                    latestDetected = det; // เก็บสถานะเจอมือ
                    if (det) { latestPos = new Vector2(x, 1f - y); latestOpen = o; } // เจอมือ: เก็บตำแหน่ง (กลับแกน y) และ openness
                    if (hasPts) { Array.Copy(recvLandmarks, latestLandmarks, LandmarkCount); latestHasLandmarks = true; } // คัดลอกจุดบนมือ
                    else if (!det) latestHasLandmarks = false; // ไม่เจอมือ ไม่มีจุด
                    latestGesture = g; // เก็บ gesture
                    lastPacketTime = nowTime; // จำเวลาที่ได้ packet
                    packetCount++; // นับ packet
                }
            }
            catch (SocketException) { } // error ของ socket ชั่วคราว ข้ามไป
            catch (ObjectDisposedException) { break; } // socket ถูกปิดแล้ว ออกจาก loop
            catch (FormatException) { } // ข้อมูลแปลงเป็นตัวเลขไม่ได้ ข้าม packet นี้
        }
    }

    // venv ใช้ได้แค่บนเครื่องที่สร้างมัน: pyvenv.cfg จะระบุโฟลเดอร์ Python ของเครื่องนั้น ("home")
    // ถ้าโฟลเดอร์นั้นไม่มีในเครื่องนี้ python.exe จะเปิดไม่ได้
    static bool VenvUsable(string exe)
    {
        if (!File.Exists(exe)) return false; // ไม่มี python.exe
        string cfg = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(exe)), "pyvenv.cfg"); // path ของ pyvenv.cfg (ถอยออกจาก Scripts)
        if (!File.Exists(cfg)) return true; // ไม่มีไฟล์ cfg ถือว่าใช้ได้
        foreach (string line in File.ReadAllLines(cfg)) // อ่านทีละบรรทัด
        {
            int eq = line.IndexOf('='); // หาเครื่องหมาย =
            if (eq < 0 || line.Substring(0, eq).Trim() != "home") continue; // ไม่ใช่บรรทัด home ข้าม
            return Directory.Exists(line.Substring(eq + 1).Trim()); // โฟลเดอร์ Python ต้นทางมีอยู่จริงไหม
        }
        return true; // ไม่เจอบรรทัด home ถือว่าใช้ได้
    }

    // เช็ค venvPython ก่อน แล้วค่อยหา venv ของแต่ละเครื่องที่สร้างจาก run_hand_tracker_portable.py
    // (HandTrackerEnv-<ชื่อเครื่อง>) คืน null ถ้าไม่มีอันไหนใช้ได้บนเครื่องนี้
    string FindUsableVenvPython(string projectDir)
    {
        string exe = Path.Combine(projectDir, venvPython); // path venv หลัก
        if (VenvUsable(exe)) return exe; // ใช้ได้ก็คืนเลย
        try
        {
            foreach (string dir in Directory.GetDirectories(projectDir, "HandTrackerEnv*")) // หาโฟลเดอร์ venv อื่นๆ
            {
                string candidate = Path.Combine(dir, "Scripts", "python.exe"); // python.exe ในโฟลเดอร์นั้น
                if (VenvUsable(candidate)) return candidate; // ใช้ได้ก็คืน
            }
        }
        catch (Exception) { } // อ่านโฟลเดอร์ไม่ได้ ข้าม
        return null; // ไม่เจอ venv ที่ใช้ได้
    }

    // ---- เปิด process Python ของตัวจับมือ ----
    void StartPython()
    {
        string projectDir = Path.GetDirectoryName(Application.dataPath); // โฟลเดอร์โปรเจกต์ (parent ของ Assets / _Data)
        string script = Path.Combine(Application.streamingAssetsPath, scriptRelativeToStreamingAssets); // path เต็มของสคริปต์ tracker
        string exe = FindUsableVenvPython(projectDir); // หา python.exe ที่ใช้ได้
        if (exe == null) // ไม่เจอ venv ที่ใช้ได้
        {
            Debug.LogError("[Hand] HandTrackerEnv is missing or was copied from another computer, so the hand tracker " // แจ้งวิธีแก้
                + "cannot start. On this computer run \"Start Hand Tracker (Portable).bat\" once, " // ให้รัน .bat ครั้งเดียว
                + "then press Play again. The mouse controls the hand until then."); // ระหว่างนั้นใช้เมาส์
            return; // ออก
        }
        if (!File.Exists(script)) // ไม่มีไฟล์สคริปต์
        {
            Debug.LogError("[Hand] Tracker script missing: " + script); // แจ้ง error
            return; // ออก
        }

        string args = $"\"{script}\" --port {receivePort} --listen {heartbeatPort}"; // argument: สคริปต์, พอร์ตส่งข้อมูล, พอร์ต heartbeat
        if (showPythonPreviewWindow) args += " --preview"; // เปิดหน้าต่างพรีวิวกล้อง

        try
        {
            var psi = new ProcessStartInfo(exe, args) // ตั้งค่าการเปิด process
            {
                UseShellExecute = false, // เปิดตรงๆ ไม่ผ่าน shell (จำเป็นสำหรับ redirect output)
                CreateNoWindow = true, // ไม่ต้องเปิดหน้าต่าง console
                RedirectStandardOutput = true, // ดึง output มาแสดงใน Console ของ Unity
                RedirectStandardError = true, // ดึง error มาด้วย
                WorkingDirectory = Path.GetDirectoryName(script) // ให้ทำงานในโฟลเดอร์ของสคริปต์ (หาไฟล์โมเดลเจอ)
            };
            python = new Process { StartInfo = psi, EnableRaisingEvents = true }; // สร้าง process
            python.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.Log(e.Data); }; // output ปกติ log ออก Console
            python.ErrorDataReceived += (s, e) => // output จาก stderr
            {
                if (string.IsNullOrEmpty(e.Data)) return; // ว่างก็ข้าม
                // MediaPipe / TFLite เขียนข้อความ info ปกติลง stderr ด้วย เลยกรองเฉพาะ error จริง
                if (e.Data.Contains("Error") || e.Data.Contains("Traceback")) Debug.LogWarning("[Hand py] " + e.Data); // แสดงเฉพาะ error
            };
            python.Start(); // เปิด Python
            python.BeginOutputReadLine(); // เริ่มอ่าน output
            python.BeginErrorReadLine(); // เริ่มอ่าน error
            Debug.Log("[Hand] Started tracker: " + exe + " " + args); // log ว่าเปิดแล้ว
        }
        catch (Exception e) // เปิดไม่สำเร็จ
        {
            Debug.LogWarning("[Hand] Cannot start python: " + e.Message + " - using mouse."); // แจ้งเตือน แล้วใช้เมาส์แทน
            python = null; // เคลียร์ตัวแปร
        }
    }

    // ---- สั่ง Python ให้ลืม Hand 1 มือถัดไปที่เข้ากล้องจะเป็นมือผู้เล่น ----
    // เรียกตอนเริ่มหน้าเช็คมือ และตอนจบเกม
    public static void ResetHandLock()
    {
        var c = Instance; // ดึงตัวที่ทำงานอยู่
        if (c == null || c.useMouseFallback || c.heartbeat == null) return; // ไม่มี tracker หรือใช้เมาส์ ไม่ต้องทำ
        try { c.heartbeat.Send(new byte[] { (byte)'R' }, 1, "127.0.0.1", c.heartbeatPort); } catch { } // ส่งตัวอักษร 'R' (Reset) ไปที่พอร์ต heartbeat
    }

    // ---- ทุกเฟรม: ส่ง heartbeat ดึงข้อมูลจาก thread ทำให้เคอร์เซอร์นุ่ม และตัดสินกำ/แบมือ ----
    void Update()
    {
        nowTime = Time.realtimeSinceStartupAsDouble; // อัปเดตเวลาปัจจุบันให้ thread รับข้อมูลใช้

        var kb = Keyboard.current; // ดึงคีย์บอร์ด
        if (kb != null && kb.mKey.wasPressedThisFrame) SetMouseMode(!useMouseFallback); // กด M สลับโหมดเมาส์/มือ

        // โหมดเมาส์ไม่ส่ง heartbeat: tracker ที่เกมเปิดไว้จะปิดตัวเองเมื่อไม่ได้ heartbeat
        if (!useMouseFallback && Time.unscaledTime >= nextPing) // ถึงเวลาส่ง heartbeat
        {
            nextPing = Time.unscaledTime + 1f; // ส่งทุก 1 วินาที
            try { heartbeat?.Send(new byte[] { 1 }, 1, "127.0.0.1", heartbeatPort); } catch { } // ส่ง 1 byte บอกว่าเกมยังเปิดอยู่
        }

        // ---- คัดลอกข้อมูลล่าสุดจาก thread รับข้อมูล (ภายใน lock) ----
        bool det; Vector2 raw; float open; double last; string gesture; // ตัวแปรเก็บสำเนา
        lock (lockObj) // ล็อกกันชนกับ thread
        {
            det = latestDetected; raw = latestPos; open = latestOpen; last = lastPacketTime; gesture = latestGesture; // คัดลอกค่า
            HasLandmarks = latestHasLandmarks; // มีจุดบนมือไหม
            if (latestHasLandmarks) Array.Copy(latestLandmarks, Landmarks, LandmarkCount); // คัดลอกจุดบนมือ
        }
        Gesture = gesture; // อัปเดต gesture
        RawDetected = det; // อัปเดตสถานะดิบ

        UsingMouse = useMouseFallback || nowTime - last > dataTimeout; // ใช้เมาส์ถ้าเลือกเอง หรือไม่มีข้อมูลนานเกิน timeout

        Vector2 target; // ตำแหน่งเป้าหมายของเคอร์เซอร์
        bool fistNow; // เฟรมนี้กำมืออยู่ไหม
        if (UsingMouse) // ---- โหมดเมาส์ ----
        {
            var mouse = Mouse.current; // ดึงเมาส์
            Vector2 m = mouse != null ? mouse.position.ReadValue() : new Vector2(Screen.width, Screen.height) * 0.5f; // ตำแหน่งเมาส์ (ไม่มีเมาส์ใช้กลางจอ)
            bool pressed = mouse != null && mouse.leftButton.isPressed; // กดคลิกซ้ายอยู่ไหม
            target = new Vector2(m.x / Screen.width, m.y / Screen.height); // แปลงเป็นช่วง 0-1
            Detected = true; // เมาส์ถือว่าเจอมือตลอด
            RawOpenness = pressed ? fistValue : openValue; // จำลองค่า openness
            fistNow = pressed; // คลิกค้าง = กำมือ
            ScreenPos01 = target; // ใช้ตำแหน่งเมาส์ตรงๆ ไม่ต้องทำให้นุ่ม
        }
        else // ---- โหมดมือจริง ----
        {
            // MediaPipe หามือไม่เจอบางเฟรมเป็นพักๆ (ขยับเร็ว, กำมือ, ภาพเบลอ)
            // เลยให้ถือว่ายังเจอมืออยู่ช่วงสั้นๆ เคอร์เซอร์จะได้ไม่กระพริบ
            Detected = det || lostTimer < detectGrace; // เจอมือ หรือเพิ่งหายไม่นาน
            if (det) // packet ล่าสุดเจอมือ
            {
                RawOpenness = open; // อัปเดต openness
                lostTimer = 0; // รีเซ็ตตัวนับมือหาย
                // tracker ส่งตำแหน่ง ~25-30 ครั้ง/วินาที แบบไม่สม่ำเสมอ แต่เกมวาดเฟรมมากกว่านั้น
                // เลย interpolate ระหว่างสองตำแหน่งล่าสุด ตามช่วงห่างของ packet (หน่วงประมาณ 1 packet ~35 ms)
                // เคอร์เซอร์จะขยับความเร็วสม่ำเสมอ ไม่กระโดดตอน packet มาถึง
                double now = Time.realtimeSinceStartupAsDouble; // เวลาปัจจุบัน
                if (raw != prevRaw) // ได้ตำแหน่งใหม่
                {
                    float gap = (float)(now - prevRawTime); // ช่วงห่างจาก packet ก่อนหน้า
                    // เริ่มจากตำแหน่งเคอร์เซอร์ตอนนี้ ถ้าห่างนานเกินไปให้กระโดดไปเลย
                    interpFrom = gap > 0.25f ? raw : interpPos; // จุดเริ่ม interpolate
                    interpTime = Mathf.Clamp(gap, 0.02f, maxInterp); // ระยะเวลา interpolate (จำกัด 0.02 - maxInterp)
                    prevRaw = raw; // จำตำแหน่งดิบล่าสุด
                    prevRawTime = now; // จำเวลา
                }
                float a = Mathf.Clamp01((float)(now - prevRawTime) / interpTime); // ความคืบหน้าของการ interpolate 0-1
                interpPos = Vector2.Lerp(interpFrom, raw, a); // ตำแหน่งที่ interpolate แล้ว
                Vector2 predicted = interpPos; // ตำแหน่งที่จะใช้
                float m = edgeMargin, my = edgeMarginY; // ขอบที่ตัดทิ้ง
                target = new Vector2(Mathf.InverseLerp(m, 1f - m, predicted.x), Mathf.InverseLerp(my, 1f - my, predicted.y)); // ขยายช่วงกลางกล้องให้เต็มจอ
                // smoothing แบบปรับตามความเร็ว: มือนิ่ง = นิ่ง, มือขยับเร็ว = แทบไม่หน่วง
                float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f); // เวลาระหว่างเฟรม (กันหารศูนย์)
                float speed = (target - ScreenPos01).magnitude / dt; // ความเร็วมือ
                float sharp = Mathf.Lerp(followSharpness, fastSharpness, Mathf.Clamp01(speed / fastSpeed)); // ความไวตามความเร็ว
                float k = 1f - Mathf.Exp(-sharp * speedMultiplier * dt); // สัดส่วน lerp ที่ไม่ขึ้นกับ fps
                ScreenPos01 = Vector2.Lerp(ScreenPos01, target, k); // ขยับเคอร์เซอร์เข้าหาเป้า
            }
            float mid = (openValue + fistValue) * 0.5f; // ค่ากึ่งกลางระหว่างแบกับกำ
            float band = (openValue - fistValue) * hysteresis; // ความกว้างช่วง hysteresis
            if (!det) // ---- มือหายจากกล้อง ----
            {
                // MediaPipe มักหามือที่กำอยู่ไม่เจอ 1-2 เฟรม: ให้ถือว่ายังกำอยู่ช่วงสั้นๆ
                lostTimer += Time.unscaledDeltaTime; // นับเวลาที่มือหาย
                fistNow = IsFist && lostTimer < lostHold; // ยังกำอยู่ถ้าก่อนหน้ากำและหายไม่นาน
            }
            else if (gesture != null) // ---- tracker ส่ง gesture มา ----
            {
                // ใช้สองสัญญาณร่วมกัน ถ้าอันใดอันหนึ่งช้าจะได้ไม่พลาดการคว้า:
                //   - Gesture Recognizer บอกว่า Closed_Fist หรือ
                //   - openness (ระยะปลายนิ้ว / ขนาดฝ่ามือ) ต่ำกว่า gestureFistOpenness
                // จะปล่อยเมื่อมือแบชัดเจนอีกครั้งเท่านั้น (hysteresis) และไม่สนใจการแบแป๊บเดียว กันกำมือกระพริบ
                bool closedSignal = gesture == "Closed_Fist" || open < gestureFistOpenness; // สัญญาณกำ
                bool openSignal = gesture == "Open_Palm" || open > gestureOpenOpenness; // สัญญาณแบ
                if (closedSignal) { fistNow = true; openTimer = 0; } // กำ: ตั้งเป็นกำ รีเซ็ตตัวนับแบ
                else if (IsFist) // ก่อนหน้ากำอยู่ และตอนนี้ไม่มีสัญญาณกำ
                {
                    openTimer = openSignal ? openTimer + Time.unscaledDeltaTime : 0; // นับเวลาแบค้าง
                    fistNow = openTimer < releaseDelay; // แบยังไม่นานพอ ถือว่ายังกำ
                }
                else fistNow = false; // ไม่ได้กำ
            }
            // tracker เวอร์ชันเก่าไม่มี gesture: ใช้ threshold จากการ calibrate + hysteresis
            else if (IsFist) fistNow = open < mid + band; // กำอยู่: ต้องแบเกินกึ่งกลาง+band ถึงจะปล่อย
            else fistNow = open < mid - band; // แบอยู่: ต้องกำต่ำกว่ากึ่งกลาง-band ถึงจะนับว่ากำ
        }

        FistDown = fistNow && !IsFist; // เพิ่งกำในเฟรมนี้
        FistUp = !fistNow && IsFist; // เพิ่งแบในเฟรมนี้
        IsFist = fistNow; // อัปเดตสถานะกำ
    }

    // ---- บันทึกค่า calibrate (TitleHandCheck เรียก) ----
    public void SaveCalibration(float open, float fist)
    {
        if (open - fist < 0.2f) // ค่าแบกับกำใกล้กันเกินไป แยกไม่ออก
        {
            Debug.LogWarning($"[Hand] Calibration range too small (open {open:F2}, fist {fist:F2}). Using defaults."); // แจ้งเตือน
            open = 1.9f; fist = 1.0f; // ใช้ค่า default แทน
        }
        openValue = open; // ตั้งค่าแบมือ
        fistValue = fist; // ตั้งค่ากำมือ
        PlayerPrefs.SetFloat(PrefOpen, open); // บันทึกค่าแบมือ
        PlayerPrefs.SetFloat(PrefFist, fist); // บันทึกค่ากำมือ
        PlayerPrefs.Save(); // เขียนลงดิสก์
        Debug.Log($"[Hand] Calibrated open={open:F2} fist={fist:F2}"); // log ค่าที่บันทึก
    }

    // ---- ปิดทุกอย่างเมื่อออบเจกต์ถูกลบหรือปิดเกม ----
    void OnDestroy()
    {
        if (Instance != this) return; // ไม่ใช่ตัวหลัก ไม่ต้องปิด
        Shutdown(); // ปิด UDP และ Python
    }

    void OnApplicationQuit() => Shutdown(); // ปิดเกมแล้วปิด UDP และ Python ด้วย

    void Shutdown()
    {
        running = false; // สั่ง thread รับข้อมูลให้หยุด
        try { udp?.Close(); } catch { } // ปิด socket รับ (ทำให้ Receive หลุดออก)
        udp = null; // เคลียร์ตัวแปร
        try { heartbeat?.Close(); } catch { } // ปิด socket heartbeat
        heartbeat = null; // เคลียร์ตัวแปร
        StopPython(); // ปิด Python (ปิดกล้อง)
    }
}
