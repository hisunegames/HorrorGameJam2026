using UnityEngine;
using UnityEngine.Rendering;

//A capsule that holds one body. CapsuleSpawner fills it with a random body type at the start.
//The body is a child of the capsule and drawn just behind it; once the player swaps into it,
//it goes back to its normal sorting, leaves the capsule and the capsule switches to the used sprite.
//Tools > Jam > Create Body Capsule makes one with the sprites already set
public class BodyCapsule : MonoBehaviour
{
    public SpriteRenderer capsuleRenderer;
    public Sprite unusedSprite;
    public Sprite usedSprite;
    //Where the body stands, from the capsule's pivot (bottom centre)
    public Vector2 bodyOffset = new Vector2(0f, 0.2f);
    //Optional: always this body instead of a random one
    public BodyScript forcedBody;

    public BodyScript Body { get; private set; }
    public bool IsUsed { get; private set; }

    private BodyManager bodyManager;
    //Body's own sorting, put back when it leaves the capsule
    private SortingGroup bodyGroup;
    private SpriteRenderer bodyRenderer;
    private int originalLayer;
    private int originalOrder;

    void Awake()
    {
        if(capsuleRenderer == null) capsuleRenderer = GetComponent<SpriteRenderer>();
        if(capsuleRenderer != null && unusedSprite != null) capsuleRenderer.sprite = unusedSprite;
    }

    void OnEnable()
    {
        bodyManager = GameManager.Get<BodyManager>();
        if(bodyManager != null) bodyManager.OnBodySwapped += HandleSwap;
    }

    void OnDisable()
    {
        if(bodyManager != null) bodyManager.OnBodySwapped -= HandleSwap;
    }

    //Called by CapsuleSpawner
    public void Fill(BodyScript prefab)
    {
        if(Body != null || prefab == null) return;
        Vector3 pos = transform.position + (Vector3)bodyOffset;
        Body = Instantiate(prefab, pos, Quaternion.identity, transform);
        Body.name = prefab.name;
        Body.isCurrentBody = false;

        //Bodies sort through a Sorting Group (Player&Monster layer) if they have one, otherwise their sprite
        bodyGroup = Body.GetComponent<SortingGroup>();
        bodyRenderer = Body.GetComponent<SpriteRenderer>();
        if(capsuleRenderer == null) return;
        if(bodyGroup != null)
        {
            originalLayer = bodyGroup.sortingLayerID;
            originalOrder = bodyGroup.sortingOrder;
            SetBodySorting(capsuleRenderer.sortingLayerID, capsuleRenderer.sortingOrder - 1);
        }
        else if(bodyRenderer != null)
        {
            originalLayer = bodyRenderer.sortingLayerID;
            originalOrder = bodyRenderer.sortingOrder;
            SetBodySorting(capsuleRenderer.sortingLayerID, capsuleRenderer.sortingOrder - 1);
        }
    }

    void SetBodySorting(int layer, int order)
    {
        if(bodyGroup != null)
        {
            bodyGroup.sortingLayerID = layer;
            bodyGroup.sortingOrder = order;
        }
        else if(bodyRenderer != null)
        {
            bodyRenderer.sortingLayerID = layer;
            bodyRenderer.sortingOrder = order;
        }
    }

    void HandleSwap(BodyScript oldBody, BodyScript newBody, int swapCount)
    {
        if(IsUsed || Body == null || newBody != Body) return;
        IsUsed = true;
        //Out of the glass: normal sorting again (in front of props) and no longer moves with the capsule
        SetBodySorting(originalLayer, originalOrder);
        Body.transform.SetParent(transform.parent, true);
        //Opened, nothing to bump into any more. Only the capsule's own colliders, not the body's
        foreach(var col in GetComponents<Collider2D>())
        {
            col.enabled = false;
        }
        if(capsuleRenderer != null && usedSprite != null) capsuleRenderer.sprite = usedSprite;
    }
}
