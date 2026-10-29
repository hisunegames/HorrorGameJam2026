using UnityEngine;
using UnityEngine.Rendering;

//A capsule that holds one body. CapsuleSpawner fills it with a random body type at the start.
//The body is a child of the capsule, drawn over the glass tinted like it's in the liquid; once the player swaps into it,
//it goes back to its normal sorting, leaves the capsule and the capsule switches to the used sprite.
//[F] Inspect while someone's in it: the player sizes up that body out loud (pros and cons, text in Lore.cs).
//Tools > Jam > Create Body Capsule makes one with the sprites already set
public class BodyCapsule : Interactable
{
    public SpriteRenderer capsuleRenderer;
    public Sprite unusedSprite;
    public Sprite usedSprite;
    //Where the body stands, from the capsule's pivot (bottom centre)
    public Vector2 bodyOffset = new Vector2(0f, 0.2f);
    //Optional: always this body instead of a random one
    public BodyScript forcedBody;
    [Tooltip("The glass art is solid, so the body is drawn over it tinted like it's in the liquid. White = no tint")]
    public Color insideTint = new Color(0.72f, 0.95f, 0.78f, 0.9f);

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

    protected override void OnEnable()
    {
        base.OnEnable();
        bodyManager = GameManager.Get<BodyManager>();
        if(bodyManager != null) bodyManager.OnBodySwapped += HandleSwap;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if(bodyManager != null) bodyManager.OnBodySwapped -= HandleSwap;
    }

    public override string Prompt(BodyScript body)
    {
        return Body != null && !IsUsed && Lore.CanInspect(Body.customBodySO) ? "Inspect" : null;
    }

    public override void Interact(BodyScript body)
    {
        SpeechBubble.Say(body.transform, Lore.InspectBody(Body.customBodySO), 4.5f);
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
        //Floating in the liquid: drawn over the (solid) glass, tinted green
        if(bodyRenderer != null) bodyRenderer.color = insideTint;
        if(capsuleRenderer == null) return;
        if(bodyGroup != null)
        {
            originalLayer = bodyGroup.sortingLayerID;
            originalOrder = bodyGroup.sortingOrder;
            SetBodySorting(capsuleRenderer.sortingLayerID, capsuleRenderer.sortingOrder + 1);
        }
        else if(bodyRenderer != null)
        {
            originalLayer = bodyRenderer.sortingLayerID;
            originalOrder = bodyRenderer.sortingOrder;
            SetBodySorting(capsuleRenderer.sortingLayerID, capsuleRenderer.sortingOrder + 1);
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
        //Out of the liquid
        if(bodyRenderer != null) bodyRenderer.color = Color.white;
        Body.transform.SetParent(transform.parent, true);
        //Opened, nothing to bump into any more. Only the capsule's own colliders, not the body's
        foreach(var col in GetComponents<Collider2D>())
        {
            col.enabled = false;
        }
        if(capsuleRenderer != null && usedSprite != null) capsuleRenderer.sprite = usedSprite;
    }
}
