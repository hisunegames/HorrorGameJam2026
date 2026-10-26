using Unity.Collections;
using UnityEngine;

public class BodyScript : MonoBehaviour
{
    public bool isCurrentBody = false;
    [Header("BodyScriptableObject")]
    public CustomBodySO customBodySO;
    [Header("General Component")]
    public Rigidbody2D rb;
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

            rb.linearVelocity = new Vector2(horizontal,vertical)*customBodySO.moveSpeed;
        }
    }

    public void ChangeToNewBody()
    {
        isCurrentBody = true;
        rb.constraints = RigidbodyConstraints2D.None;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        gameObject.layer = LayerMask.NameToLayer("Player");
    }

    public void LeftOldBody()
    {
        isCurrentBody = false;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
    }
}
