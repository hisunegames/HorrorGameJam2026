using UnityEngine;

//The big machine: [F] with a toolbox to strip it. Gives the spare parts (generator) and drops a note. Once only
public class SalvageMachine : Interactable
{
    public ItemData toolbox;
    public ItemData spareParts;
    [Tooltip("Seconds you have to stay at it (walk off or swap and it starts over). Makes noise")]
    public float salvageTime = 2f;
    public float salvageNoise = 0.6f;

    [Header("Note that falls out")]
    public string noteTitle = "Maintenance log";
    [TextArea(4, 12)] public string noteBody;
    public Sprite noteImage;
    //How the note looks on the floor (Notes sprite)
    public Sprite noteWorldSprite;
    public Vector2 dropOffset = new Vector2(0f, -1f);

    [Header("Look")]
    //Optional: stripped machine
    public Sprite salvagedSprite;

    public bool salvaged;
    private float progress = -1f;
    private BodyScript worker;

    void Reset()
    {
        range = 1.6f;
        toolbox = ItemData.Find("Toolbox");
        spareParts = ItemData.Find("SpareParts");
#if UNITY_EDITOR
        noteWorldSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/PropNFurniture/Notes.png");
#endif
    }

    public override string Prompt(BodyScript body)
    {
        if(salvaged) return null;
        if(progress >= 0f) return "Salvaging...";
        return Inventory != null && Inventory.Has(toolbox) ? "Salvage machine" : "Machine (needs a toolbox)";
    }

    public override bool CanInteract(BodyScript body)
    {
        return !salvaged && progress < 0f && Inventory != null && Inventory.Has(toolbox);
    }

    protected override string DefaultThought(BodyScript body)
    {
        return "Everything's bolted down. I'd need a toolbox to strip this.";
    }

    public override void Interact(BodyScript body)
    {
        worker = body;
        progress = 0f;
        if(salvageNoise > 0f) Noise.Make(transform.position, salvageNoise);
    }

    void Update()
    {
        if(progress < 0f) return;
        //Walked off or swapped: start over
        if(worker == null || !worker.isCurrentBody || DistanceFrom(worker) > range + 0.5f)
        {
            progress = -1f;
            return;
        }
        progress += Time.deltaTime;
        if(progress < salvageTime) return;

        progress = -1f;
        salvaged = true;
        if(salvagedSprite != null) GetComponent<SpriteRenderer>().sprite = salvagedSprite;
        Vector2 drop = (Vector2)transform.position + dropOffset;
        if(spareParts != null) Inventory.Give(spareParts, drop + Vector2.right * 0.4f);
        NoteObject.Spawn(drop, noteTitle, noteBody, noteImage, noteWorldSprite);
    }
}
