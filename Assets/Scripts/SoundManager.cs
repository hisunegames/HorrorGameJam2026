using UnityEngine;

public class SoundManager : MonoBehaviour
{
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip _menuSong;
    [SerializeField] AudioClip _introSong;
    [SerializeField] AudioClip _postIntroSong;
    [SerializeField] AudioClip _bgmSong;
    [SerializeField] AudioClip _bridgeSong;
    [SerializeField] AudioClip _postBridgeSong;
    [SerializeField] AudioClip _chorusSong;
    [SerializeField] AudioClip _endSong;

    public SoundManager Instance;
    public string curQueue = "";
    string Song = "";
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

    public void QueueMusic(string Song)
    {
        curQueue = Song;
        float curSongLength = audioSource.clip.length - audioSource.time;
        audioSource.loop = false;
        Invoke(nameof(ChangeMusic), curSongLength);
    }

    public void ChangeMusic()
    {

        if (curQueue != "") Song = curQueue;
        curQueue = "";
        audioSource.loop = true;

        if (Song == "Menu") audioSource.clip = _menuSong;

        else if (Song == "Intro")

        {
            audioSource.clip = _introSong;
            QueueMusic("PostIntro");
        }

        else if (Song == "PostIntro")
        {
            audioSource.clip = _postIntroSong;
            QueueMusic("BGM");
        }

        else if (Song == "BGM") audioSource.clip = _bgmSong;

        else if (Song == "Bridge")
        {
            audioSource.clip = _bridgeSong;
            QueueMusic("PostBridge");
        }

        else if (Song == "PostBridge")
        {
            audioSource.clip = _postBridgeSong;
            QueueMusic("Chorus");
        }
        else if (Song == "Chorus") audioSource.clip = _chorusSong;
        else if (Song == "End") audioSource.clip = _endSong;

        audioSource.Play();
    }
}