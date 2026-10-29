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
    //Played by PlayerSprint on each step (bodies with their own Footstep Sounds use those instead)
    [SerializeField] AudioClip _footsteps;

    public static AudioClip Footstep => Instance != null ? Instance._footsteps : null;

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

        audioSource.Play();
    }
}