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

    [Header("Systems (found on this object if empty)")]
    public BodyManager bodyManager;
    public PlayerSprint sprint;
    public PlayerHUD hud;
    public BlackScreenUI blackScreen;
    public GameOverScreen gameOver;
    public SwapMemoryScreen swapScreen;
    public DialogueController dialogue;

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

        if(bodyManager == null) bodyManager = GetComponentInChildren<BodyManager>();
        if(sprint == null) sprint = GetComponentInChildren<PlayerSprint>();
        if(hud == null) hud = GetComponentInChildren<PlayerHUD>();
        if(blackScreen == null) blackScreen = GetComponentInChildren<BlackScreenUI>();
        if(gameOver == null) gameOver = GetComponentInChildren<GameOverScreen>();
        if(swapScreen == null) swapScreen = GetComponentInChildren<SwapMemoryScreen>();
        if(dialogue == null) dialogue = GetComponentInChildren<DialogueController>();
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
