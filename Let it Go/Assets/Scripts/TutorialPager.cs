using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Shows the tutorial as full-screen pages (Assets/Background/Tutorial/1..9.jpg).
// Each page image already contains the frame and the text; only the button is a separate object.
// The button advances to the next page. On the last page it shows the Start sprite
// (placed where the Start art sits) and loads the gameplay scene.
public class TutorialPager : MonoBehaviour
{
    public Image background;
    public Sprite[] pages;
    public Button nextButton;
    public Sprite nextSprite;
    public Sprite startSprite;
    public string gameplayScene = "Gameplay";

    [Header("Button layout (canvas units, 1920x1080, centre anchor)")]
    public Vector2 nextPos = new Vector2(0, -436);
    public Vector2 nextSize = new Vector2(550, 275);
    public Vector2 startPos = new Vector2(-3, -416);
    public Vector2 startSize = new Vector2(480, 240);

    RectTransform buttonRect;
    Image buttonImage;
    int index;

    void Awake()
    {
        buttonRect = (RectTransform)nextButton.transform;
        buttonImage = nextButton.GetComponent<Image>();
        buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.localScale = Vector3.one;
        nextButton.onClick.AddListener(OnNext);
        Show(0);
    }

    void Update()
    {
        // Keyboard shortcut for testing: Enter / Right arrow = Next.
        var kb = Keyboard.current;
        if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)) OnNext();
    }

    void OnNext()
    {
        if (index >= pages.Length - 1)
            SceneManager.LoadScene(gameplayScene);
        else
            Show(index + 1);
    }

    void Show(int i)
    {
        index = i;
        if (background && pages.Length > 0) background.sprite = pages[i];
        bool last = index >= pages.Length - 1;
        buttonImage.sprite = last ? startSprite : nextSprite;
        buttonRect.anchoredPosition = last ? startPos : nextPos;
        buttonRect.sizeDelta = last ? startSize : nextSize;
    }
}
