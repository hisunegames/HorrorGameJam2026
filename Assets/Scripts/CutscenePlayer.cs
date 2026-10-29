using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

//Plays an .mp4 from Assets/StreamingAssets full screen on top of everything (works in WebGL, which can't use
//imported VideoClips). Used for the opening video (IntroVideo) and the ending cutscenes (GameOverScreen).
//Runs on unscaled time, so the game can be paused behind it
public static class CutscenePlayer
{
    //done(true) if it played (to the end or skipped), done(false) if it couldn't load
    public static IEnumerator Play(string file, Func<bool> skipPressed, Action<bool> done, float loadTimeout = 10f)
    {
        GameObject canvasObj = new GameObject("Cutscene");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        RawImage screen = new GameObject("Screen").AddComponent<RawImage>();
        screen.transform.SetParent(canvasObj.transform, false);
        screen.color = Color.black;
        RectTransform r = screen.rectTransform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;

        VideoPlayer player = canvasObj.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        //A URL in WebGL, a file path elsewhere
        player.source = VideoSource.Url;
        player.url = Application.streamingAssetsPath + "/" + file;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.audioOutputMode = VideoAudioOutputMode.Direct;
        player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        bool failed = false;
        player.errorReceived += (p, message) =>
        {
            failed = true;
            Debug.LogWarning("Video " + file + " failed: " + message);
        };
        player.Prepare();
        float waited = 0f;
        while(!player.isPrepared && !failed && waited < loadTimeout)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        if(!player.isPrepared)
        {
            //Missing or unplayable: carry on without it
            UnityEngine.Object.Destroy(canvasObj);
            done?.Invoke(false);
            yield break;
        }
        RenderTexture target = new RenderTexture((int)Mathf.Max(16, player.width), (int)Mathf.Max(16, player.height), 0);
        player.targetTexture = target;
        screen.texture = target;
        screen.color = Color.white;
        player.Play();

        //The key that started it shouldn't skip it
        yield return null;
        while(player.isPlaying && !(skipPressed != null && skipPressed())) yield return null;
        player.Stop();
        UnityEngine.Object.Destroy(canvasObj);
        target.Release();
        done?.Invoke(true);
    }

    public static bool AnySkipKey()
    {
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.F);
    }
}
