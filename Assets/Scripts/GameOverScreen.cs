using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverScreen : MonoBehaviour
{
    public BlackScreenUI blackScreen;
    public string deathTitle = "YOU DIED";
    [TextArea] public string deathLine = "The knowledge dies with you.\n\nPress R to try again";
    private bool isGameOver = false;

    void Awake()
    {
        if(blackScreen == null) blackScreen = GetComponent<BlackScreenUI>();
    }

    void OnEnable()
    {
        Enemy.OnPlayerKilled += ShowGameOver;
    }

    void OnDisable()
    {
        Enemy.OnPlayerKilled -= ShowGameOver;
    }

    void Update()
    {
        if(isGameOver && Input.GetKeyDown(KeyCode.R))
        {
            Restart();
        }
    }

    void ShowGameOver()
    {
        if(isGameOver) return;
        isGameOver = true;
        StartCoroutine(GameOverRoutine());
    }

    IEnumerator GameOverRoutine()
    {
        Time.timeScale = 0f;
        blackScreen.SetText(deathTitle, deathLine);
        yield return blackScreen.FadeIn();
    }

    void Restart()
    {
        Time.timeScale = 1f;
        Scene scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
        //Test scenes aren't in Build Settings, so load by path in the editor
        if(scene.buildIndex < 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
            return;
        }
#endif
        SceneManager.LoadScene(scene.buildIndex);
    }
}
