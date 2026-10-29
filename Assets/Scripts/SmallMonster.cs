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
                }
                break;

            case State.Pounce:
                //Straight along the telegraphed line (agent.Move drifted off it with the 2D NavMesh), stopping at walls
                //From the agent's NavMesh position, not the transform (that's Base Offset away and would add up every warp)
                Vector3 before = agent.nextPosition;
                float want = Mathf.Min(pounceSpeed * Time.deltaTime, leapLength - travelled);
                Vector3 next = before + (Vector3)(pounceDir * want);
                bool blocked = NavMesh.Raycast(before, next, out NavMeshHit edge, NavMesh.AllAreas);
                if(blocked) next = edge.position;
                agent.Warp(next);
                float step = Vector2.Distance(before, next);
                travelled += step;
                if(CaughtPlayer()) return;
                //Done, or hit a wall
                if(travelled >= leapLength - 0.01f || blocked)
                {
                    telegraph.gameObject.SetActive(false);
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
        if(body == null || body.isHiding || body.isPlayingDead) return null;
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
        Vector3 end = start + (Vector3)(pounceDir * pounceDistance);
        leapLength = NavMesh.Raycast(start, end, out NavMeshHit edge, NavMesh.AllAreas) ? Vector2.Distance(start, edge.position) : pounceDistance;
        telegraph.position = transform.position;
        telegraph.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(pounceDir.y, pounceDir.x) * Mathf.Rad2Deg);
        telegraph.localScale = new Vector3(Mathf.Max(0.1f, leapLength), telegraphWidth, 1f);
        telegraph.gameObject.SetActive(true);
    }

    bool CaughtPlayer()
    {
        BodyScript body = bodyManager != null ? bodyManager.currentBody : null;
        //Hidden, or playing dead: the leap goes over you
        if(body == null || body.isHiding || body.isPlayingDead) return false;
        if(Vector2.Distance(body.transform.position, transform.position) > hitRadius) return false;
        Enemy.KillPlayer("Something small and fast got your throat.");
        state = State.Recover;
        timer = 999f;
        telegraph.gameObject.SetActive(false);
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
