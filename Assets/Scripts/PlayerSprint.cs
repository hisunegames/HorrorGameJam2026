using UnityEngine;

//Hold sprint to move faster. Drains stamina and makes noise the monster can hear.
//Put it next to BodyManager; works for whichever body is current
public class PlayerSprint : MonoBehaviour
{
    public BodyManager bodyManager;
    public KeyCode sprintKey = KeyCode.LeftShift;
    public float sprintMultiplier = 1.6f;

    [Header("Stamina")]
    public float maxStamina = 100f;
    public float drainPerSecond = 35f;
    public float regenPerSecond = 20f;
    //Wait after sprinting before stamina comes back
    public float regenDelay = 0.8f;
    //Once empty you can't sprint again until it refills to this
    public float recoverAt = 30f;

    [Header("Noise")]
    public float noiseLoudness = 1f;
    public float noiseInterval = 0.35f;
    //Walking steps are slower than running ones (only matters for heavy-boot bodies)
    public float walkNoiseInterval = 0.5f;
    [Range(0f, 1f)] public float footstepVolume = 0.6f;
    private AudioSource stepSource;

    public float Stamina { get; private set; }
    public float Stamina01 => MaxStamina > 0f ? Stamina / MaxStamina : 0f;
    //Same for every body (No Legs' bonus only makes playing dead last longer, see PlayDead)
    public float MaxStamina => maxStamina;
    public bool IsSprinting { get; private set; }
    public bool IsExhausted { get; private set; }
    //Adrenaline: boosted (free, faster sprinting) then crashing (slow, no sprint, no stamina regen)
    public bool IsBoosted => boostTimer > 0f;
    public bool IsCrashing => !IsBoosted && crashTimer > 0f;

    private float boostTimer;
    private float crashTimer;
    private float boostSpeed = 1f;
    private float crashSpeed = 1f;

    private float regenTimer;
    private float noiseTimer;
    private BodyScript lastBody;

    void Awake()
    {
        if(bodyManager == null) bodyManager = GameManager.Get<BodyManager>();
        Stamina = maxStamina;
    }

    void Update()
    {
        BodyScript body = bodyManager != null ? bodyManager.currentBody : null;
        if(lastBody != null && lastBody != body) lastBody.speedMultiplier = 1f;
        //New body may have a smaller stamina pool
        if(Stamina > MaxStamina) Stamina = MaxStamina;
        lastBody = body;
        if(body == null || Time.timeScale == 0f) return;

        if(boostTimer > 0f)
        {
            boostTimer -= Time.deltaTime;
            Stamina = MaxStamina;
            IsExhausted = false;
        }
        else if(crashTimer > 0f) crashTimer -= Time.deltaTime;

        bool moving = !body.isPlayingDead && !body.isHiding && !ModalUI.IsOpen && PlayerInput.RawMove().sqrMagnitude > 0.01f;
        //Slow bodies can't sprint, nor can you while dragging or crashing
        bool canSprint = (body.customBodySO == null || !body.customBodySO.isSlow) && body.dragMultiplier >= 1f && !IsCrashing;
        IsSprinting = canSprint && moving && Input.GetKey(sprintKey) && !IsExhausted && Stamina > 0f;

        if(IsSprinting && !IsBoosted)
        {
            Stamina = Mathf.Max(0f, Stamina - drainPerSecond * Time.deltaTime);
            regenTimer = regenDelay;
            if(Stamina <= 0f) IsExhausted = true;
        }
        else if(!IsSprinting && !IsCrashing)
        {
            if(regenTimer > 0f) regenTimer -= Time.deltaTime;
            else Stamina = Mathf.Min(MaxStamina, Stamina + regenPerSecond * Time.deltaTime);
            if(IsExhausted && Stamina >= recoverAt) IsExhausted = false;
        }

        float speed = IsSprinting ? sprintMultiplier : 1f;
        if(IsBoosted && IsSprinting) speed *= boostSpeed;
        if(IsCrashing) speed = crashSpeed;
        body.speedMultiplier = speed;
        UpdateFootsteps(body, moving);
    }

    //Adrenaline shot: full stamina that doesn't drain and faster sprinting, then the crash
    public void ApplyAdrenaline(ItemData shot)
    {
        boostTimer = shot.boostTime;
        crashTimer = shot.crashTime;
        boostSpeed = shot.boostSpeed;
        crashSpeed = shot.crashSpeed;
        Stamina = MaxStamina;
        IsExhausted = false;
    }

    //Used by PlayDead. Holds off regen like sprinting does. False once stamina is empty
    public bool Drain(float amount)
    {
        Stamina = Mathf.Max(0f, Stamina - amount);
        regenTimer = regenDelay;
        if(Stamina <= 0f) IsExhausted = true;
        return Stamina > 0f;
    }

    //Sprinting is always loud; bodies with heavy boots (walkNoise) are heard walking too
    void UpdateFootsteps(BodyScript body, bool moving)
    {
        CustomBodySO so = body.customBodySO;
        float loudness = 0f;
        float interval = noiseInterval;
        if(IsSprinting)
        {
            loudness = noiseLoudness * (so != null ? so.sprintNoiseMultiplier : 1f);
        }
        else if(moving && so != null && so.walkNoise > 0f)
        {
            loudness = so.walkNoise;
            interval = walkNoiseInterval;
        }

        if(loudness <= 0f)
        {
            noiseTimer = 0f;
            return;
        }
        noiseTimer -= Time.deltaTime;
        if(noiseTimer > 0f) return;
        noiseTimer = interval;
        Noise.Make(body.transform.position, loudness);
        PlayStep(so);
    }

    void PlayStep(CustomBodySO so)
    {
        //The body's own steps if it has any, otherwise the shared one on the SFXManager
        bool own = so != null && so.footstepSounds != null && so.footstepSounds.Length > 0;
        AudioClip clip = own ? so.footstepSounds[Random.Range(0, so.footstepSounds.Length)] : SFXManager.Footstep;
        if(clip == null) return;
        if(stepSource == null)
        {
            stepSource = gameObject.AddComponent<AudioSource>();
            stepSource.playOnAwake = false;
            stepSource.spatialBlend = 0f;
        }
        stepSource.pitch = Random.Range(0.9f, 1.1f);
        stepSource.PlayOneShot(clip, footstepVolume);
    }
}
