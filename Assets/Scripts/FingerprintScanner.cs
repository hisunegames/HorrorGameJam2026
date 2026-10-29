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
    //How far above the box its line shows
    public float lineHeight = 0.5f;
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
        return Lore.ScannerLocked[Random.Range(0, Lore.ScannerLocked.Length)];
    }

    public override void Interact(BodyScript body)
    {
        used = true;
        if(openSprite != null) GetComponent<SpriteRenderer>().sprite = openSprite;
        SpeechBubble.Say(transform, acceptedLine, 3f, lineHeight);
        Inventory.Give(emptyBattery, transform.position);
    }
}
