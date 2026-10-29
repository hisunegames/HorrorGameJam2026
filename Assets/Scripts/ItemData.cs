using UnityEngine;

public enum ItemKind { KeyItem, Trap, Weapon, Adrenaline, Lure }

//One kind of item. Tools > Jam > Create Item Assets makes all the game's items in Assets/Items
[CreateAssetMenu(fileName = "ItemData", menuName = "Scriptable Objects/Item Data")]
public class ItemData : ScriptableObject
{
    public string itemName = "Item";
    public ItemKind kind = ItemKind.KeyItem;
    //Lying on the floor
    public Sprite worldSprite;

    [Header("Trap")]
    [Tooltip("Old flag, same as Kind = Trap")]
    public bool isTrap = false;
    //Shown once it has gone off (can't be picked up again)
    public Sprite snappedSprite;
    //Monster this close to it sets it off
    public float triggerRadius = 0.6f;
    public float stunDuration = 5f;
    //Noise when it snaps (0 = silent), same scale as Noise.Make
    public float snapNoise = 0f;

    [Header("Weapon")]
    //Uses before it's gone (gun = 1 ammo)
    public int charges = 2;
    //Tiles in front of you it reaches (knife/saw 1, chainsaw 2)
    public float range = 1f;
    //Gun: fires a projectile (Projectile) that flies up to Range. Melee: anything in Range in the way you face
    public bool ranged = false;
    //Ranged: shots per use (shotgun pellets), their spread in degrees, speed and colour (trail + sparks)
    public int pellets = 1;
    public float spread = 0f;
    public float projectileSpeed = 14f;
    public Color projectileColor = new Color(0.4f, 0.9f, 1f);
    //Can kill Researcher F / cut off her finger
    public bool canCut = true;
    public float weaponStun = 5f;
    //Noise when used (chainsaw, gun)
    public float useNoise = 0f;

    [Header("Adrenaline")]
    public float boostTime = 8f;
    //Sprint speed while boosted, times normal sprint
    public float boostSpeed = 1.3f;
    public float crashTime = 6f;
    //Walk speed during the crash
    public float crashSpeed = 0.6f;

    [Header("Lure (radio)")]
    //Seconds after it's set down before it starts (and the monster comes)
    public float lureDelay = 1.5f;
    //How long it keeps playing
    public float lureDuration = 12f;
    //Noise it makes every second while playing (shows on the noise meter, other monsters hear it)
    public float lureLoudness = 1.5f;
    //Optional static / music, looped while it plays
    public AudioClip lureSound;

    public bool IsTrap => isTrap || kind == ItemKind.Trap;
    public int StartCharges => kind == ItemKind.Weapon ? Mathf.Max(1, charges) : 1;

    //Editor only: components fill in their item slots from Assets/Items when added (Reset)
    public static ItemData Find(string assetName)
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Items/" + assetName + ".asset");
#else
        return null;
#endif
    }
}
