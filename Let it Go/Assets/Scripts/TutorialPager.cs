using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Shows tutorial pages one at a time. The button advances to the next page;
// on the last page it swaps to the Start sprite and loads the gameplay scene.
public class TutorialPager : MonoBehaviour
{
    public TMP_Text bodyText;
    public Button nextButton;
    public Sprite nextSprite;
    public Sprite startSprite;
    public string gameplayScene = "Gameplay";
    [TextArea(3, 6)] public string[] pages;

    // Optional extra layout per page (same index as pages). Only the current page's object is shown.
    public GameObject[] pageObjects;

    // Extra shift for the button on the last page, because the Start art sits lower than the Next art.
    public Vector2 startButtonOffset = new Vector2(0, 50);

    RectTransform buttonRect;
    Vector2 buttonBasePos;

    int index;

    void Awake()
    {
        buttonRect = (RectTransform)nextButton.transform;
        buttonBasePos = buttonRect.anchoredPosition;
        nextButton.onClick.AddListener(OnNext);
        Show(0);
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
        bodyText.text = pages[i];
        for (int k = 0; k < pageObjects.Length; k++)
            if (pageObjects[k] != null)
                pageObjects[k].SetActive(k == i);
        bool last = index >= pages.Length - 1;
        nextButton.GetComponent<Image>().sprite = last ? startSprite : nextSprite;
        buttonRect.anchoredPosition = last ? buttonBasePos + startButtonOffset : buttonBasePos;
    }
}
