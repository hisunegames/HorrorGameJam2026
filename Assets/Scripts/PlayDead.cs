using UnityEngine;

//Bodies with CustomBodySO.canPlayDead can hold a key to drop flat (their collapse animation) and lie still.
//The monster stops treating them as a target and won't grab them even if it walks right over them.
//Drains stamina; running out gets you back up, exhausted. Put it on the GameManager next to PlayerSprint
public class PlayDead : MonoBehaviour
{
    public BodyManager bodyManager;
    public PlayerSprint sprint;
    public KeyCode playDeadKey = KeyCode.C;
    //Stamina used per second lying still: 100 stamina lasts 12.5 s, No Legs (x1.3) about 16 s
    public float staminaDrainPerSecond = 8f;
    //Need at least this much stamina to drop
    public float minStaminaToStart = 10f;
    //Collapse animation speed when dropping on purpose (leaving a body plays it at 1x)
    public float dropAnimationSpeed = 3f;

    public bool IsPlayingDead => body != null && body.isPlayingDead;

    private BodyScript body;
    private Sprite standingSprite;
    private bool standingFlip;

    void Awake()
    {
        if(bodyManager == null) bodyManager = GameManager.Get<BodyManager>();
        if(sprint == null) sprint = GameManager.Get<PlayerSprint>();
    }

    void Update()
    {
        BodyScript current = bodyManager != null ? bodyManager.currentBody : null;
        //Body changed under us (shouldn't happen, swapping is blocked): let go of the old one
        if(body != null && body != current && body.isPlayingDead) GetUp();
        if(Time.timeScale == 0f || current == null) return;

        bool can = current.customBodySO != null && current.customBodySO.canPlayDead;
        bool holding = can && Input.GetKey(playDeadKey);

        if(!current.isPlayingDead)
        {
            bool enoughStamina = sprint == null || (!sprint.IsExhausted && sprint.Stamina >= minStaminaToStart);
            if(holding && Input.GetKeyDown(playDeadKey) && enoughStamina) Drop(current);
            return;
        }

        //Bodies built for it (No Legs) last longer lying still
        float lasts = current.customBodySO != null ? Mathf.Max(0.1f, current.customBodySO.staminaMultiplier) : 1f;
        bool stillHasStamina = sprint == null || sprint.Drain(staminaDrainPerSecond / lasts * Time.deltaTime);
        if(!holding || !stillHasStamina) GetUp();
    }

    void Drop(BodyScript target)
    {
        body = target;
        body.isPlayingDead = true;
        body.rb.linearVelocity = Vector2.zero;

        BodyFacing facing = body.GetComponent<BodyFacing>();
        if(facing != null) facing.enabled = false;
        SpriteRenderer sr = body.GetComponent<SpriteRenderer>();
        if(sr != null)
        {
            standingSprite = sr.sprite;
            standingFlip = sr.flipX;
        }
        Animator animator = Collapse(body);
        if(animator != null)
        {
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.speed = dropAnimationSpeed;
            //Turning it on plays the default state (the fall) from the start
            animator.enabled = true;
        }
    }

    void GetUp()
    {
        if(body == null) return;
        body.isPlayingDead = false;
        //Swapped away or left behind: the body keeps lying there, BodyScript handles it
        if(body.isCurrentBody)
        {
            Animator animator = Collapse(body);
            if(animator != null)
            {
                animator.enabled = false;
                animator.speed = 1f;
            }
            SpriteRenderer sr = body.GetComponent<SpriteRenderer>();
            if(sr != null && standingSprite != null)
            {
                sr.sprite = standingSprite;
                sr.flipX = standingFlip;
            }
            BodyFacing facing = body.GetComponent<BodyFacing>();
            if(facing != null) facing.enabled = true;
        }
        body = null;
    }

    static Animator Collapse(BodyScript b)
    {
        Animator animator = b.collapseAnimator != null ? b.collapseAnimator : b.GetComponent<Animator>();
        return animator != null && animator.runtimeAnimatorController != null ? animator : null;
    }
}
