using System.Collections;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class BodyScript : MonoBehaviour
{
    public bool isCurrentBody = false;
    [Header("Left Behind")]
    public bool isVacant = false;
    public bool isConsumed = false;
    public Color consumedColor = new Color(0.35f, 0.05f, 0.05f);
    [Header("BodyScriptableObject")]
    public CustomBodySO customBodySO;
    [Header("General Component")]
    public Rigidbody2D rb;
    [Header("Collapse (plays when the player leaves this body)")]
    //Disabled Animator whose default state is the fall. Tools > Jam > Set Up Collapse Animations makes it.
    //Kept off until then so it doesn't fight BodyFacing over the sprite
    public Animator collapseAnimator;
    //Set by PlayerSprint
    [HideInInspector] public float speedMultiplier = 1f;
    //Set by PlayDead: lying still, the monster doesn't count it as a target
    [HideInInspector] public bool isPlayingDead = false;
    //Set by BodyDrag while pulling a body (slower, no sprint)
    [HideInInspector] public float dragMultiplier = 1f;
    //In a locker: can't move, the monster doesn't see it
    [HideInInspector] public bool isHiding = false;
    //Researcher F's corpse has had its finger cut off
    [HideInInspector] public bool fingerTaken = false;

    public bool HasFingerprint => customBodySO != null && customBodySO.hasFingerprint && !fingerTaken;
    //Left-behind bodies and corpses can be dragged around
    public bool IsDraggable => !isCurrentBody && (isVacant || isConsumed);
    private string originalTag;
    private Vector2 moveInput;

    public bool IsBait => isVacant && !isConsumed;

    void Awake()
    {
        originalTag = gameObject.tag;
    }

    void Start()
    {
        if (!isCurrentBody)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }
    }

    void FixedUpdate()
    {
        if (isCurrentBody && (isPlayingDead || isHiding))
        {
            rb.linearVelocity = Vector2.zero;
        }
        else if (isCurrentBody)
        {
            moveInput = PlayerInput.Smooth(moveInput, PlayerInput.RawMove(), Time.fixedDeltaTime);

            rb.linearVelocity = moveInput*customBodySO.moveSpeed*customBodySO.SpeedMultiplier*speedMultiplier*dragMultiplier*GameManager.PlayerSpeedMultiplier;
        }
    }

    public void ChangeToNewBody()
    {
        isCurrentBody = true;
        rb.constraints = RigidbodyConstraints2D.None;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        gameObject.layer = LayerMask.NameToLayer("Player");
        gameObject.tag = "Player";
    }

    //Old body stays as a solid obstacle and becomes bait for the enemy, can't be re-entered
    public void LeftOldBody()
    {
        isCurrentBody = false;
        isVacant = true;
        rb.linearVelocity = Vector2.zero;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
        gameObject.layer = LayerMask.NameToLayer("Body");
        gameObject.tag = originalTag == "Player" ? "Untagged" : originalTag;
        if(collapseAnimator == null) collapseAnimator = GetComponent<Animator>();
        if(collapseAnimator != null && collapseAnimator.runtimeAnimatorController != null)
        {
            StartCoroutine(Collapse());
        }
    }

    //Plays the fall animation (non-looping, so it stays down). Keeps facing left/right as it was.
    //If the swap screen is coming up, the body is drawn on top of the black and animates in real time (the game is paused)
    IEnumerator Collapse()
    {
        BodyFacing facing = GetComponent<BodyFacing>();
        if(facing != null) facing.enabled = false;
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if(sr == null) yield break;
        SwapMemoryScreen swapScreen = GameManager.Get<SwapMemoryScreen>();
        bool overScreen = swapScreen != null && swapScreen.ShowsScreen;

        //Bodies sort through a Sorting Group if they have one, otherwise their sprite
        SortingGroup group = GetComponent<SortingGroup>();
        int oldLayer = group != null ? group.sortingLayerID : sr.sortingLayerID;
        int oldOrder = group != null ? group.sortingOrder : sr.sortingOrder;
        if(overScreen)
        {
            SortingLayer[] layers = SortingLayer.layers;
            SetSorting(group, sr, layers[layers.Length - 1].id, BlackScreenUI.CanvasSortingOrder + 10);
        }

        collapseAnimator.updateMode = overScreen ? AnimatorUpdateMode.UnscaledTime : AnimatorUpdateMode.Normal;
        collapseAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        collapseAnimator.speed = 1f;
        //Turning it on starts its default state (the fall) from the beginning
        collapseAnimator.enabled = true;

        if(overScreen)
        {
            //Fall and lie there on the black until the screen has gone
            yield return null;
            while(swapScreen != null && swapScreen.IsShowing) yield return null;
            collapseAnimator.updateMode = AnimatorUpdateMode.Normal;
            SetSorting(group, sr, oldLayer, oldOrder);
        }
    }

    static void SetSorting(SortingGroup group, SpriteRenderer sr, int layer, int order)
    {
        if(group != null)
        {
            group.sortingLayerID = layer;
            group.sortingOrder = order;
        }
        else
        {
            sr.sortingLayerID = layer;
            sr.sortingOrder = order;
        }
    }

    //Eaten by the monster or killed by the player: a corpse that can still be dragged, never entered again
    public void Consume()
    {
        isConsumed = true;
        isVacant = true;
        if(!isCurrentBody)
        {
            gameObject.layer = LayerMask.NameToLayer("Body");
            rb.linearVelocity = Vector2.zero;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if(sr == null) return;
        Sprite corpse = customBodySO != null ? customBodySO.corpseSprite : null;
        if(corpse != null)
        {
            //Nothing else gets to change the sprite now
            StopAllCoroutines();
            BodyFacing facing = GetComponent<BodyFacing>();
            if(facing != null) facing.enabled = false;
            if(collapseAnimator != null) collapseAnimator.enabled = false;
            sr.sprite = corpse;
            sr.color = Color.white;
        }
        else
        {
            sr.color = consumedColor;
        }
    }
}
