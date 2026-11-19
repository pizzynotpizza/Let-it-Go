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
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // Named handler, removed in OnDestroy. The project has "Enter Play Mode Options"
    // on (no domain reload), so a lambda left on the static sceneLoaded event would
    // survive into the next Play session and touch this destroyed object.
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayForScene(scene.name);
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
