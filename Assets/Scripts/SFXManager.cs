using UnityEngine;

public class SFXManager : MonoBehaviour
{
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip _bear;
    [SerializeField] AudioClip _door;
    [SerializeField] AudioClip _lock;
    [SerializeField] AudioClip _item;
    [SerializeField] AudioClip _chem;
    [SerializeField] AudioClip _scan;
    [SerializeField] AudioClip _stab;
    [SerializeField] AudioClip _shoot;
    [SerializeField] AudioClip _note;
    [SerializeField] AudioClip _gen;
    [SerializeField] AudioClip _salvage;
    [SerializeField] AudioClip _bodyswap;
    [SerializeField] AudioClip _flash;
    [SerializeField] AudioClip _explosion;
    [Tooltip("The warhead explosion is loud: this much of full volume")]
    [Range(0f, 1f)] public float explosionVolume = 0.45f;
    private AudioSource explosionSource;
    private float explosionFade = -1f;
    private float explosionFadeTime = 1f;
    //Looped while the warhead counts down (its own source, so other sounds don't cut it off)
    [SerializeField] AudioClip _alarm;
    private AudioSource alarmSource;
    //Played by PlayerSprint on each step (bodies with their own Footstep Sounds use those instead)
    [SerializeField] AudioClip _footsteps;

    public static AudioClip Footstep => Instance != null ? Instance._footsteps : null;

    //The footsteps file has several steps in a row: cut into one clip per step (found by where the sound
    //jumps up after a quiet bit; even pieces if that doesn't work). Needs the clip on Decompress On Load
    private static AudioClip slicedFrom;
    private static AudioClip[] slices;

    public static AudioClip[] FootstepSlices
    {
        get
        {
            AudioClip clip = Footstep;
            if(clip == null) return null;
            if(clip == slicedFrom && slices != null) return slices;
            if(clip.loadState != AudioDataLoadState.Loaded)
            {
                clip.LoadAudioData();
                //Not ready yet: play it whole this time
                if(clip.loadState != AudioDataLoadState.Loaded) return new[] { clip };
            }
            slicedFrom = clip;
            slices = Slice(clip, 4);
            return slices;
        }
    }

    static AudioClip[] Slice(AudioClip clip, int expected)
    {
        int channels = clip.channels;
        float[] data = new float[clip.samples * channels];
        if(!clip.GetData(data, 0)) return new[] { clip };

        //Loudness in 10 ms windows
        int window = Mathf.Max(1, clip.frequency / 100);
        int windows = clip.samples / window;
        float[] level = new float[windows];
        float loudest = 0f;
        for(int w = 0; w < windows; w++)
        {
            float sum = 0f;
            for(int i = w * window * channels; i < (w + 1) * window * channels; i++) sum += data[i] * data[i];
            level[w] = Mathf.Sqrt(sum / (window * channels));
            loudest = Mathf.Max(loudest, level[w]);
        }

        //A step starts where it gets loud again, at least 0.15 s after the last one
        var starts = new System.Collections.Generic.List<int>();
        bool quiet = true;
        int gap = 15;
        for(int w = 0; w < windows; w++)
        {
            bool loud = level[w] >= loudest * 0.3f;
            if(loud && quiet && (starts.Count == 0 || w - starts[starts.Count - 1] >= gap)) starts.Add(Mathf.Max(0, w - 1));
            if(level[w] < loudest * 0.1f) quiet = true;
            else if(loud) quiet = false;
        }
        if(starts.Count < 2)
        {
            starts.Clear();
            for(int i = 0; i < expected; i++) starts.Add(windows * i / expected);
        }

        var result = new AudioClip[starts.Count];
        for(int s = 0; s < starts.Count; s++)
        {
            int from = starts[s] * window;
            int to = s + 1 < starts.Count ? starts[s + 1] * window : clip.samples;
            int length = Mathf.Max(1, to - from);
            float[] part = new float[length * channels];
            System.Array.Copy(data, from * channels, part, 0, part.Length);
            //Short fade out so the cut doesn't click
            int fade = Mathf.Min(length, clip.frequency / 100);
            for(int i = 0; i < fade; i++)
            {
                float k = (float)i / fade;
                for(int c = 0; c < channels; c++) part[(length - 1 - i) * channels + c] *= k;
            }
            AudioClip piece = AudioClip.Create(clip.name + " step " + (s + 1), length, channels, clip.frequency, false);
            piece.SetData(part, 0);
            result[s] = piece;
        }
        Debug.Log($"SFXManager: cut {clip.name} into {result.Length} steps");
        return result;
    }

    public static SFXManager Instance;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void SetVolumeForSettings(bool up)
    {
        if (up)
        {
            audioSource.volume += 0.1f;
        }
        else
        {
            audioSource.volume -= 0.1f;
        }
    }

    public void SetSongVolume(float volume)
    {
        audioSource.volume = volume;
    }
    public float GetVolume()
    {
        return audioSource.volume;
    }

    public void SongState(bool state)
    {
        if (state == false)
            audioSource.Pause();
        else
            audioSource.Play();
    }

    //Safe from anywhere: does nothing in a scene without an SFXManager
    public static void Play(string sound)
    {
        if (Instance != null) Instance.PlaySFX(sound);
    }

    //Warhead explosion on its own source, quieter than the rest, so it can be faded out under the ending text
    public static void Explosion()
    {
        if (Instance == null || Instance._explosion == null) return;
        if (Instance.explosionSource == null)
        {
            GameObject obj = new GameObject("ExplosionAudio");
            obj.transform.SetParent(Instance.transform, false);
            Instance.explosionSource = obj.AddComponent<AudioSource>();
            Instance.explosionSource.playOnAwake = false;
            //Keeps going while the ending pauses the game, until it has faded
            Instance.explosionSource.ignoreListenerPause = true;
            GameAudio.SelfManaged.Add(Instance.explosionSource);
        }
        Instance.explosionFade = -1f;
        Instance.explosionSource.clip = Instance._explosion;
        Instance.explosionSource.volume = Instance.ExplosionLevel(1f);
        Instance.explosionSource.Play();
    }

    //Fade the explosion out over this many seconds (as the screen goes black for the ending text)
    public static void FadeOutExplosion(float seconds)
    {
        if (Instance == null || Instance.explosionSource == null || !Instance.explosionSource.isPlaying) return;
        Instance.explosionFadeTime = Mathf.Max(0.01f, seconds);
        Instance.explosionFade = 0f;
    }

    float ExplosionLevel(float k)
    {
        return explosionVolume * GameAudio.Get(GameAudio.Channel.Sfx) * k;
    }

    void Update()
    {
        if (explosionSource == null || explosionFade < 0f) return;
        //Real time: the ending has paused the game
        explosionFade += Time.unscaledDeltaTime;
        float k = 1f - explosionFade / explosionFadeTime;
        if (k <= 0f)
        {
            explosionSource.Stop();
            explosionFade = -1f;
            return;
        }
        explosionSource.volume = ExplosionLevel(k);
    }

    //Warhead alarm on / off
    public static void Alarm(bool on)
    {
        if (Instance == null || Instance._alarm == null) return;
        if (Instance.alarmSource == null)
        {
            Instance.alarmSource = Instance.gameObject.AddComponent<AudioSource>();
            Instance.alarmSource.playOnAwake = false;
            Instance.alarmSource.loop = true;
            Instance.alarmSource.clip = Instance._alarm;
        }
        if (on && !Instance.alarmSource.isPlaying) Instance.alarmSource.Play();
        else if (!on) Instance.alarmSource.Stop();
    }

    public void PlaySFX(string Sound)
    {


        if (Sound == "bear") audioSource.clip = _bear;
        else if (Sound == "item") audioSource.clip = _item;
        else if (Sound == "door") audioSource.clip = _door;
        else if (Sound == "lock") audioSource.clip = _lock;
        else if (Sound == "stab") audioSource.clip = _stab;
        else if (Sound == "chem") audioSource.clip = _chem;
        else if (Sound == "shoot") audioSource.clip = _shoot;
        else if (Sound == "note") audioSource.clip = _note;
        else if (Sound == "scan") audioSource.clip = _scan;
        else if (Sound == "gen") audioSource.clip = _gen;
        else if (Sound == "salvage") audioSource.clip = _salvage;
        else if (Sound == "bodyswap") audioSource.clip = _bodyswap;
        else if (Sound == "flash") audioSource.clip = _flash;
        else if (Sound == "explosion") audioSource.clip = _explosion;

        audioSource.Play();
    }
}