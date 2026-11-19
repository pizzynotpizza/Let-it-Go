using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

// Starts the Python MediaPipe hand tracker and receives hand data over UDP.
// Message per frame: "detected,x,y,openness,gesture[,21 landmark x,y pairs]" (see StreamingAssets/HandTracker/hand_tracker_core.py).
// The Title scene creates the client with Ensure() when Start is pressed, so the tracker
// is already warm in Tutorial and Gameplay (the object survives scene loads).
//
// Gives the rest of the game:
//   Detected     - a hand is visible
//   ScreenPos01  - smoothed hand position, 0..1 (x left->right, y bottom->top)
//   IsFist       - hand closed, uses calibrated threshold with hysteresis
//   FistDown     - true only on the frame the hand closes
//
// If Python is not running (or useMouseFallback is on), the mouse drives the hand:
// move = hand, hold left button = fist. Good for testing without a webcam.
// Mouse mode (press M, saved in PlayerPrefs): Python is not started, so the webcam stays off.
// Press M again to start the tracker and play with the hand.
[DefaultExecutionOrder(-100)]
public class HandTrackingClient : MonoBehaviour
{
    public static HandTrackingClient Instance { get; private set; }

    // Returns the running client, or creates one with default settings.
    public static HandTrackingClient Ensure()
    {
        if (Instance != null) return Instance;
        var found = FindAnyObjectByType<HandTrackingClient>();
        if (found != null) return found;
        return new GameObject("HandTrackingClient").AddComponent<HandTrackingClient>();
    }

    [Header("Python")]
    public bool launchPython = true;
    [Tooltip("Relative to the Unity project folder (parent of Assets).")]
    public string venvPython = "HandTrackerEnv/Scripts/python.exe";
    public string scriptRelativeToStreamingAssets = "HandTracker/hand_tracker.py";
    public bool showPythonPreviewWindow = false;
    public int receivePort = 5065;
    public int heartbeatPort = 5066;

    [Header("Mapping")]
    [Tooltip("Camera border ignored at the left and right. Larger = a smaller left/right hand move covers the whole screen width.")]
    [Range(0f, 0.45f)] public float edgeMargin = 0.3f;
    [Tooltip("Camera border ignored at the top and bottom. Larger = a smaller up/down hand move covers the whole screen height.")]
    [Range(0f, 0.45f)] public float edgeMarginY = 0.3f;
    [Tooltip("Smoothing while the hand is still (low = steady, less jitter).")]
    [Range(1f, 40f)] public float followSharpness = 14f;
    [Tooltip("Smoothing while the hand moves fast (high = less lag).")]
    [Range(1f, 80f)] public float fastSharpness = 30f;
    [Tooltip("Hand speed (screen widths per second) that uses fastSharpness.")]
    public float fastSpeed = 1.5f;
    [Tooltip("Multiplier on follow speed. GameManager lowers it while the player is stressed.")]
    public float speedMultiplier = 1f;

    [Header("Fallback")]
    public bool useMouseFallback = false;
    [Tooltip("Seconds without UDP data before the mouse takes over.")]
    public float dataTimeout = 1.5f;

    [Header("Calibration (saved in PlayerPrefs)")]
    public float openValue = 1.9f;
    public float fistValue = 1.0f;
    [Range(0.05f, 0.45f)] public float hysteresis = 0.15f;

    [Header("Fist with gestures")]
    [Tooltip("Openness below this counts as a fist even if the gesture is not Closed_Fist yet.")]
    public float gestureFistOpenness = 1.35f;
    [Tooltip("Openness above this counts as open (release).")]
    public float gestureOpenOpenness = 1.6f;
    [Tooltip("Hand must look open this long before the fist releases.")]
    public float releaseDelay = 0.08f;
    [Tooltip("Keep the fist this long when the hand is lost for a moment.")]
    public float lostHold = 0.4f;
    [Tooltip("Hand counts as found this long after the tracker loses it (stops the cursor blinking).")]
    public float detectGrace = 0.35f;
    [Tooltip("Longest time (s) the cursor takes to glide to a new tracker position.")]
    public float maxInterp = 0.06f;
    Vector2 prevRaw, interpFrom, interpPos;
    float interpTime = 0.035f;
    double prevRawTime;
    float openTimer;
    float lostTimer = 999f;

    public bool Detected { get; private set; }
    public Vector2 ScreenPos01 { get; private set; } = new Vector2(0.5f, 0.5f);
    public float RawOpenness { get; private set; }
    public bool IsFist { get; private set; }
    public bool FistDown { get; private set; }
    public bool FistUp { get; private set; }
    public bool UsingMouse { get; private set; }
    // MediaPipe gesture name from the tracker (Closed_Fist, Open_Palm, None, ...). Null with an old tracker.
    public string Gesture { get; private set; }
    // True when the tracker sends gestures. Then Closed_Fist is the grab signal and no calibration is needed.
    public bool HasGestures => !UsingMouse && Gesture != null;
    public bool IsCalibrated => PlayerPrefs.HasKey(PrefOpen);
    // Diagnostics: hand state in the newest packet (no grace time) and packets received so far.
    public bool RawDetected { get; private set; }
    public int PacketCount => packetCount;
    volatile int packetCount;

    const string PrefOpen = "hand_open";
    const string PrefFist = "hand_fist";
    const string PrefMouse = "hand_use_mouse";

    // True when the player chose the mouse: the tracker (and webcam) is not started.
    public bool MouseMode => useMouseFallback;

    public void SetMouseMode(bool on)
    {
        useMouseFallback = on;
        PlayerPrefs.SetInt(PrefMouse, on ? 1 : 0);
        PlayerPrefs.Save();
        if (on) StopPython();
        else TryStartPython();
        Debug.Log("[Hand] Mouse mode " + (on ? "ON - webcam off" : "OFF - starting tracker"));
    }

    // 21 MediaPipe hand landmarks, 0..1 (x left->right, y bottom->top). Valid while HasLandmarks.
    public const int LandmarkCount = 21;
    public Vector2[] Landmarks { get; } = new Vector2[LandmarkCount];
    public bool HasLandmarks { get; private set; }
    readonly Vector2[] recvLandmarks = new Vector2[LandmarkCount];
    readonly Vector2[] latestLandmarks = new Vector2[LandmarkCount];
    bool latestHasLandmarks;

    UdpClient udp;
    Thread recvThread;
    volatile bool running;
    readonly object lockObj = new object();
    bool latestDetected;
    Vector2 latestPos;
    float latestOpen;
    string latestGesture;
    double lastPacketTime = -999;
    double nowTime;
    Process python;
    UdpClient heartbeat;
    float nextPing;

    // Frame rate = monitor refresh (VSync). A fixed 60 fps cap on a 144 Hz monitor
    // shows frames for uneven times (2,3,2,3 refreshes) and everything moving judders.
    // Uncapped (~200 fps) wastes CPU the MediaPipe tracker needs. VSync avoids both.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void SyncFrameRate()
    {
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = -1;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (PlayerPrefs.HasKey(PrefOpen))
        {
            openValue = PlayerPrefs.GetFloat(PrefOpen);
            fistValue = PlayerPrefs.GetFloat(PrefFist);
        }

        if (PlayerPrefs.HasKey(PrefMouse)) useMouseFallback = PlayerPrefs.GetInt(PrefMouse) == 1;

        StartReceiver();
        if (!useMouseFallback) TryStartPython();
        heartbeat = new UdpClient();
    }

    void TryStartPython()
    {
        if (!launchPython || (python != null && !python.HasExited)) return;
        // run_hand_tracker.py (started before Unity) binds the heartbeat port.
        // If the port is taken, use that tracker and do not start a second one.
        if (PortInUse(heartbeatPort))
            Debug.Log("[Hand] External tracker already running - not starting Python.");
        else
            StartPython();
    }

    void StopPython()
    {
        try { if (python != null && !python.HasExited) python.Kill(); } catch { }
        python = null;
    }

    static bool PortInUse(int port)
    {
        try
        {
            using (new UdpClient(new IPEndPoint(IPAddress.Loopback, port))) { }
            return false;
        }
        catch (SocketException)
        {
            return true;
        }
    }

    void StartReceiver()
    {
        try
        {
            udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, receivePort));
            running = true;
            recvThread = new Thread(ReceiveLoop) { IsBackground = true };
            recvThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Hand] Cannot open UDP port " + receivePort + ": " + e.Message);
        }
    }

    void ReceiveLoop()
    {
        var ep = new IPEndPoint(IPAddress.Any, 0);
        while (running)
        {
            try
            {
                byte[] data = udp.Receive(ref ep);
                string[] p = Encoding.ASCII.GetString(data).Split(',');
                if (p.Length < 4) continue;
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                bool det = p[0] == "1";
                float x = float.Parse(p[1], inv);
                float y = float.Parse(p[2], inv);
                float o = float.Parse(p[3], inv);
                string g = p.Length >= 5 ? p[4].Trim() : null;
                bool hasPts = det && p.Length >= 5 + LandmarkCount * 2;
                if (hasPts)
                    for (int i = 0; i < LandmarkCount; i++)
                        recvLandmarks[i] = new Vector2(float.Parse(p[5 + i * 2], inv), 1f - float.Parse(p[6 + i * 2], inv));
                lock (lockObj)
                {
                    latestDetected = det;
                    if (det) { latestPos = new Vector2(x, 1f - y); latestOpen = o; }
                    if (hasPts) { Array.Copy(recvLandmarks, latestLandmarks, LandmarkCount); latestHasLandmarks = true; }
                    else if (!det) latestHasLandmarks = false;
                    latestGesture = g;
                    lastPacketTime = nowTime;
                    packetCount++;
                }
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { break; }
            catch (FormatException) { }
        }
    }

    // A venv only works on the computer that made it: pyvenv.cfg names that computer's
    // Python folder ("home"). If that folder is not here, python.exe cannot start.
    static bool VenvUsable(string exe)
    {
        if (!File.Exists(exe)) return false;
        string cfg = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(exe)), "pyvenv.cfg");
        if (!File.Exists(cfg)) return true;
        foreach (string line in File.ReadAllLines(cfg))
        {
            int eq = line.IndexOf('=');
            if (eq < 0 || line.Substring(0, eq).Trim() != "home") continue;
            return Directory.Exists(line.Substring(eq + 1).Trim());
        }
        return true;
    }

    // Checks venvPython first, then any per-machine venv made by run_hand_tracker_portable.py
    // (HandTrackerEnv-<computer name>). Returns null when none works on this computer.
    string FindUsableVenvPython(string projectDir)
    {
        string exe = Path.Combine(projectDir, venvPython);
        if (VenvUsable(exe)) return exe;
        try
        {
            foreach (string dir in Directory.GetDirectories(projectDir, "HandTrackerEnv*"))
            {
                string candidate = Path.Combine(dir, "Scripts", "python.exe");
                if (VenvUsable(candidate)) return candidate;
            }
        }
        catch (Exception) { }
        return null;
    }

    void StartPython()
    {
        string projectDir = Path.GetDirectoryName(Application.dataPath);
        string script = Path.Combine(Application.streamingAssetsPath, scriptRelativeToStreamingAssets);
        string exe = FindUsableVenvPython(projectDir);
        if (exe == null)
        {
            Debug.LogError("[Hand] HandTrackerEnv is missing or was copied from another computer, so the hand tracker "
                + "cannot start. On this computer run \"Start Hand Tracker (Portable).bat\" once, "
                + "then press Play again. The mouse controls the hand until then.");
            return;
        }
        if (!File.Exists(script))
        {
            Debug.LogError("[Hand] Tracker script missing: " + script);
            return;
        }

        string args = $"\"{script}\" --port {receivePort} --listen {heartbeatPort}";
        if (showPythonPreviewWindow) args += " --preview";

        try
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(script)
            };
            python = new Process { StartInfo = psi, EnableRaisingEvents = true };
            python.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.Log(e.Data); };
            python.ErrorDataReceived += (s, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                // MediaPipe / TFLite write normal info lines to stderr.
                if (e.Data.Contains("Error") || e.Data.Contains("Traceback")) Debug.LogWarning("[Hand py] " + e.Data);
            };
            python.Start();
            python.BeginOutputReadLine();
            python.BeginErrorReadLine();
            Debug.Log("[Hand] Started tracker: " + exe + " " + args);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Hand] Cannot start python: " + e.Message + " - using mouse.");
            python = null;
        }
    }

    // Tells the Python tracker to forget Hand 1, so the next hand that enters the frame
    // becomes the player hand. Called when the hand check starts and when a game ends.
    public static void ResetHandLock()
    {
        var c = Instance;
        if (c == null || c.useMouseFallback || c.heartbeat == null) return;
        try { c.heartbeat.Send(new byte[] { (byte)'R' }, 1, "127.0.0.1", c.heartbeatPort); } catch { }
    }

    void Update()
    {
        nowTime = Time.realtimeSinceStartupAsDouble;

        var kb = Keyboard.current;
        if (kb != null && kb.mKey.wasPressedThisFrame) SetMouseMode(!useMouseFallback);

        // No heartbeat in mouse mode: a tracker this game started exits by itself.
        if (!useMouseFallback && Time.unscaledTime >= nextPing)
        {
            nextPing = Time.unscaledTime + 1f;
            try { heartbeat?.Send(new byte[] { 1 }, 1, "127.0.0.1", heartbeatPort); } catch { }
        }

        bool det; Vector2 raw; float open; double last; string gesture;
        lock (lockObj)
        {
            det = latestDetected; raw = latestPos; open = latestOpen; last = lastPacketTime; gesture = latestGesture;
            HasLandmarks = latestHasLandmarks;
            if (latestHasLandmarks) Array.Copy(latestLandmarks, Landmarks, LandmarkCount);
        }
        Gesture = gesture;
        RawDetected = det;

        UsingMouse = useMouseFallback || nowTime - last > dataTimeout;

        Vector2 target;
        bool fistNow;
        if (UsingMouse)
        {
            var mouse = Mouse.current;
            Vector2 m = mouse != null ? mouse.position.ReadValue() : new Vector2(Screen.width, Screen.height) * 0.5f;
            bool pressed = mouse != null && mouse.leftButton.isPressed;
            target = new Vector2(m.x / Screen.width, m.y / Screen.height);
            Detected = true;
            RawOpenness = pressed ? fistValue : openValue;
            fistNow = pressed;
            ScreenPos01 = target;
        }
        else
        {
            // MediaPipe misses the hand for a few frames now and then (fast moves, a fist,
            // motion blur). Keep the hand "found" for a short grace time so the cursor
            // does not blink on and off.
            Detected = det || lostTimer < detectGrace;
            if (det)
            {
                RawOpenness = open;
                lostTimer = 0;
                // The tracker sends ~25-30 positions a second at uneven times; the game draws
                // far more frames. Interpolate between the last two positions over the time
                // the last packet gap took (about one packet of delay, ~35 ms), so the cursor
                // moves at an even speed instead of jumping when a packet arrives.
                double now = Time.realtimeSinceStartupAsDouble;
                if (raw != prevRaw)
                {
                    float gap = (float)(now - prevRawTime);
                    // Start from where the cursor is now; after a long gap jump straight there.
                    interpFrom = gap > 0.25f ? raw : interpPos;
                    interpTime = Mathf.Clamp(gap, 0.02f, maxInterp);
                    prevRaw = raw;
                    prevRawTime = now;
                }
                float a = Mathf.Clamp01((float)(now - prevRawTime) / interpTime);
                interpPos = Vector2.Lerp(interpFrom, raw, a);
                Vector2 predicted = interpPos;
                float m = edgeMargin, my = edgeMarginY;
                target = new Vector2(Mathf.InverseLerp(m, 1f - m, predicted.x), Mathf.InverseLerp(my, 1f - my, predicted.y));
                // Adaptive smoothing: steady when still, almost no lag when moving fast.
                float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
                float speed = (target - ScreenPos01).magnitude / dt;
                float sharp = Mathf.Lerp(followSharpness, fastSharpness, Mathf.Clamp01(speed / fastSpeed));
                float k = 1f - Mathf.Exp(-sharp * speedMultiplier * dt);
                ScreenPos01 = Vector2.Lerp(ScreenPos01, target, k);
            }
            float mid = (openValue + fistValue) * 0.5f;
            float band = (openValue - fistValue) * hysteresis;
            if (!det)
            {
                // MediaPipe often loses a closed hand for a frame or two: keep the fist briefly.
                lostTimer += Time.unscaledDeltaTime;
                fistNow = IsFist && lostTimer < lostHold;
            }
            else if (gesture != null)
            {
                // Two signals together, so a grab is not missed when one of them is late:
                //   - Gesture Recognizer says Closed_Fist, or
                //   - openness (fingertip distance / palm size) drops below gestureFistOpenness.
                // Release only when the hand is clearly open again (hysteresis), and ignore
                // very short open blips so the fist does not flicker.
                bool closedSignal = gesture == "Closed_Fist" || open < gestureFistOpenness;
                bool openSignal = gesture == "Open_Palm" || open > gestureOpenOpenness;
                if (closedSignal) { fistNow = true; openTimer = 0; }
                else if (IsFist)
                {
                    openTimer = openSignal ? openTimer + Time.unscaledDeltaTime : 0;
                    fistNow = openTimer < releaseDelay;
                }
                else fistNow = false;
            }
            // Old tracker without gestures: calibrated openness threshold with hysteresis.
            else if (IsFist) fistNow = open < mid + band;
            else fistNow = open < mid - band;
        }

        FistDown = fistNow && !IsFist;
        FistUp = !fistNow && IsFist;
        IsFist = fistNow;
    }

    public void SaveCalibration(float open, float fist)
    {
        if (open - fist < 0.2f)
        {
            Debug.LogWarning($"[Hand] Calibration range too small (open {open:F2}, fist {fist:F2}). Using defaults.");
            open = 1.9f; fist = 1.0f;
        }
        openValue = open;
        fistValue = fist;
        PlayerPrefs.SetFloat(PrefOpen, open);
        PlayerPrefs.SetFloat(PrefFist, fist);
        PlayerPrefs.Save();
        Debug.Log($"[Hand] Calibrated open={open:F2} fist={fist:F2}");
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        Shutdown();
    }

    void OnApplicationQuit() => Shutdown();

    void Shutdown()
    {
        running = false;
        try { udp?.Close(); } catch { }
        udp = null;
        try { heartbeat?.Close(); } catch { }
        heartbeat = null;
        StopPython();
    }
}
