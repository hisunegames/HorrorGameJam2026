using UnityEngine;

//The scientist's lines that fire on their own (all text in Lore.cs): chased by the daughter or a small monster,
//swapping bodies, and seeing the half body (No Legs) for the first time. Put it on the GameManager
public class LoreTriggers : MonoBehaviour
{
    public BodyManager bodyManager;
    //Don't repeat the same kind of line more often than this
    public float chaseLineCooldown = 20f;
    //How close to the No Legs body before "I remember having his torso..."
    public float halfBodyRange = 2.5f;

    private float daughterLineTimer;
    private float smallLineTimer;
    private bool halfBodySeen;

    void Awake()
    {
        if(bodyManager == null) bodyManager = GameManager.Get<BodyManager>();
    }

    void OnEnable()
    {
        Enemy.OnChaseStarted += HandleDaughterChase;
        SmallMonster.OnChaseStarted += HandleSmallChase;
        if(bodyManager != null) bodyManager.OnBodySwapped += HandleSwap;
    }

    void OnDisable()
    {
        Enemy.OnChaseStarted -= HandleDaughterChase;
        SmallMonster.OnChaseStarted -= HandleSmallChase;
        if(bodyManager != null) bodyManager.OnBodySwapped -= HandleSwap;
    }

    void Update()
    {
        daughterLineTimer -= Time.deltaTime;
        smallLineTimer -= Time.deltaTime;
        if(!halfBodySeen) CheckHalfBody();
    }

    void Say(string line)
    {
        BodyScript body = bodyManager != null ? bodyManager.currentBody : null;
        //The doctor's own reactions don't fit once you're her
        if(body != null && !DaughterTransfer.Active) SpeechBubble.Say(body.transform, line, 2.5f);
    }

    static string Pick(string[] lines)
    {
        return lines[Random.Range(0, lines.Length)];
    }

    void HandleDaughterChase(Enemy enemy)
    {
        if(daughterLineTimer > 0f) return;
        daughterLineTimer = chaseLineCooldown;
        Say(Pick(Lore.DaughterChase));
    }

    void HandleSmallChase(SmallMonster small)
    {
        if(smallLineTimer > 0f) return;
        smallLineTimer = chaseLineCooldown;
        Say(Lore.SmallMonsterChase);
    }

    void HandleSwap(BodyScript oldBody, BodyScript newBody, int swapCount)
    {
        if(newBody != null) SpeechBubble.Say(newBody.transform, Pick(Lore.BodySwap), 2.5f);
    }

    //First time near a No Legs body (still unused)
    void CheckHalfBody()
    {
        BodyScript body = bodyManager != null ? bodyManager.currentBody : null;
        if(body == null) return;
        foreach(var other in FindObjectsByType<BodyScript>())
        {
            if(other == body || other.isVacant || other.customBodySO == null || !other.customBodySO.isSlow) continue;
            if(Vector2.Distance(other.transform.position, body.transform.position) > halfBodyRange) continue;
            halfBodySeen = true;
            Say(Lore.HalfBody);
            return;
        }
    }
}
