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
    //Seconds a video can sit on the same frame before it counts as frozen
    const float StallTimeout = 3f;

    //A video left on its last frame (holdLastFrame), until Release()
    private static GameObject held;
    private static RenderTexture heldTexture;

    //done(true) if it played (to the end or skipped), done(false) if it couldn't load.
    //holdLastFrame: leave the final frame on screen until Release() (e.g. under a choice)
    public static IEnumerator Play(string file, Func<bool> skipPressed, Action<bool> done, float loadTimeout = 10f, bool holdLastFrame = false)
    {
        //Not there yet (or an empty / Git LFS placeholder file): skip it without the video player erroring.
        //WebGL can't look at files, so there it just tries and times out
        if(!Exists(file))
        {
            Debug.Log("Video " + file + " isn't in StreamingAssets, skipping it");
            done?.Invoke(false);
            yield break;
        }
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
        //Cutscene volume from the pause menu (video sound skips the AudioListener, so Master is in there too)
        for(ushort track = 0; track < player.audioTrackCount; track++) player.SetDirectAudioVolume(track, GameAudio.CutsceneVolume);
        player.Play();

        //The key that started it shouldn't skip it
        yield return null;
        //Some encodes stall (Windows decoder + non-baseline H.264) while still "playing", or never report the end:
        //stop at the end ourselves, and give up if the picture stops moving for a few seconds
        double lastTime = -1;
        float stalled = 0f;
        while(player.isPlaying && !(skipPressed != null && skipPressed()))
        {
            if(player.length > 0 && player.time >= player.length - 0.05) break;
            if(player.time > lastTime + 0.0001)
            {
                lastTime = player.time;
                stalled = 0f;
            }
            else
            {
                stalled += Time.unscaledDeltaTime;
                if(stalled >= StallTimeout)
                {
                    Debug.LogWarning($"Video {file} stopped moving at {player.time:0.0}s of {player.length:0.0}s, carrying on");
                    break;
                }
            }
            yield return null;
        }
        if(holdLastFrame)
        {
            //Paused on whatever frame it got to, still showing
            player.Pause();
            Release();
            held = canvasObj;
            heldTexture = target;
            done?.Invoke(true);
            yield break;
        }
        player.Stop();
        UnityEngine.Object.Destroy(canvasObj);
        target.Release();
        done?.Invoke(true);
    }

    static bool Exists(string file)
    {
        if(string.IsNullOrEmpty(file)) return false;
        if(Application.platform == RuntimePlatform.WebGLPlayer) return true;
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, file);
        //Under 1 KB can't be a real video (LFS leaves a ~130 byte pointer when it hasn't been pulled)
        return System.IO.File.Exists(path) && new System.IO.FileInfo(path).Length > 1024;
    }

    //Take down a held last frame
    public static void Release()
    {
        if(held != null) UnityEngine.Object.Destroy(held);
        if(heldTexture != null) heldTexture.Release();
        held = null;
        heldTexture = null;
    }

    public static bool AnySkipKey()
    {
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.F);
    }
}
