using UnityEngine;

//Swaps the body's sprite to face the way it's moving. Left uses the side sprite flipped.
//Standing still keeps the last facing
public class BodyFacing : MonoBehaviour
{
    public Sprite frontSprite; //down
    public Sprite backSprite;  //up
    public Sprite sideSprite;  //right
    [Tooltip("The side art faces left instead of right (the Amalgam's)")]
    public bool sideFacesLeft = false;
    public float minSpeed = 0.1f;

    private SpriteRenderer sr;
    private Rigidbody2D rb;

    void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        rb = GetComponentInParent<Rigidbody2D>();
    }

    void Update()
    {
        if(sr == null || rb == null) return;

        Vector2 velocity = rb.linearVelocity;
        if(velocity.sqrMagnitude < minSpeed * minSpeed) return;

        if(Mathf.Abs(velocity.x) > Mathf.Abs(velocity.y))
        {
            SetSprite(sideSprite, sideFacesLeft ? velocity.x > 0f : velocity.x < 0f);
        }
        else
        {
            SetSprite(velocity.y > 0f ? backSprite : frontSprite, false);
        }
    }

    void SetSprite(Sprite sprite, bool flip)
    {
        if(sprite != null) sr.sprite = sprite;
        sr.flipX = flip;
    }
}
