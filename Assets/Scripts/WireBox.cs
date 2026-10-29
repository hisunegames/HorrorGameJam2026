using UnityEngine;

//Electric box with loose wiring: [F] to pull out the wire (generator part). Doing it while standing in a puddle
//kills you, unless the body has rubber boots (Major), a body is lying in the water, or you're on a chair in it
public class WireBox : Interactable
{
    public ItemData wire;
    //Puddles that count; empty = every puddle in the scene
    public WaterPuddle[] puddles;
    public bool taken;
    //Optional: box after the wire's gone
    public Sprite emptySprite;

    void Reset()
    {
        range = 1.2f;
        wire = ItemData.Find("Wire");
    }

    public override string Prompt(BodyScript body)
    {
        return taken ? null : "Pull out the wire";
    }

    public override bool CanInteract(BodyScript body)
    {
        return !taken && Inventory != null && !Inventory.IsFull;
    }

    protected override string DefaultThought(BodyScript body)
    {
        return "Loose wiring. Still live.";
    }

    public override void Interact(BodyScript body)
    {
        bool boots = body.customBodySO != null && body.customBodySO.rubberBoots;
        if(!boots && InDangerousWater(body))
        {
            Enemy.KillPlayer("You pulled the live wire standing in water.");
            return;
        }
        taken = true;
        if(emptySprite != null) GetComponent<SpriteRenderer>().sprite = emptySprite;
        Inventory.Give(wire, body.transform.position);
    }

    bool InDangerousWater(BodyScript body)
    {
        WaterPuddle[] list = puddles != null && puddles.Length > 0 ? puddles : FindObjectsByType<WaterPuddle>();
        foreach(var puddle in list)
        {
            if(puddle != null && puddle.IsDangerFor(body)) return true;
        }
        return false;
    }
}
