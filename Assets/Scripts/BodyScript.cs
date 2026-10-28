using Unity.Collections;
using UnityEngine;

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
    //Set by PlayerSprint
    [HideInInspector] public float speedMultiplier = 1f;
    private string originalTag;

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
        if (isCurrentBody)
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            rb.linearVelocity = new Vector2(horizontal,vertical)*customBodySO.moveSpeed*speedMultiplier*GameManager.PlayerSpeedMultiplier;
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
    }

    public void Consume()
    {
        isConsumed = true;
        //TODO swap to corpse sprite when art is in
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if(sr != null)
        {
            sr.color = consumedColor;
        }
    }
}
