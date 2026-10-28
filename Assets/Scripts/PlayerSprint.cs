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

    public float Stamina { get; private set; }
    public float Stamina01 => maxStamina > 0f ? Stamina / maxStamina : 0f;
    public bool IsSprinting { get; private set; }
    public bool IsExhausted { get; private set; }

    private float regenTimer;
    private float noiseTimer;
    private BodyScript lastBody;

    void Awake()
    {
        if(bodyManager == null) bodyManager = FindAnyObjectByType<BodyManager>();
        Stamina = maxStamina;
    }

    void Update()
    {
        BodyScript body = bodyManager != null ? bodyManager.currentBody : null;
        if(lastBody != null && lastBody != body) lastBody.speedMultiplier = 1f;
        lastBody = body;
        if(body == null || Time.timeScale == 0f) return;

        bool moving = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).sqrMagnitude > 0.01f;
        //Slow bodies can't sprint
        bool canSprint = body.customBodySO == null || !body.customBodySO.isSlow;
        IsSprinting = canSprint && moving && Input.GetKey(sprintKey) && !IsExhausted && Stamina > 0f;

        if(IsSprinting)
        {
            Stamina = Mathf.Max(0f, Stamina - drainPerSecond * Time.deltaTime);
            regenTimer = regenDelay;
            if(Stamina <= 0f) IsExhausted = true;

            noiseTimer -= Time.deltaTime;
            if(noiseTimer <= 0f)
            {
                noiseTimer = noiseInterval;
                Noise.Make(body.transform.position, noiseLoudness);
            }
        }
        else
        {
            noiseTimer = 0f;
            if(regenTimer > 0f) regenTimer -= Time.deltaTime;
            else Stamina = Mathf.Min(maxStamina, Stamina + regenPerSecond * Time.deltaTime);
            if(IsExhausted && Stamina >= recoverAt) IsExhausted = false;
        }

        body.speedMultiplier = IsSprinting ? sprintMultiplier : 1f;
    }
}
