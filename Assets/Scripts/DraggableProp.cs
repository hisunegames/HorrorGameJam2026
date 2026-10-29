using System.Collections.Generic;
using UnityEngine;

//A prop that can be dragged like a body (chair, a corpse prop). Stand next to it and hold the drag key.
//A chair can be stood on in a puddle; a corpse prop lying in one makes it safe (WaterPuddle checks both).
//Needs a Rigidbody2D, added if missing
public class DraggableProp : MonoBehaviour
{
    public static readonly List<DraggableProp> All = new List<DraggableProp>();

    [Tooltip("Chair: standing next to it on a puddle counts as standing on it (no shock)")]
    public bool canStandOn = true;
    public float standRadius = 0.5f;
    [Tooltip("A dead body prop: leaves a blood trail when dragged, and lying in a puddle makes it safe like a real body")]
    public bool isBody = false;
    //Set once it's been dragged and left blood: the monster smells it and comes to eat it, like a real body
    public bool hasBled = false;
    public bool eaten = false;
    public Color eatenColor = new Color(0.35f, 0.05f, 0.05f);

    public bool IsBait => isBody && hasBled && !eaten;

    //Eaten by the monster
    public void Consume()
    {
        eaten = true;
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if(sr != null) sr.color = eatenColor;
    }

    public Rigidbody2D Rb { get; private set; }

    void Awake()
    {
        Rb = GetComponent<Rigidbody2D>();
        if(Rb == null) Rb = gameObject.AddComponent<Rigidbody2D>();
        Rb.gravityScale = 0f;
        Rb.constraints = RigidbodyConstraints2D.FreezeAll;
        Rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        //Same layer as bodies: the player can step onto it (GameManager.walkOverBodies), walls still stop it
        int bodyLayer = LayerMask.NameToLayer("Body");
        if(bodyLayer >= 0) gameObject.layer = bodyLayer;
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public bool IsStandingOn(Vector2 position)
    {
        return canStandOn && Vector2.Distance(position, transform.position) <= standRadius;
    }
}
