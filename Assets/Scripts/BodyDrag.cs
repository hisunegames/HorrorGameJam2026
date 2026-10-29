using UnityEngine;

//Press the drag key next to a left-behind body, a corpse or a chair (DraggableProp) to pull it along behind you
//(slower, no sprinting). Press it again to drop it. Dragged bodies leave a blood trail the monster follows.
//Put it on the GameManager
public class BodyDrag : MonoBehaviour
{
    public BodyManager bodyManager;
    public KeyCode dragKey = KeyCode.G;
    public float grabRange = 1f;
    //How far behind you it trails
    public float trailDistance = 0.7f;
    //Your speed while dragging
    [Range(0.1f, 1f)] public float dragSpeedMultiplier = 0.6f;
    public float followSpeed = 8f;
    [Tooltip("A blood drop every this many units a body is dragged (0 = no blood)")]
    public float bloodSpacing = 0.35f;
    [Tooltip("Blood smear sprite (BLoodPuddle). Empty = small generated blobs")]
    public Sprite bloodSprite;

    void Reset()
    {
#if UNITY_EDITOR
        bloodSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/PropNFurniture/BLoodPuddle.png");
#endif
    }

    public bool IsDragging => draggedRb != null;
    public BodyScript Dragged { get; private set; }
    //Corpses (killed / eaten), bodies whose type bleeds (BlindGuy) and corpse props leave blood. Chairs and
    //ordinary bodies you swapped out of don't
    bool LeavesBlood
    {
        get
        {
            if(draggedRb == null) return false;
            if(Dragged != null) return Dragged.isConsumed || (Dragged.customBodySO != null && Dragged.customBodySO.bleedsWhenDragged);
            return draggedProp != null && draggedProp.isBody;
        }
    }
    private DraggableProp draggedProp;

    private Rigidbody2D draggedRb;
    private BodyScript dragger;
    private Vector2 facing = Vector2.down;
    private Vector2 lastBlood;

    void Awake()
    {
        if(bodyManager == null) bodyManager = GameManager.Get<BodyManager>();
    }

    void Update()
    {
        BodyScript body = bodyManager != null ? bodyManager.currentBody : null;
        bool pressed = Input.GetKeyDown(dragKey) && Time.timeScale > 0f && !ModalUI.IsOpen;
        //Pressed again to drop it, or swapped / playing dead / hiding / the body stopped being draggable
        if(IsDragging && (pressed || body != dragger || body.isPlayingDead || body.isHiding || (Dragged != null && !Dragged.IsDraggable)))
        {
            Release();
            //That press was the drop, not a new grab
            pressed = false;
        }
        if(!IsDragging && pressed && body != null && !body.isPlayingDead && !body.isHiding)
        {
            Component near = NearestDraggable(body);
            if(near != null) Grab(body, near);
        }
        if(LeavesBlood && bloodSpacing > 0f && Vector2.Distance(draggedRb.position, lastBlood) >= bloodSpacing)
        {
            lastBlood = draggedRb.position;
            BloodTrail.Add(lastBlood, bloodSprite);
            //A corpse prop that's bled is bait from now on
            if(draggedProp != null) draggedProp.hasBled = true;
        }
    }

    void FixedUpdate()
    {
        if(!IsDragging || dragger == null) return;
        Vector2 v = dragger.rb.linearVelocity;
        if(v.sqrMagnitude > 0.01f) facing = v.normalized;
        Vector2 spot = dragger.rb.position - facing * trailDistance;
        Vector2 to = spot - draggedRb.position;
        //Only pull when it's lagging, so it doesn't jitter right behind you
        draggedRb.linearVelocity = to.magnitude > 0.05f ? Vector2.ClampMagnitude(to * followSpeed, followSpeed) : Vector2.zero;
    }

    //Called by RoomDoor after it moves the player: bring the dragged thing along, trailing back towards the door
    public void CarryThrough(BodyScript body, Vector2 exitPoint, Vector2 inward)
    {
        if(!IsDragging || body != dragger) return;
        facing = inward.sqrMagnitude > 0.001f ? inward.normalized : facing;
        draggedRb.linearVelocity = Vector2.zero;
        draggedRb.position = exitPoint;
        draggedRb.transform.position = exitPoint;
        //New trail starts here (no smear across the gap between rooms)
        lastBlood = exitPoint;
        if(LeavesBlood && bloodSpacing > 0f) BloodTrail.Add(exitPoint, bloodSprite);
    }

    //A BodyScript or a DraggableProp within reach
    public Component NearestDraggable(BodyScript body)
    {
        Component best = null;
        float bestDist = grabRange;
        foreach(var other in FindObjectsByType<BodyScript>())
        {
            if(other == body || !other.IsDraggable) continue;
            float d = Vector2.Distance(other.transform.position, body.transform.position);
            if(d <= bestDist)
            {
                bestDist = d;
                best = other;
            }
        }
        foreach(var prop in DraggableProp.All)
        {
            float d = Vector2.Distance(prop.transform.position, body.transform.position);
            if(d <= bestDist)
            {
                bestDist = d;
                best = prop;
            }
        }
        return best;
    }

    void Grab(BodyScript body, Component target)
    {
        dragger = body;
        Dragged = target as BodyScript;
        draggedProp = target as DraggableProp;
        draggedRb = Dragged != null ? Dragged.rb : draggedProp.Rb;
        Vector2 away = (Vector2)(body.transform.position - target.transform.position);
        facing = away.sqrMagnitude > 0.001f ? away.normalized : Vector2.down;
        lastBlood = draggedRb.position;
        draggedRb.constraints = RigidbodyConstraints2D.FreezeRotation;
        SetIgnoreCollision(true);
        dragger.dragMultiplier = dragSpeedMultiplier;
    }

    void Release()
    {
        if(draggedRb != null)
        {
            draggedRb.linearVelocity = Vector2.zero;
            draggedRb.constraints = RigidbodyConstraints2D.FreezeAll;
            SetIgnoreCollision(false);
        }
        if(dragger != null) dragger.dragMultiplier = 1f;
        draggedRb = null;
        Dragged = null;
        draggedProp = null;
        dragger = null;
    }

    //Don't shove the thing you're pulling
    void SetIgnoreCollision(bool ignore)
    {
        if(dragger == null || draggedRb == null) return;
        foreach(var a in dragger.GetComponents<Collider2D>())
        {
            foreach(var b in draggedRb.GetComponents<Collider2D>())
            {
                Physics2D.IgnoreCollision(a, b, ignore);
            }
        }
    }
}
