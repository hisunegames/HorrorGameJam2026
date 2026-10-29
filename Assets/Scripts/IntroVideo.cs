using System.Collections;
using UnityEngine;

//Opening video before play starts: an .mp4 in Assets/StreamingAssets. The game is paused until it ends or is
//skipped ([Space]/[Esc]/[Enter]/[F]). First a "press any key" screen: browsers only allow sound after the player
//has pressed something, so in WebGL the video would otherwise start muted. Put it on the GameManager
public class IntroVideo : MonoBehaviour
{
    [Tooltip("File name inside Assets/StreamingAssets, e.g. Intro.mp4. Empty = no opening video")]
    public string videoFile = "Intro_Final.mp4";
    public string pressKeyTitle = Lore.Game;
    public string pressKeyText = "Press any key";
    //Only the first time the scene loads, not after dying and pressing R
    public bool onlyOnFirstLoad = true;

    private static bool shownThisSession;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        shownThisSession = false;
    }

    IEnumerator Start()
    {
        if(string.IsNullOrEmpty(videoFile) || (onlyOnFirstLoad && shownThisSession)) yield break;
        //A scene with the main menu pauses for that instead (both would fight over the pause)
        if(FindAnyObjectByType<MainMenu>() != null) yield break;
        shownThisSession = true;
        float oldScale = Time.timeScale;
        Time.timeScale = 0f;

        BlackScreenUI black = GameManager.Get<BlackScreenUI>();
        if(black != null)
        {
            black.SetText(pressKeyTitle, pressKeyText);
            yield return black.FadeIn(0.01f);
            //Wait a frame so a key held from the editor's Play button doesn't count
            yield return null;
            while(!Input.anyKeyDown) yield return null;
        }

        yield return CutscenePlayer.Play(videoFile, CutscenePlayer.AnySkipKey, null);

        if(black != null)
        {
            black.SetText("", "");
            yield return black.FadeOut();
        }
        Time.timeScale = oldScale > 0f ? oldScale : 1f;
    }
}
