using UnityEngine;
using UnityEngine.SceneManagement;

// Background music for every scene. Created automatically at startup; no scene setup needed.
// Clips live in Assets/Sound/Resources/Music. Credits: Assets/Sound/CREDITS.txt
public class MusicPlayer : MonoBehaviour
{
    public static MusicPlayer Instance { get; private set; }

    public const string Title = "BGM_Title";
    public const string Tutorial = "BGM_Tutorial";
    public const string Gameplay = "BGM_Gameplay";
    public const string GameOver = "BGM_GameOver";
    public const string Victory = "BGM_Victory";
    public const string Hurry = "BGM_Hurry";

    // Sound effects in Assets/Sound/Resources/SFX
    public const string SfxButton = "SFX_Button";
    public const string SfxGrabWork = "SFX_GrabWork";
    public const string SfxGrabRelax = "SFX_GrabRelax";
    public const string SfxGrabRecover = "SFX_GrabRecover";

    [Tooltip("Kept below the music volume so effects stay soft.")]
    [Range(0, 1)] public float sfxVolume = 0.25f;
    AudioSource sfxSource;
    readonly System.Collections.Generic.Dictionary<string, AudioClip> sfxCache = new System.Collections.Generic.Dictionary<string, AudioClip>();

    public const string SfxFillTick = "SFX_FillTick";
    public const string SfxFillDone = "SFX_FillDone";

    public static void PlaySfx(string name, float pitch = 1f)
    {
        if (Instance) Instance.PlaySfxInternal(name, pitch);
    }

    void PlaySfxInternal(string name, float pitch)
    {
        if (!sfxCache.TryGetValue(name, out var clip))
        {
            clip = Resources.Load<AudioClip>("SFX/" + name);
            sfxCache[name] = clip;
            if (!clip) Debug.LogWarning("MusicPlayer: missing sfx " + name);
        }
        if (!clip) return;
        if (Mathf.Approximately(pitch, 1f)) { sfxSource.PlayOneShot(clip, sfxVolume); return; }
        // Pitched sounds get their own source so the pitch does not change other effects.
        if (!pitchSource) { pitchSource = gameObject.AddComponent<AudioSource>(); pitchSource.playOnAwake = false; }
        pitchSource.pitch = pitch;
        pitchSource.PlayOneShot(clip, sfxVolume);
    }

    AudioSource pitchSource;

    // Every UI Button in the loaded scene plays the click sound (mouse or hand pointer).
    static void HookButtons()
    {
        foreach (var b in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            b.onClick.RemoveListener(PlayClick);
            b.onClick.AddListener(PlayClick);
        }
    }

    static void PlayClick() => PlaySfx(SfxButton);

    [Range(0, 1)] public float volume = 0.4f;
    public float fadeTime = 0.6f;

    AudioSource source;
    string current;
    Coroutine fade;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (Instance) return;
        var go = new GameObject("MusicPlayer");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<MusicPlayer>();
    }

    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.volume = 0f;
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        SceneManager.sceneLoaded += OnSceneLoaded;

        // Load every effect now, so the first grab or click does not load from disk mid-game.
        foreach (var clip in Resources.LoadAll<AudioClip>("SFX"))
        {
            sfxCache[clip.name] = clip;
            clip.LoadAudioData();
        }
    }

    // Named handler, removed in OnDestroy. The project has "Enter Play Mode Options"
    // on (no domain reload), so a lambda left on the static sceneLoaded event would
    // survive into the next Play session and touch this destroyed object.
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayForScene(scene.name);
        HookButtons();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    void PlayForScene(string scene)
    {
        switch (scene)
        {
            case "Title": Play(Title); break;
            case "Tutorial": Play(Tutorial); break;
            case "Gameplay": Play(Gameplay); break;
        }
    }

    // Static helper so callers need no null check.
    public static void PlayTrack(string name, bool loop = true)
    {
        if (Instance) Instance.Play(name, loop);
    }

    public void Play(string name, bool loop = true)
    {
        if (name == current && source.isPlaying) return;
        var clip = Resources.Load<AudioClip>("Music/" + name);
        if (!clip) { Debug.LogWarning("MusicPlayer: missing clip " + name); return; }
        current = name;
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(Swap(clip, loop));
    }

    System.Collections.IEnumerator Swap(AudioClip clip, bool loop)
    {
        for (float v = source.volume; v > 0f && source.isPlaying; v -= Time.unscaledDeltaTime / fadeTime * volume)
        {
            source.volume = v;
            yield return null;
        }
        source.clip = clip;
        source.loop = loop;
        source.Play();
        for (float v = 0f; v < volume; v += Time.unscaledDeltaTime / fadeTime * volume)
        {
            source.volume = v;
            yield return null;
        }
        source.volume = volume;
    }
}
