using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Start button on the Title screen. Runs the hand check first when one is in the
// scene (it loads the next scene itself); otherwise loads the target scene directly.
[RequireComponent(typeof(Button))]
public class StartButton : MonoBehaviour
{
    public string sceneName = "Tutorial";

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        var check = FindAnyObjectByType<TitleHandCheck>();
        if (check != null) check.Begin();
        else SceneManager.LoadScene(sceneName);
    }
}
