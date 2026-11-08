using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Loads the target scene when the attached Button is clicked.
[RequireComponent(typeof(Button))]
public class StartButton : MonoBehaviour
{
    public string sceneName = "Tutorial";

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() => SceneManager.LoadScene(sceneName));
    }
}
