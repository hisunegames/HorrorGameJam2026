using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

//Death and ending screens. Endings are a few pages of text ([Space]/[F] next page, [R] to restart at the end).
//Each ending can have a video (e.g. an After Effects cutscene) that plays first, full screen; [Space] skips it.
//Videos are .mp4 files in Assets/StreamingAssets, streamed by file name, so they work in WebGL builds too
public class GameOverScreen : MonoBehaviour
{
    public BlackScreenUI blackScreen;
    public string deathTitle = "YOU DIED";
    [TextArea] public string deathLine = "The knowledge dies with you.\n\nPress R to try again";
    private bool isGameOver = false;
    private bool canRestart = false;
    private bool nextPage = false;

    [Header("Ending cutscenes (optional, play before the text)")]
    [Tooltip("File names inside Assets/StreamingAssets, e.g. Karma.mp4. Empty = no video for that ending")]
    public string defiedDeathVideo = "";
    public string karmaVideo = "";
    public string neverWorkedVideo = "";
    public string trueEndingVideo = "";
    //Gives up on a video that won't load (missing file, unsupported format) after this many seconds
    public float videoLoadTimeout = 10f;
    [Tooltip("On: the video is the whole ending (just 'Press R' after it). Off: the text pages follow the video")]
    public bool videoReplacesText = false;

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
        if(!isGameOver) return;
        if(Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Return)) nextPage = true;
        if(canRestart && Input.GetKeyDown(KeyCode.R)) Restart();
    }

    void ShowGameOver()
    {
        if(isGameOver) return;
        isGameOver = true;
        canRestart = true;
        //Whatever killed you (Enemy.KillPlayer) goes above the usual line
        string cause = Enemy.LastDeathCause;
        StartCoroutine(EndRoutine(deathTitle, string.IsNullOrEmpty(cause) ? deathLine : cause + "\n\n" + deathLine));
    }

    //One-page ending
    public void ShowEnd(string title, string body)
    {
        if(isGameOver) return;
        isGameOver = true;
        canRestart = true;
        StartCoroutine(EndRoutine(title, body));
    }

    //Multi-page ending: title on every page, the last page says how to restart
    public void ShowEnding(string title, string[] pages)
    {
        if(isGameOver) return;
        isGameOver = true;
        RecordEnding(title);
        StartCoroutine(EndingRoutine(title, pages, VideoFor(title)));
    }

    //The four endings; which ones you've seen is saved between plays
    static readonly string[] Endings = { "DEFIED DEATH", "IT NEVER WORKED", "KARMA", "TRUE ENDING" };

    static void RecordEnding(string title)
    {
        if(System.Array.IndexOf(Endings, title) < 0) return;
        PlayerPrefs.SetInt("Ending_" + title, 1);
        PlayerPrefs.Save();
    }

    public static int EndingsFound
    {
        get
        {
            int found = 0;
            foreach(string e in Endings) if(PlayerPrefs.GetInt("Ending_" + e, 0) == 1) found++;
            return found;
        }
    }

    public static int EndingCount => Endings.Length;

    string VideoFor(string title)
    {
        switch(title)
        {
            case "DEFIED DEATH": return defiedDeathVideo;
            case "KARMA": return karmaVideo;
            case "IT NEVER WORKED": return neverWorkedVideo;
            case "TRUE ENDING": return trueEndingVideo;
        }
        return null;
    }

    IEnumerator EndingRoutine(string title, string[] pages, string videoFile)
    {
        if(!string.IsNullOrEmpty(videoFile))
        {
            Time.timeScale = 0f;
            bool played = false;
            yield return CutscenePlayer.Play(videoFile, CutscenePlayer.AnySkipKey, ok => played = ok, videoLoadTimeout);
            if(played && videoReplacesText) pages = new[] { "" };
        }
        yield return PagesRoutine(title, pages);
    }

    IEnumerator EndRoutine(string title, string body)
    {
        Time.timeScale = 0f;
        blackScreen.SetText(title, body);
        yield return blackScreen.FadeIn();
    }

    IEnumerator PagesRoutine(string title, string[] pages)
    {
        Time.timeScale = 0f;
        for(int i = 0; i < pages.Length; i++)
        {
            bool last = i == pages.Length - 1;
            string hint = last ? $"\n\n\n{Lore.Game}\n\nEndings found: {EndingsFound}/{EndingCount}\n\nPress R to play again" : "\n\n\n[Space]";
            blackScreen.SetText(title, pages[i] + hint);
            if(i == 0) yield return blackScreen.FadeIn();
            if(last) break;
            nextPage = false;
            while(!nextPage) yield return null;
        }
        canRestart = true;
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
