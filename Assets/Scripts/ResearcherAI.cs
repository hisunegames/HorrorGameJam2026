using UnityEngine;
using UnityEngine.AI;

//Researcher F while nobody's in her: wanders her room, scared. Come at her with a weapon selected (or the monster
//gets close) and she runs: away from you, and out through a door if one's clear. A chair parked in a doorway
//blocks it, so you can trap her in a room and corner her. Says a line when you come near (calm or armed).
//Stops once she's possessed, left behind or dead
[RequireComponent(typeof(BodyScript))]
public class ResearcherAI : MonoBehaviour
{
    public float wanderSpeed = 1.1f;
    //Between your walk (3.5) and sprint (5.6): you have to corner her
    public float fleeSpeed = 3.7f;
    //Runs when you're this close with a weapon out
    public float fleeRange = 5f;
    //...or the monster is this close
    public float monsterFleeRange = 5f;
    //Goes for a door only if she's further from you than this (otherwise just runs across the room)
    public float doorDash = 1.5f;
    //A chair this close to a doorway blocks it
    public float chairBlockRadius = 1f;
    public Vector2 wanderPause = new Vector2(1.5f, 4f);

    [Header("Talking")]
    public float talkRange = 2.5f;
    public float talkCooldown = 7f;
    [TextArea] public string[] calmLines =
    {
        "Who... who's in there?",
        "Don't come any closer. Please.",
        "You're not him. You can't be him.",
    };
    [TextArea] public string[] armedLines =
    {
        "No - no, put that down!",
        "Stay away from me!",
        "Please, I have a family!",
    };
    [TextArea] public string[] corneredLines =
    {
        "Please... please don't...",
        "I'll do anything. Don't.",
    };
    [TextArea] public string[] monsterLines =
    {
        "It's here - it's HERE!",
    };

    private BodyScript body;
    private BodyManager bodyManager;
    private NavMeshPath path;
    private Vector3[] corners = new Vector3[0];
    private int cornerIndex;
    private RoomDoor targetDoor;
    private float repathTimer;
    private float pauseTimer;
    private float talkTimer;
    private bool wasArmedNear;
    private bool active;

    public bool IsFleeing { get; private set; }
    public bool IsCornered { get; private set; }

    void Awake()
    {
        body = GetComponent<BodyScript>();
        path = new NavMeshPath();
    }

    void Start()
    {
        bodyManager = GameManager.Get<BodyManager>();
    }

    bool CanAct => !body.isCurrentBody && !body.isVacant && !body.isConsumed;

    void Update()
    {
        if(!CanAct)
        {
            if(active) Stop();
            return;
        }
        if(Time.timeScale == 0f) return;
        if(!active)
        {
            active = true;
            body.rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
        talkTimer -= Time.deltaTime;

        Vector2 me = transform.position;
        BodyScript player = bodyManager != null ? bodyManager.currentBody : null;
        bool playerNear = player != null && !player.isHiding && Vector2.Distance(player.transform.position, me) <= talkRange;
        bool armed = player != null && !player.isHiding && Armed() && Vector2.Distance(player.transform.position, me) <= fleeRange;
        Enemy monster = NearestMonster(me);

        //Who to run from: you with a weapon, or the monster
        Vector2? threat = null;
        if(monster != null) threat = monster.transform.position;
        else if(armed) threat = player.transform.position;

        Talk(playerNear, armed, monster != null);
        bool wasFleeing = IsFleeing;
        IsFleeing = threat != null;

        repathTimer -= Time.deltaTime;
        if(IsFleeing && (!wasFleeing || repathTimer <= 0f)) PlanEscape(me, threat.Value);
        else if(!IsFleeing && wasFleeing) ClearPath();
        if(!IsFleeing) IsCornered = false;
    }

    void FixedUpdate()
    {
        if(!active || !CanAct) return;
        if(!IsFleeing && cornerIndex >= corners.Length)
        {
            body.rb.linearVelocity = Vector2.zero;
            pauseTimer -= Time.fixedDeltaTime;
            if(pauseTimer <= 0f) Wander();
            return;
        }
        if(cornerIndex >= corners.Length)
        {
            body.rb.linearVelocity = Vector2.zero;
            return;
        }
        Vector2 to = (Vector2)corners[cornerIndex] - body.rb.position;
        if(to.magnitude < 0.12f)
        {
            cornerIndex++;
            if(cornerIndex >= corners.Length) Arrived();
            return;
        }
        body.rb.linearVelocity = to.normalized * (IsFleeing ? fleeSpeed : wanderSpeed);
    }

    bool Armed()
    {
        PlayerInventory inv = GameManager.Get<PlayerInventory>();
        ItemData held = inv != null ? inv.SelectedItem : null;
        return held != null && held.kind == ItemKind.Weapon;
    }

    Enemy NearestMonster(Vector2 me)
    {
        foreach(var enemy in FindObjectsByType<Enemy>())
        {
            if(!enemy.IsStunned && Vector2.Distance(enemy.transform.position, me) <= monsterFleeRange) return enemy;
        }
        return null;
    }

    void Talk(bool playerNear, bool armed, bool monster)
    {
        //Pulling a weapon out near her gets a reaction straight away
        bool armedNow = armed && playerNear;
        if(armedNow && !wasArmedNear) talkTimer = 0f;
        wasArmedNear = armedNow;
        if(talkTimer > 0f) return;

        string[] lines = null;
        if(monster) lines = monsterLines;
        else if(IsCornered && playerNear) lines = corneredLines;
        else if(armedNow) lines = armedLines;
        else if(playerNear) lines = calmLines;
        if(lines == null || lines.Length == 0) return;
        SpeechBubble.Say(transform, lines[Random.Range(0, lines.Length)], 3f);
        talkTimer = talkCooldown;
    }

    //Out through a clear door if she can get there before you, otherwise the spot in the room furthest from you
    void PlanEscape(Vector2 me, Vector2 threat)
    {
        repathTimer = 0.5f;
        Transform room = RoomMap.RoomAt(me);
        RoomDoor bestDoor = null;
        float bestDoorScore = float.MinValue;
        foreach(var door in RoomDoor.All)
        {
            if(door.isShortcut || door.linkedDoor == null || RoomMap.RoomOf(door) != room || Blocked(door)) continue;
            Vector2 exit = door.ExitPoint;
            float mine = Vector2.Distance(me, exit);
            float theirs = Vector2.Distance(threat, exit);
            //You'd cut her off
            if(theirs < mine + doorDash) continue;
            float score = theirs - mine;
            if(score > bestDoorScore && SetPath(exit))
            {
                bestDoorScore = score;
                bestDoor = door;
            }
        }
        if(bestDoor != null)
        {
            SetPath(bestDoor.ExitPoint);
            targetDoor = bestDoor;
            IsCornered = false;
            return;
        }

        //No way out: furthest reachable spot in the room
        targetDoor = null;
        Vector2 best = me;
        float bestDist = Vector2.Distance(me, threat);
        if(room != null && RoomMap.TryGetBounds(room, out Bounds b))
        {
            for(int i = 0; i < 12; i++)
            {
                Vector2 p = new Vector2(Random.Range(b.min.x + 1.5f, b.max.x - 1.5f), Random.Range(b.min.y + 1.5f, b.max.y - 1.5f));
                if(!NavMesh.SamplePosition(p, out NavMeshHit hit, 1f, NavMesh.AllAreas)) continue;
                float d = Vector2.Distance(hit.position, threat);
                if(d > bestDist + 0.5f && Reachable(hit.position))
                {
                    bestDist = d;
                    best = hit.position;
                }
            }
        }
        IsCornered = best == me || bestDist < 2f;
        if(best != me) SetPath(best);
        else ClearPath();
    }

    bool Blocked(RoomDoor door)
    {
        foreach(var prop in DraggableProp.All)
        {
            if(Vector2.Distance(prop.transform.position, door.ExitPoint) <= chairBlockRadius) return true;
            if(Vector2.Distance(prop.transform.position, door.transform.position) <= chairBlockRadius) return true;
        }
        return false;
    }

    void Arrived()
    {
        if(targetDoor != null && targetDoor.linkedDoor != null && !Blocked(targetDoor))
        {
            //Out the door into the next room
            Vector3 to = targetDoor.linkedDoor.ExitPoint;
            to.z = transform.position.z;
            body.rb.position = to;
            transform.position = to;
            targetDoor = null;
            repathTimer = 0f;
            return;
        }
        targetDoor = null;
        pauseTimer = Random.Range(wanderPause.x, wanderPause.y);
    }

    void Wander()
    {
        Vector2 me = transform.position;
        Transform room = RoomMap.RoomAt(me);
        if(room == null || !RoomMap.TryGetBounds(room, out Bounds b))
        {
            pauseTimer = wanderPause.y;
            return;
        }
        for(int i = 0; i < 8; i++)
        {
            Vector2 p = me + Random.insideUnitCircle * 3f;
            p.x = Mathf.Clamp(p.x, b.min.x + 1.5f, b.max.x - 1.5f);
            p.y = Mathf.Clamp(p.y, b.min.y + 1.5f, b.max.y - 1.5f);
            if(NavMesh.SamplePosition(p, out NavMeshHit hit, 1f, NavMesh.AllAreas) && SetPath(hit.position)) return;
        }
        pauseTimer = wanderPause.x;
    }

    bool Reachable(Vector3 point)
    {
        return NavMesh.CalculatePath(transform.position, point, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
    }

    bool SetPath(Vector3 point)
    {
        if(NavMesh.SamplePosition(point, out NavMeshHit hit, 1f, NavMesh.AllAreas)) point = hit.position;
        if(!Reachable(point)) return false;
        corners = path.corners;
        cornerIndex = corners.Length > 1 ? 1 : corners.Length;
        return true;
    }

    void ClearPath()
    {
        corners = new Vector3[0];
        cornerIndex = 0;
        targetDoor = null;
        pauseTimer = Random.Range(wanderPause.x, wanderPause.y);
    }

    void Stop()
    {
        active = false;
        IsFleeing = false;
        ClearPath();
        if(!body.isCurrentBody)
        {
            body.rb.linearVelocity = Vector2.zero;
            body.rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }
    }
}
