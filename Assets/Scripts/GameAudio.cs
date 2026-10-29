using System.Collections.Generic;
using UnityEngine;

//Volume settings (Master, Music, SFX, Cutscene), saved between plays. Master is the AudioListener; Music is any
//AudioSource on a SoundManager; SFX is every other AudioSource (footsteps, radio, monster...); Cutscene is the
//video sound (CutscenePlayer). Sources keep the volume they were made with, times their category. On the GameManager
public class GameAudio : MonoBehaviour
{
    public enum Channel { Master, Music, Sfx, Cutscene }

    static readonly float[] volumes = { 1f, 1f, 1f, 1f };
    static bool loaded;

    //Volume each source started with, so the setting scales it instead of overwriting it
    private readonly Dictionary<AudioSource, float> baseVolume = new Dictionary<AudioSource, float>();
    //Sources that set their own volume (fades): they multiply in the SFX setting themselves, left alone here
    public static readonly HashSet<AudioSource> SelfManaged = new HashSet<AudioSource>();
    private float scanTimer;

    public static float Get(Channel channel)
    {
        Load();
        return volumes[(int)channel];
    }

    public static void Set(Channel channel, float value)
    {
        Load();
        volumes[(int)channel] = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat("Volume_" + channel, volumes[(int)channel]);
        PlayerPrefs.Save();
        AudioListener.volume = volumes[(int)Channel.Master];
    }

    //Videos play their sound straight out (not through the AudioListener), so Master is applied here too
    public static float CutsceneVolume => Get(Channel.Master) * Get(Channel.Cutscene);

    static void Load()
    {
        if(loaded) return;
        loaded = true;
        for(int i = 0; i < volumes.Length; i++) volumes[i] = PlayerPrefs.GetFloat("Volume_" + (Channel)i, 1f);
    }

    void Awake()
    {
        Load();
        AudioListener.volume = volumes[(int)Channel.Master];
    }

    void Update()
    {
        //New sources turn up (footsteps, radio), so look every so often
        scanTimer -= Time.unscaledDeltaTime;
        if(scanTimer > 0f) return;
        scanTimer = 0.25f;
        AudioListener.volume = volumes[(int)Channel.Master];
        foreach(AudioSource source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
        {
            if(SelfManaged.Contains(source)) continue;
            if(!baseVolume.TryGetValue(source, out float start))
            {
                start = source.volume;
                baseVolume[source] = start;
            }
            bool music = source.GetComponentInParent<SoundManager>() != null;
            //Music keeps going in the pause menu, the rest pauses with the game
            source.ignoreListenerPause = music;
            source.volume = start * (music ? volumes[(int)Channel.Music] : volumes[(int)Channel.Sfx]);
        }
    }
}
