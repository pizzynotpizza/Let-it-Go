using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Loads a scene by name. Hook LoadTarget() to a Button's OnClick.
/// The scene must be listed in File > Build Profiles (Build Settings).
/// </summary>
public class StartGame : MonoBehaviour
{
    public string sceneName = "Tutorial";

    public void LoadTarget()
    {
        SceneManager.LoadScene(sceneName);
    }
}
