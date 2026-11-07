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
// Message per frame: "detected,x,y,openness" (see StreamingAssets/HandTracker/hand_tracker.py).
//
// Gives the rest of the game:
//   Detected     - a hand is visible
//   ScreenPos01  - smoothed hand position, 0..1 (x left->right, y bottom->top)
//   IsFist       - hand closed, uses calibrated threshold with hysteresis
//   FistDown     - true only on the frame the hand closes
//
// If Python is not running (or useMouseFallback is on), the mouse drives the hand:
// move = hand, hold left button = fist. Good for testing without a webcam.
[DefaultExecutionOrder(-100)]
public class HandTrackingClient : MonoBehaviour
{
    public static HandTrackingClient Instance { get; private set; }

    [Header("Python")]
    public bool launchPython = true;
    [Tooltip("Relative to the Unity project folder (parent of Assets).")]
    public string venvPython = "HandTrackerEnv/Scripts/python.exe";
    public string scriptRelativeToStreamingAssets = "HandTracker/hand_tracker.py";
    public bool showPythonPreviewWindow = false;
    public int receivePort = 5065;
    public int heartbeatPort = 5066;

    [Header("Mapping")]
    [Tooltip("Camera border ignored, so the player does not need to reach the camera edge.")]
    [Range(0f, 0.4f)] public float edgeMargin = 0.15f;
    [Range(1f, 40f)] public float followSharpness = 18f;
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

    public bool Detected { get; private set; }
    public Vector2 ScreenPos01 { get; private set; } = new Vector2(0.5f, 0.5f);
    public float RawOpenness { get; private set; }
    public bool IsFist { get; private set; }
    public bool FistDown { get; private set; }
    public bool FistUp { get; private set; }
    public bool UsingMouse { get; private set; }
    public bool IsCalibrated => PlayerPrefs.HasKey(PrefOpen);

    const string PrefOpen = "hand_open";
    const string PrefFist = "hand_fist";

    UdpClient udp;
    Thread recvThread;
    volatile bool running;
    readonly object lockObj = new object();
    bool latestDetected;
    Vector2 latestPos;
    float latestOpen;
    double lastPacketTime = -999;
    double nowTime;
    Process python;
    UdpClient heartbeat;
    float nextPing;

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

        StartReceiver();
        if (launchPython && !useMouseFallback) StartPython();
        heartbeat = new UdpClient();
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
                lock (lockObj)
                {
                    latestDetected = det;
                    if (det) { latestPos = new Vector2(x, 1f - y); latestOpen = o; }
                    lastPacketTime = nowTime;
                }
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { break; }
            catch (FormatException) { }
        }
    }

    void StartPython()
    {
        string projectDir = Path.GetDirectoryName(Application.dataPath);
        string script = Path.Combine(Application.streamingAssetsPath, scriptRelativeToStreamingAssets);
        string exe = Path.Combine(projectDir, venvPython);
        if (!File.Exists(exe))
        {
            Debug.LogWarning("[Hand] venv python not found at " + exe + ". Trying 'python' on PATH.");
            exe = "python";
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

    void Update()
    {
        nowTime = Time.realtimeSinceStartupAsDouble;

        if (Time.unscaledTime >= nextPing)
        {
            nextPing = Time.unscaledTime + 1f;
            try { heartbeat?.Send(new byte[] { 1 }, 1, "127.0.0.1", heartbeatPort); } catch { }
        }

        bool det; Vector2 raw; float open; double last;
        lock (lockObj) { det = latestDetected; raw = latestPos; open = latestOpen; last = lastPacketTime; }

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
            Detected = det;
            if (det)
            {
                RawOpenness = open;
                float m = edgeMargin;
                target = new Vector2(Mathf.InverseLerp(m, 1f - m, raw.x), Mathf.InverseLerp(m, 1f - m, raw.y));
                float k = 1f - Mathf.Exp(-followSharpness * speedMultiplier * Time.unscaledDeltaTime);
                ScreenPos01 = Vector2.Lerp(ScreenPos01, target, k);
            }
            // Threshold with hysteresis so the state does not flicker.
            float mid = (openValue + fistValue) * 0.5f;
            float band = (openValue - fistValue) * hysteresis;
            if (!det) fistNow = false;
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
        try
        {
            if (python != null && !python.HasExited) python.Kill();
        }
        catch { }
        python = null;
    }
}
