using UnityEngine;

//The electric box with the fingerprint lock. Scan Researcher F's print and it lets go of the empty battery:
//be in her body, carry her finger, or drag her body (alive or dead) next to it
public class FingerprintScanner : Interactable
{
    public ItemData emptyBattery;
    public ItemData finger;
    //Her body this close works too
    public float bodyRange = 1.5f;
    [TextArea] public string acceptedLine = "*click* The panel slides open. A battery, drained.";
    //Optional: opened box
    public Sprite openSprite;

    public bool used;

    void Reset()
    {
        range = 1.2f;
        emptyBattery = ItemData.Find("EmptyBattery");
        finger = ItemData.Find("Finger");
    }

    public override string Prompt(BodyScript body)
    {
        if(used) return null;
        return HasFingerprint(body) ? "Scan fingerprint" : "Fingerprint scanner (locked)";
    }

    public override bool CanInteract(BodyScript body)
    {
        return !used && HasFingerprint(body);
    }

    public bool HasFingerprint(BodyScript body)
    {
        if(body.HasFingerprint) return true;
        PlayerInventory inv = Inventory;
        if(inv != null && inv.Has(finger)) return true;
        foreach(var other in FindObjectsByType<BodyScript>())
        {
            if(other.HasFingerprint && Vector2.Distance(other.transform.position, Point) <= bodyRange) return true;
        }
        return false;
    }

    protected override string DefaultThought(BodyScript body)
    {
        return "A fingerprint lock. My hands won't open it... but hers would.";
    }

    public override void Interact(BodyScript body)
    {
        used = true;
        if(openSprite != null) GetComponent<SpriteRenderer>().sprite = openSprite;
        SpeechBubble.Say(transform, acceptedLine, 3f);
        Inventory.Give(emptyBattery, transform.position);
    }
}
