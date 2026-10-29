using UnityEngine;

//An item lying on the floor. [F] picks it up into the PlayerInventory. Spawned when items are dropped,
//or placed with Tools > Jam > Create Item Pickup
public class WorldItem : Interactable
{
    public ItemData item;
    //Uses left (weapons). -1 = full
    public int charges = -1;

    protected override void OnEnable()
    {
        base.OnEnable();
        ApplySprite();
    }

    void Reset()
    {
        range = 0.8f;
    }

    void OnValidate()
    {
        ApplySprite();
    }

    void ApplySprite()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if(sr != null && item != null && item.worldSprite != null) sr.sprite = item.worldSprite;
    }

    public override string Prompt(BodyScript body)
    {
        if(item == null) return null;
        PlayerInventory inv = Inventory;
        if(inv == null) return null;
        return inv.IsFull ? "Hands full" : "Pick up " + item.itemName;
    }

    public override bool CanInteract(BodyScript body)
    {
        PlayerInventory inv = Inventory;
        return inv != null && !inv.IsFull;
    }

    protected override string DefaultThought(BodyScript body)
    {
        return "My hands are full.";
    }

    public override void Interact(BodyScript body)
    {
        if(Inventory.Add(item, charges)) Destroy(gameObject);
    }

    public static WorldItem Spawn(ItemData item, int charges, Vector2 position)
    {
        GameObject obj = new GameObject(item.itemName);
        obj.transform.position = position;
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        if(SortingLayer.NameToID("Prop") != 0) sr.sortingLayerName = "Prop";
        sr.sortingOrder = 5;
        WorldItem worldItem = obj.AddComponent<WorldItem>();
        worldItem.range = 0.8f;
        worldItem.item = item;
        worldItem.charges = charges;
        worldItem.ApplySprite();
        return worldItem;
    }
}
