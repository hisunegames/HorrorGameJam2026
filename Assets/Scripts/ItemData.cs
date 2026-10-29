using UnityEngine;

public enum ItemKind { KeyItem, Trap, Weapon, Adrenaline }

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
    //Gun: hits the first monster in a straight line up to Range. Melee: anything in Range in the way you face
    public bool ranged = false;
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
