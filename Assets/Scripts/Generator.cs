using System;
using UnityEngine;

//The generator: [F] with a charged battery, spare parts and wire to get it running. Opens the exit (ExitDoor)
public class Generator : Interactable
{
    public static event Action OnFixed;
    public static bool IsFixed { get; private set; }

    public ItemData chargedBattery;
    public ItemData spareParts;
    public ItemData wire;
    public float startNoise = 1.5f;
    //Optional: running generator
    public Sprite runningSprite;

    //Parts already put in (you can bring them one at a time)
    public bool hasBattery, hasParts, hasWire;

    bool fixedOnce = false;

    void Reset()
    {
        range = 1.6f;
        chargedBattery = ItemData.Find("ChargedBattery");
        spareParts = ItemData.Find("SpareParts");
        wire = ItemData.Find("Wire");
    }

    void Awake()
    {
        IsFixed = false;
    }

    public override string Prompt(BodyScript body)
    {
        if(IsFixed) return null;
        return CanInteract(body) ? "Fit parts" : "Generator (needs " + Missing() + ")";
    }

    public override bool CanInteract(BodyScript body)
    {
        PlayerInventory inv = Inventory;
        return !IsFixed && inv != null && ((!hasBattery && inv.Has(chargedBattery)) || (!hasParts && inv.Has(spareParts)) || (!hasWire && inv.Has(wire)));
    }

    protected override string DefaultThought(BodyScript body)
    {
        return "It's dead. Still missing the " + Missing() + ".";
    }

    public override void Interact(BodyScript body)
    {
        PlayerInventory inv = Inventory;
        if(!hasBattery && inv.Remove(chargedBattery)) hasBattery = true;
        if(!hasParts && inv.Remove(spareParts)) hasParts = true;
        if(!hasWire && inv.Remove(wire)) hasWire = true;
        if ((hasBattery || hasParts || hasWire) && !fixedOnce)
        {
            SoundManager.Instance.QueueMusic("Bridge");
            fixedOnce = true;
        }

        if (!hasBattery || !hasParts || !hasWire) return;
        IsFixed = true;
        if(runningSprite != null) GetComponent<SpriteRenderer>().sprite = runningSprite;
        //It's loud
        if(startNoise > 0f) Noise.Make(transform.position, startNoise);
        Debug.Log("Generator running, exit open", this);
        SoundManager.Instance.QueueMusic("End");
        SFXManager.Play("gen");
        SpeechBubble.Say(body.transform, Lore.GeneratorStarts, 3f);
        OnFixed?.Invoke();
    }

    //Debug routes: running without the parts
    public void DebugFix()
    {
        if(IsFixed) return;
        hasBattery = hasParts = hasWire = true;
        IsFixed = true;
        if(runningSprite != null) GetComponent<SpriteRenderer>().sprite = runningSprite;
        OnFixed?.Invoke();
    }

    string Missing()
    {
        string s = "";
        if(!hasBattery) s += "charged battery, ";
        if(!hasParts) s += "spare parts, ";
        if(!hasWire) s += "wire, ";
        return s.TrimEnd(',', ' ');
    }
}
