using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [Header("Sound Settings")]
    [SerializeField] SoundManager soundManager;
    [SerializeField] GameObject settingPanel;
    [SerializeField] Button backToMenu;
    [SerializeField] Slider musicSlider;
    [SerializeField] Slider soundSlider;
    private CanvasGroup settingCanvasgroup;
    private Coroutine SettingGameCoroutine;

    bool isSoundSettingOn;
    
    [Header("Main Menu UI")]
    [SerializeField] GameObject mainMenuPanel;
    [SerializeField] Button StartButton;
    [SerializeField] Button QuitButton;
    [SerializeField] Button SettingButton;
    private CanvasGroup mainmenuCanvasgroup;
    private Coroutine StartGameCoroutine;

    void Start()
    {
        mainmenuCanvasgroup = mainMenuPanel.GetComponent<CanvasGroup>();
        settingCanvasgroup = settingPanel.GetComponent<CanvasGroup>();
        settingCanvasgroup.alpha = 0f;
        settingCanvasgroup.blocksRaycasts = false;
        Time.timeScale = 0f;
        backToMenu.interactable = false;
        soundSlider.interactable = false;
        musicSlider.interactable = false;
        if (StartGameCoroutine != null)
        {
            StopCoroutine(StartGameCoroutine);
            StartGameCoroutine = null;
        }
        if(SettingGameCoroutine != null)
        {
            StopCoroutine(SettingGameCoroutine);
            SettingGameCoroutine = null;
        }
    }

    public void EnterGame()
    {
        StartGameCoroutine = StartCoroutine(FadeMenu());
    }

    IEnumerator FadeMenu()
    {
        yield return null;
        float duration = 2f;
        float elapsed = 0f;
        float startalpha = mainmenuCanvasgroup.alpha;
        SettingButton.interactable = false;
        StartButton.interactable = false;
        QuitButton.interactable = false;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            mainmenuCanvasgroup.alpha = Mathf.Lerp(startalpha , 0f , elapsed/duration);
            yield return null;
        }
        Time.timeScale = 1f;
        mainmenuCanvasgroup.alpha = 0f;
        mainMenuPanel.SetActive(false);
        //Start GameState

    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void ToggleSetting()
    {
        isSoundSettingOn = !isSoundSettingOn;
        StartCoroutine(fadeSettings());
    }
    
    IEnumerator fadeSettings()
    {
        yield return null;
        float duration = 2f;
        float elapsed = 0f;
        float startalpha = settingCanvasgroup.alpha;
        if (isSoundSettingOn)
        {
            settingPanel.SetActive(true);
            SettingButton.interactable =false;
            StartButton.interactable = false;
            QuitButton.interactable = false;
        }
        backToMenu.interactable = false;
        soundSlider.interactable = false;
        musicSlider.interactable = false;

        while (elapsed < duration)
        {
            if (isSoundSettingOn)
            {
                elapsed += Time.unscaledDeltaTime;
                settingCanvasgroup.alpha = Mathf.Lerp(startalpha , 1f , elapsed/duration);
            }
            else
            {
                elapsed += Time.unscaledDeltaTime;
                settingCanvasgroup.alpha = Mathf.Lerp(startalpha , 0f , elapsed/duration);
            }
            yield return null;
        }
        settingCanvasgroup.alpha = isSoundSettingOn? 1f : 0f;
        backToMenu.interactable = false;
        if (isSoundSettingOn)
        {
            settingPanel.SetActive(true);
            backToMenu.interactable = true;
            soundSlider.interactable = true;
            musicSlider.interactable = true;
            settingCanvasgroup.blocksRaycasts = true;
        }
        else
        {
            settingPanel.SetActive(false);
            StartButton.interactable = true;
            QuitButton.interactable = true;
            SettingButton.interactable = true;
            settingCanvasgroup.blocksRaycasts = false;
        }
    }
}
