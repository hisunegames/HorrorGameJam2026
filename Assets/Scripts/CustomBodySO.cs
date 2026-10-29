using UnityEngine;

[CreateAssetMenu(fileName = "CustomBodySO", menuName = "Scriptable Objects/CustomBodySO")]
public class CustomBodySO : ScriptableObject
{
    [Header("Abilites")]
    [Tooltip("Feels the monster's footsteps: ripples show where it is, even through fog or off screen (VibrationSense on the GameManager)")]
    public bool sensesVibrations = false;
    [Tooltip("Hold the play dead key to lie flat (collapse animation): the monster ignores you unless it walks into you. Drains stamina")]
    public bool canPlayDead = false;
    public bool canUnlockDoors = false;
    public bool canSwim = false;
    public bool canCrawl = false;
    [Tooltip("Rubber boots (Major): safe from the shock when taking the wire while standing in water")]
    public bool rubberBoots = false;

    [Header("Story")]
    [Tooltip("Researcher F: her fingerprint opens the charger (possess her, drag her body there, or cut off her finger)")]
    public bool hasFingerprint = false;
    [Tooltip("Cut off a corpse's finger with a blade and you get this item")]
    public ItemData fingerItem;
    [Tooltip("The monster hunts and eats this body even while nobody is in it (bait it onto her)")]
    public bool isPrey = false;
    [Tooltip("Sprite once dead / eaten. Empty = tinted dark red")]
    public Sprite corpseSprite;
    [Tooltip("Leaves a blood trail when dragged even while alive (left behind after a swap). Corpses always bleed")]
    public bool bleedsWhenDragged = false;

    [Header("Cons")]
    [Tooltip("No eyes: fog closes in around you (BodyFog on the GameManager)")]
    public bool isNearsighted = false;
    [Tooltip("How far you can see with no eyes, in world units")]
    public float fogRadius = 2f;
    [Tooltip("No legs: much slower and can't sprint")]
    public bool isSlow = false;
    [Range(0.1f, 1f)] public float slowSpeedMultiplier = 0.4f;
    [Tooltip("Heavy boots: walking makes noise the monster hears. 0 = silent, 1 = as loud as a normal sprint")]
    public float walkNoise = 0f;
    [Tooltip("Sprinting noise is multiplied by this. Heavy boots = 1.5")]
    public float sprintNoiseMultiplier = 1f;
    [Tooltip("Optional step sounds, one plays with each noise")]
    public AudioClip[] footstepSounds;

    [Header("Stats")]
    public float moveSpeed = 5f;
    [Tooltip("Size of the stamina pool (sprinting, playing dead) compared to normal")]
    public float staminaMultiplier = 1f;

    public float SpeedMultiplier => isSlow ? slowSpeedMultiplier : 1f;
}
