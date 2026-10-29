using UnityEngine;

//One object that holds the game-wide scripts (body swapping, sprint, HUD, screens, dialogue) and global tuning.
//Tools > Jam > Create Game Manager moves the existing scripts onto it. Other scripts reach them through GameManager.Instance
[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Player")]
    //Scales every body's moveSpeed. Lower = slower player
    public float playerSpeedMultiplier = 0.7f;
    [Tooltip("The player walks over bodies and chairs (Body layer) instead of bumping into them, so you can stand on one in a puddle. They still hit walls when dragged")]
    public bool walkOverBodies = true;

    [Header("Systems (found on this object if empty)")]
    public BodyManager bodyManager;
    public PlayerSprint sprint;
    public PlayerHUD hud;
    public BlackScreenUI blackScreen;
    public GameOverScreen gameOver;
    public SwapMemoryScreen swapScreen;
    public DialogueController dialogue;
    public BodyFog fog;
    public VibrationSense vibrationSense;
    public PlayDead playDead;
    public PlayerInventory inventory;
    public BodyDrag bodyDrag;

    //Speed multiplier for bodies, 1 when there's no GameManager in the scene
    public static float PlayerSpeedMultiplier => Instance != null ? Instance.playerSpeedMultiplier : 1f;

    void Awake()
    {
        if(Instance != null && Instance != this)
        {
            Debug.LogWarning("More than one GameManager in the scene, ignoring this one", this);
            return;
        }
        Instance = this;
        int bodyLayer = LayerMask.NameToLayer("Body");
        int playerLayer = LayerMask.NameToLayer("Player");
        if(bodyLayer >= 0 && playerLayer >= 0) Physics2D.IgnoreLayerCollision(bodyLayer, playerLayer, walkOverBodies);

        //Systems that need no settings: added here if the scene doesn't have them yet (so a newer system works even
        //if Tools > Jam > Create Game Manager wasn't run again). Ones with settings to fill in are still added by the tool
        Ensure<Interactor>();
        Ensure<ModalUI>();
        Ensure<PlayerInventory>();
        Ensure<BodyDrag>();
        Ensure<PlayDead>();
        Ensure<BodyFog>();
        Ensure<VibrationSense>();
        Ensure<NoiseIndicator>();
        Ensure<LoreTriggers>();
        Ensure<DaughterTransfer>();
        Ensure<DebugRoutes>();

        if(bodyManager == null) bodyManager = GetComponentInChildren<BodyManager>();
        if(sprint == null) sprint = GetComponentInChildren<PlayerSprint>();
        if(hud == null) hud = GetComponentInChildren<PlayerHUD>();
        if(blackScreen == null) blackScreen = GetComponentInChildren<BlackScreenUI>();
        if(gameOver == null) gameOver = GetComponentInChildren<GameOverScreen>();
        if(swapScreen == null) swapScreen = GetComponentInChildren<SwapMemoryScreen>();
        if(dialogue == null) dialogue = GetComponentInChildren<DialogueController>();
        if(fog == null) fog = GetComponentInChildren<BodyFog>();
        if(vibrationSense == null) vibrationSense = GetComponentInChildren<VibrationSense>();
        if(playDead == null) playDead = GetComponentInChildren<PlayDead>();
        if(inventory == null) inventory = GetComponentInChildren<PlayerInventory>();
        if(bodyDrag == null) bodyDrag = GetComponentInChildren<BodyDrag>();
    }

    void Ensure<T>() where T : Component
    {
        if(GetComponentInChildren<T>() != null || FindAnyObjectByType<T>() != null) return;
        gameObject.AddComponent<T>();
        Debug.Log($"GameManager: added missing {typeof(T).Name} (run Tools > Jam > Create Game Manager to keep it in the scene)", this);
    }

    void OnDestroy()
    {
        if(Instance == this) Instance = null;
    }

    //Used by other scripts: the GameManager's copy if there is one, otherwise whatever is in the scene
    public static T Get<T>() where T : Component
    {
        if(Instance != null)
        {
            T found = Instance.GetComponentInChildren<T>();
            if(found != null) return found;
        }
        return FindAnyObjectByType<T>();
    }
}
