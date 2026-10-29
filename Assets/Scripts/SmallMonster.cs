using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

//Small fast monster. Wanders near where it starts, chases the player when it sees them, and kills by pouncing:
//it stops, shakes and shows a red strip for where it will land, then leaps along it. Dodge out of the strip.
//Dies to any weapon ([Q] when close) or a trap. Tools > Jam > Create Small Monster makes one
[RequireComponent(typeof(NavMeshAgent))]
public class SmallMonster : MonoBehaviour
{
    public static readonly List<SmallMonster> All = new List<SmallMonster>();
    //Spotted the player (LoreTriggers: "I have to end your purpose.")
    public static event System.Action<SmallMonster> OnChaseStarted;

    public float wanderSpeed = 1.5f;
    public float chaseSpeed = 2.6f;
    public float wanderRadius = 3f;
    public float sightRange = 3.5f;
    //Starts the wind-up when the player is this close
    public float pounceTrigger = 2f;
    //Length of the leap (the red strip)
    public float pounceDistance = 2.75f;
    //This leap's actual length (shorter than Pounce Distance if a wall is in the way)
    private float leapLength;
    public float pounceSpeed = 11f;
    public float windUp = 0.6f;
    public float recover = 1f;
    //Caught if the player is this close to it during the leap
    public float hitRadius = 0.4f;
    public Color telegraphColor = new Color(1f, 0f, 0f, 0.35f);
    public float telegraphWidth = 0.5f;
    public Color deadColor = new Color(0.35f, 0.1f, 0.1f);
    [Tooltip("Log when a leap gets cut short by the edge of the NavMesh")]
    public bool debugLogs = true;

    public bool IsDead => state == State.Dead;

    private enum State { Wander, Chase, WindUp, Pounce, Recover, Dead }
    private State state = State.Wander;
    private NavMeshAgent agent;
    private SpriteRenderer sr;
    private Vector3 spriteRest;
    private Transform telegraph;
    private SpriteRenderer telegraphSr;
    private BodyManager bodyManager;
    private NavMeshPath path;
    private Vector3 home;
    private Vector2 pounceDir;
    //Where the leap (and the red strip) starts, on screen and on the NavMesh
    private Vector3 leapStart;
    private Vector3 leapNavStart;
    private float travelled;
    private float timer;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false;
        agent.updateUpAxis = false;
        agent.speed = wanderSpeed;
        sr = GetComponentInChildren<SpriteRenderer>();
        if(sr != null) spriteRest = sr.transform.localPosition;
        bodyManager = GameManager.Get<BodyManager>();
        path = new NavMeshPath();
        home = transform.position;
        BuildTelegraph();
    }

    void Update()
    {
        if(state == State.Dead || agent == null || !agent.isOnNavMesh) return;
        BodyScript player = Visible();
        switch(state)
        {
            case State.Wander:
                agent.speed = wanderSpeed;
                if(player != null)
                {
                    state = State.Chase;
                    OnChaseStarted?.Invoke(this);
                    break;
                }
                timer -= Time.deltaTime;
                if(timer <= 0f && !agent.pathPending && agent.remainingDistance < 0.2f) PickWanderPoint();
                break;

            case State.Chase:
                agent.speed = chaseSpeed;
                if(player == null)
                {
                    state = State.Wander;
                    timer = 1f;
                    break;
                }
                agent.SetDestination(player.transform.position);
                if(Vector2.Distance(player.transform.position, transform.position) <= pounceTrigger) StartWindUp(player);
                break;

            case State.WindUp:
                timer -= Time.deltaTime;
                //Shaking, telegraph showing
                if(sr != null && sr.transform != transform) sr.transform.localPosition = spriteRest + (Vector3)(Random.insideUnitCircle * 0.05f);
                if(timer <= 0f)
                {
                    if(sr != null && sr.transform != transform) sr.transform.localPosition = spriteRest;
                    state = State.Pounce;
                    travelled = 0f;
                    //The leap moves the transform itself, along the strip
                    agent.updatePosition = false;
                }
                break;

            case State.Pounce:
                //Exactly along the red strip: the agent lets go of the transform for the leap, then picks up where it lands
                Vector3 before = transform.position;
                travelled = Mathf.Min(leapLength, travelled + pounceSpeed * Time.deltaTime);
                transform.position = leapStart + (Vector3)(pounceDir * travelled);
                if(CaughtPlayer(before, transform.position)) return;
                if(travelled >= leapLength)
                {
                    EndLeap();
                    state = State.Recover;
                    timer = recover;
                }
                break;

            case State.Recover:
                timer -= Time.deltaTime;
                if(timer <= 0f)
                {
                    agent.isStopped = false;
                    state = player != null ? State.Chase : State.Wander;
                }
                break;
        }
    }

    BodyScript Visible()
    {
        BodyScript body = bodyManager != null ? bodyManager.currentBody : null;
        //In her body you're one of them: it leaves you alone
        if(body == null || body.isHiding || body.isPlayingDead || DaughterTransfer.Active) return null;
        if(Vector2.Distance(body.transform.position, transform.position) > sightRange) return null;
        if(!NavMesh.SamplePosition(body.transform.position, out NavMeshHit hit, 1f, NavMesh.AllAreas)) return null;
        //From the agent's own NavMesh position (the transform is Base Offset away from it)
        agent.CalculatePath(hit.position, path);
        return path.status == NavMeshPathStatus.PathComplete ? body : null;
    }

    void PickWanderPoint()
    {
        timer = Random.Range(1f, 3f);
        Vector3 p = home + (Vector3)(Random.insideUnitCircle * wanderRadius);
        if(NavMesh.SamplePosition(p, out NavMeshHit hit, 1f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
    }

    void StartWindUp(BodyScript player)
    {
        state = State.WindUp;
        timer = windUp;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        //Locked in now: the leap goes where the player was when it started shaking
        pounceDir = ((Vector2)(player.transform.position - transform.position)).normalized;
        if(pounceDir.sqrMagnitude < 0.01f) pounceDir = Vector2.down;
        //How far it can really go that way (stops at the edge of the walkable floor): the strip shows exactly that
        Vector3 start = agent.nextPosition;
        leapNavStart = start;
        leapStart = transform.position;
        Vector3 end = start + (Vector3)(pounceDir * pounceDistance);
        leapLength = NavMesh.Raycast(start, end, out NavMeshHit edge, NavMesh.AllAreas) ? Vector2.Distance(start, edge.position) : pounceDistance;
        if(debugLogs && leapLength < pounceDistance * 0.6f) Debug.Log($"SmallMonster: leap cut to {leapLength:0.00} of {pounceDistance} by the NavMesh edge at {edge.position}", this);
        telegraph.position = transform.position;
        telegraph.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(pounceDir.y, pounceDir.x) * Mathf.Rad2Deg);
        telegraph.localScale = new Vector3(Mathf.Max(0.1f, leapLength), telegraphWidth, 1f);
        telegraph.gameObject.SetActive(true);
    }

    //Hands the monster back to its agent where the leap ended
    void EndLeap()
    {
        telegraph.gameObject.SetActive(false);
        Vector3 landing = leapNavStart + (Vector3)(pounceDir * travelled);
        if(NavMesh.SamplePosition(landing, out NavMeshHit hit, 1f, NavMesh.AllAreas)) landing = hit.position;
        agent.Warp(landing);
        agent.updatePosition = true;
    }

    //Hit if the player is on the bit of strip it covered this frame (only on the strip: stepping off it dodges)
    bool CaughtPlayer(Vector3 from, Vector3 to)
    {
        BodyScript body = bodyManager != null ? bodyManager.currentBody : null;
        //Hidden, playing dead, or in her body: the leap goes over you
        if(body == null || body.isHiding || body.isPlayingDead || DaughterTransfer.Active) return false;
        Vector2 a = from, b = to, p = body.transform.position;
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        if(Vector2.Distance(p, a + ab * t) > hitRadius) return false;
        EndLeap();
        Enemy.KillPlayer("Something small and fast got your throat.");
        state = State.Recover;
        timer = 999f;
        return true;
    }

    //Weapon or trap
    public void Kill()
    {
        if(state == State.Dead) return;
        state = State.Dead;
        if(agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }
        if(telegraph != null) telegraph.gameObject.SetActive(false);
        if(sr != null)
        {
            sr.color = deadColor;
            sr.transform.localPosition = spriteRest;
            sr.flipY = true;
        }
        Debug.Log("Small monster killed", this);
    }

    //Red strip from the monster along the leap. Unrotated it points right, 1 unit long
    void BuildTelegraph()
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        GameObject obj = new GameObject("PounceTelegraph");
        telegraph = obj.transform;
        telegraphSr = obj.AddComponent<SpriteRenderer>();
        telegraphSr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
        telegraphSr.color = telegraphColor;
        if(SortingLayer.NameToID("Prop") != 0) telegraphSr.sortingLayerName = "Prop";
        telegraphSr.sortingOrder = -5;
        obj.SetActive(false);
    }
}
