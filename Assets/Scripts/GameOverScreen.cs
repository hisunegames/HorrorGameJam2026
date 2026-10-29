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
        //Whatever killed you (Enemy.KillPlayer) goes above the usual line
        string cause = Enemy.LastDeathCause;
        yield return EndRoutine(deathTitle, string.IsNullOrEmpty(cause) ? deathLine : cause + "\n\n" + deathLine);
    }

    //Any ending (escape, nuke...): black screen with text, R restarts
    public void ShowEnd(string title, string body)
    {
        if(isGameOver) return;
        isGameOver = true;
        StartCoroutine(EndRoutine(title, body));
    }

    IEnumerator EndRoutine(string title, string body)
    {
        Time.timeScale = 0f;
        blackScreen.SetText(title, body);
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
