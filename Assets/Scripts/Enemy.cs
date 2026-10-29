using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour
{
    public static event Action OnPlayerKilled;
    //Started chasing the player (LoreTriggers: "Get away from me!")
    public static event Action<Enemy> OnChaseStarted;
    //Death line when she catches you (doesn't say who she is)
    public const string DaughterKillCause = "Something caught you in the dark.";

    static bool IsPlayer(Transform t)
    {
        BodyScript b = t != null ? t.GetComponent<BodyScript>() : null;
        return b != null && b.isCurrentBody;
    }
    //Why the player died this run (game over screen shows it). Empty = default line
    public static string LastDeathCause { get; private set; } = "";
    public static bool PlayerDied { get; private set; }

    //Anything that kills the player calls this (monster, small monster, shock, nuke). Only the first one counts
    public static void KillPlayer(string cause)
    {
        if(PlayerDied) return;
        PlayerDied = true;
        LastDeathCause = cause;
        Debug.Log("Player died: " + cause);
        OnPlayerKilled?.Invoke();
    }

    //New scene / restart
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        PlayerDied = false;
        LastDeathCause = "";
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) =>
        {
            PlayerDied = false;
            LastDeathCause = "";
        };
    }

    //In a locker, or lying still playing dead: it doesn't grab you even standing on top of you
    static bool IsHidden(Transform t)
    {
        BodyScript body = t.GetComponent<BodyScript>();
        return body != null && (body.isHiding || body.isPlayingDead);
    }
    public enum EnemyState {Wander , SoundTriggered , Chase , Return , Eat , FollowDoor , Stunned}
    public bool IsStunned => currentState == EnemyState.Stunned;
    //Chasing this and can see it right now (lockers won't hide you then)
    public bool IsChasing(Transform t) => currentState == EnemyState.Chase && target == t && targetInSight;
    private float stunTimer;
    public EnemyState currentState = EnemyState.Wander;
    [Header("References")]
    [SerializeField] Transform target;
    NavMeshAgent agent;
    public Collider2D wanderRoomBound;
    public LayerMask playerLayer;
    public LayerMask baitLayer;
    public NavMeshObstacle invisibleWall;

    [Header("Wander Settings")]
    [Tooltip("Room objects (Room1, Room5...) it roams around in. Used instead of Wander Points when set")]
    public Transform[] wanderRooms;
    public Transform[] wanderPoints;
    private int currentPointsindex = 0;
    //Gives up on a stroll / point after walking this long (stuck)
    public float wanderInterval = 5f;
    public float wanderCountTime;
    [Tooltip("Stands around this long (random between x and y) after each stroll")]
    public Vector2 wanderPause = new Vector2(1f, 3f);
    [Tooltip("How far each stroll goes inside a room (random between x and y)")]
    public Vector2 strollDistance = new Vector2(2f, 5f);
    [Tooltip("Time spent in one room before heading to another wander room")]
    public Vector2 roomStayTime = new Vector2(12f, 25f);
    [Tooltip("Wander points: lands somewhere within this of the point instead of right on it")]
    public float wanderJitter = 1.25f;
    [Tooltip("Wander walking speed varies between this fraction and full Move Speed")]
    [Range(0.3f, 1f)] public float wanderMinSpeed = 0.7f;
    private Transform wanderRoom;
    private float roomTimer;
    private bool resting;
    private float pauseTimer;
    private Vector3 spawnPosition;

    [Header("Check Sound")]
    private bool isWaitingAtSound = false;
    public float timeChecking = 5f;
    private float timeCheckCounter;

    [Header("Enemy Stats")]
    public float moveSpeed = 8f;
    [Tooltip("Sight range: sees the player or bait bodies this close")]
    public float trackDistance = 3f;
    [Tooltip("Hearing range: noises this close (times their loudness) get checked out")]
    public float hearingRange = 8f;
    [Tooltip("Optional: layers that block sight, e.g. walls. Empty = sees through everything in range")]
    public LayerMask sightBlockers;
    public float chaseLoseDistance = 12f;

    [Header("Vision")]
    [Tooltip("Width of the vision cone in degrees, out to Track Distance in the way it's moving")]
    public float viewAngle = 110f;
    [Tooltip("Notices anything this close whichever way it's facing")]
    public float senseRadius = 1.2f;
    public bool showVisionCone = true;
    public Color coneColor = new Color(1f, 1f, 0.7f, 0.18f);
    public Color chaseConeColor = new Color(1f, 0.15f, 0.1f, 0.25f);
    //Jam/SpriteSilhouette (found automatically)
    public Shader coneShader;
    private Vector2 facing = Vector2.down;
    private MeshFilter coneMesh;
    private MeshRenderer coneRenderer;
    private float builtRadius;
    private float builtAngle;
    private Color builtColor;
    [Tooltip("Smells blood from dragged bodies this far away and follows the trail")]
    public float bloodSmellRange = 6f;
    private float lastBloodTime = -1f;
    public float enemyKillRange = 0.5f;
    private Vector2 soundTargetPos;

    [Header("Chasing")]
    public float loseTargetDelay = 1.5f;
    public float reChaseCooldown = 2f;
    public float navSampleRadius = 1.5f;
    public float pathCheckInterval = 0.2f;
    [Tooltip("Logs what it's targeting and why it won't chase (turn off once things work)")]
    public bool debugLogs = true;
    private Transform loggedTarget;
    private bool loggedReach = true;
    private float loseTimer;
    private float reChaseTimer;
    private float pathCheckTimer;
    private bool cachedCanReach;
    private bool playerDead;
    private bool targetInSight;
    private NavMeshPath pathCache;

    [Header("Bait")]
    public float eatDuration = 3f;
    private float eatTimer;
    //A BodyScript or a corpse prop (DraggableProp)
    private Component eatingBody;

    [Header("Chase Look")]
    public SpriteRenderer spriteRenderer;
    public Sprite normalSprite;
    public Sprite chaseSprite;
    public AudioSource chaseAudio;
    public float chaseSpeedMultiplier = 1.5f;
    //Placeholder tint while there's no chase sprite yet
    public Color placeholderChaseColor = new Color(1f, 0.3f, 0.3f);
    private Color normalColor = Color.white;
    private EnemyState lastState;

    [Header("Doors")]
    public bool followThroughDoors = true;
    //Pause at the door before coming out the other side, gives the player a head start
    public float doorFollowDelay = 1f;
    public float doorReachDistance = 1f;
    [Tooltip("After coming out of a door it keeps hunting this long even if the player is past Chase Lose Distance")]
    public float doorChaseGrace = 5f;
    [Tooltip("Still follows you through a door this many seconds after losing you (you outran it to the door)")]
    public float doorMemory = 5f;
    private float lastChaseTime = -99f;
    private float doorGraceTimer;
    private Queue<RoomDoor> pendingDoors = new Queue<RoomDoor>();
    private RoomDoor followDoor;
    private float doorWaitTimer;

    [Header("Travelling between rooms")]
    [Tooltip("Wander points (and home) can be in other rooms: it walks there door by door, taking the door that leads closest each time")]
    public float travelDoorPause = 0.5f;
    //Warp through anyway if it can't reach a door in this long
    public float travelStuckTime = 10f;
    private Queue<RoomDoor> travelRoute = new Queue<RoomDoor>();
    private Vector3 travelTarget;
    private float travelDoorTimer;
    private float travelStuckTimer;

    void OnEnable()
    {
        RoomDoor.OnBodyUsedDoor += HandleDoorUsed;
        Noise.OnNoise += HandleNoise;
    }

    void OnDisable()
    {
        RoomDoor.OnBodyUsedDoor -= HandleDoorUsed;
        Noise.OnNoise -= HandleNoise;
        //Switched off (the player took her body): no cone, no crying
        if(coneRenderer != null) coneRenderer.enabled = false;
        if(chaseAudio != null) chaseAudio.Stop();
    }

    //Heard something: go check it out, unless busy or it's somewhere we can't walk to (another room)
    void HandleNoise(Vector2 position, float loudness)
    {
        //Destroyed but still hooked to the static event (scene reload): unhook instead of throwing
        if(this == null)
        {
            Noise.OnNoise -= HandleNoise;
            return;
        }
        if(playerDead || agent == null) return;
        if(currentState == EnemyState.Chase || currentState == EnemyState.Eat || currentState == EnemyState.FollowDoor || currentState == EnemyState.Stunned) return;
        if(Vector2.Distance(transform.position, position) > hearingRange * loudness) return;
        if(!CanReachPoint(position)) return;
        SoundTrigger(position);
    }

    void OnValidate()
    {
        //Keeps the cone shader referenced so it's in builds
        if(coneShader == null) coneShader = Shader.Find("Jam/SpriteSilhouette");
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
        agent.updateRotation = false;
        agent.updateUpAxis = false;
        wanderCountTime = wanderInterval;
        pathCache = new NavMeshPath();
        //Empty slots in the list would throw, just skip them
        wanderPoints = wanderPoints == null ? new Transform[0] : Array.FindAll(wanderPoints, p => p != null);
        wanderRooms = wanderRooms == null ? new Transform[0] : Array.FindAll(wanderRooms, r => r != null);
        spawnPosition = transform.position;
        if(wanderPoints.Length == 0 && wanderRooms.Length == 0)
        {
            Debug.LogWarning("Enemy has no wander rooms or points, it will stand still until it sees or hears something", this);
        }
        if(baitLayer == 0)
        {
            baitLayer = LayerMask.GetMask("Body");
        }
        if(spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if(spriteRenderer != null)
        {
            normalColor = spriteRenderer.color;
            if(normalSprite == null) normalSprite = spriteRenderer.sprite;
        }
        lastState = currentState;
        ApplyStateLook();
    }

    void Update()
    {
        if(playerDead) return;
        if(PlayerDied)
        {
            playerDead = true;
            agent.isStopped = true;
            if(chaseAudio != null) chaseAudio.Stop();
            return;
        }
        Collider2D touching = Physics2D.OverlapCircle(transform.position, enemyKillRange, playerLayer);
        //Can't get at you through a locker door, or while you play dead
        if(touching != null && !IsHidden(touching.transform))
        {
            playerDead = true;
            agent.isStopped = true;
            if(chaseAudio != null) chaseAudio.Stop();
            KillPlayer(DaughterKillCause);
            return;
        }
        //Caught in a trap: stands there (still deadly up close), then heads home
        if(currentState == EnemyState.Stunned)
        {
            stunTimer -= Time.deltaTime;
            if(stunTimer > 0f) return;
            agent.isStopped = false;
            currentState = EnemyState.Return;
            GoBackToStart();
        }
        if(currentState != EnemyState.Eat)
        {
            Component bait = BaitInRange();
            if(bait != null)
            {
                StartEating(bait);
            }
        }
        if(reChaseTimer > 0f) reChaseTimer -= Time.deltaTime;
        pathCheckTimer -= Time.deltaTime;

        if(pathCheckTimer <= 0f)
        {
            pathCheckTimer = pathCheckInterval;
            //Nearest of player or left behind bodies, so an old body can pull the enemy off you
            Transform seen = FindTarget();
            targetInSight = seen != null;
            if(seen != null) target = seen;
            else if(!IsValidTarget(target)) target = null;
            cachedCanReach = CanReachPlayer();
            if(debugLogs) LogTargetChanges();
            //Nothing to chase: a fresh blood trail beats wandering
            if(!targetInSight && (currentState == EnemyState.Wander || currentState == EnemyState.Return || currentState == EnemyState.SoundTriggered))
            {
                FollowBlood();
            }
        }

        if(targetInSight && cachedCanReach && currentState != EnemyState.Chase && currentState != EnemyState.Eat && reChaseTimer <= 0f)
        {
            currentState = EnemyState.Chase;
            if(IsPlayer(target)) OnChaseStarted?.Invoke(this);
            ClearDoorFollow();
            StopTravel();
            isWaitingAtSound = false;
            loseTimer = 0f;
            agent.isStopped = false;
        }
        switch (currentState)
        {
            case EnemyState.Wander:
                Wandering();
                break;
            case EnemyState.SoundTriggered:
                CheckSound();
                break;
            case EnemyState.Chase:
                Chasing();
                break;
            case EnemyState.Return:
                ReturnBack();
                break;
            case EnemyState.Eat:
                Eating();
                break;
            case EnemyState.FollowDoor:
                FollowingDoor();
                break;

        }
        if(currentState == EnemyState.Chase || currentState == EnemyState.FollowDoor) lastChaseTime = Time.time;
        if(currentState != lastState)
        {
            lastState = currentState;
            ApplyStateLook();
        }
    }

    //Goes to the freshest blood it can smell that's newer than the last drop it checked, so it walks the trail
    //towards wherever the body was dragged
    void FollowBlood()
    {
        BloodTrail.Drop drop = BloodTrail.FreshestNear(transform.position, bloodSmellRange, lastBloodTime);
        if(drop == null || !CanReachPoint(drop.position)) return;
        lastBloodTime = drop.time;
        if(debugLogs && currentState != EnemyState.SoundTriggered) Debug.Log("Enemy: found a blood trail", this);
        SoundTrigger(drop.position);
    }

    //Set off a trap (ArmedTrap): stuck in place for a while, forgets what it was doing
    public void Stun(float seconds)
    {
        if(playerDead) return;
        StopTravel();
        ClearDoorFollow();
        eatingBody = null;
        isWaitingAtSound = false;
        currentState = EnemyState.Stunned;
        stunTimer = seconds;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        lastState = currentState;
        ApplyStateLook();
    }

    //Sprite, crying audio and speed all switch the moment the chase starts
    void ApplyStateLook()
    {
        bool chasing = currentState == EnemyState.Chase || currentState == EnemyState.FollowDoor;
        agent.speed = chasing ? moveSpeed * chaseSpeedMultiplier : moveSpeed;
        if(spriteRenderer != null)
        {
            Sprite sprite = chasing ? chaseSprite : normalSprite;
            if(sprite != null) spriteRenderer.sprite = sprite;
            spriteRenderer.color = chasing && chaseSprite == null ? placeholderChaseColor : normalColor;
        }
        if(chaseAudio != null)
        {
            if(chasing && !chaseAudio.isPlaying) chaseAudio.Play();
            else if(!chasing) chaseAudio.Stop();
        }
    }

    void Wandering()
    {
        //Still going through doors to another room; the wander timers start once it's in there
        if(UpdateTravel()) return;
        if(wanderRooms.Length > 0)
        {
            WanderRooms();
            return;
        }
        if(wanderPoints.Length == 0) return;
        if(!resting) wanderCountTime += Time.deltaTime;
        if(ArrivedAndRested() || wanderCountTime >= wanderInterval)
        {
            currentPointsindex = (currentPointsindex+1)%wanderPoints.Length;
            Vector3 point = wanderPoints[currentPointsindex].position + (Vector3)(UnityEngine.Random.insideUnitCircle * wanderJitter);
            SetWanderSpeed();
            TravelTo(point);
            wanderCountTime = 0f;
        }
    }

    //Roams the wander rooms: short strolls with pauses inside one room, then moves on to another
    void WanderRooms()
    {
        Transform here = RoomMap.RoomAt(transform.position);
        if(!resting) wanderCountTime += Time.deltaTime;
        bool legDone = ArrivedAndRested() || wanderCountTime >= wanderInterval;
        roomTimer -= Time.deltaTime;

        if(wanderRoom == null || (roomTimer <= 0f && legDone))
        {
            wanderRoom = PickWanderRoom(here);
            roomTimer = UnityEngine.Random.Range(roomStayTime.x, roomStayTime.y);
            if(wanderRoom != here)
            {
                SetWanderSpeed();
                TravelTo(RandomPointIn(wanderRoom));
                wanderCountTime = 0f;
                return;
            }
        }
        if(legDone)
        {
            SetWanderSpeed();
            agent.isStopped = false;
            agent.SetDestination(StrollPoint(wanderRoom));
            wanderCountTime = 0f;
        }
    }

    Transform PickWanderRoom(Transform current)
    {
        if(wanderRooms.Length == 1) return wanderRooms[0];
        //Somewhere else if there's a choice
        for(int i = 0; i < 6; i++)
        {
            Transform room = wanderRooms[UnityEngine.Random.Range(0, wanderRooms.Length)];
            if(room != current) return room;
        }
        return wanderRooms[0];
    }

    //A spot a few steps away in some random direction, kept inside the room and reachable
    Vector3 StrollPoint(Transform room)
    {
        Bounds bounds = default;
        bool hasBounds = room != null && RoomMap.TryGetBounds(room, out bounds);
        for(int i = 0; i < 8; i++)
        {
            Vector2 dir = UnityEngine.Random.insideUnitCircle.normalized;
            Vector3 p = transform.position + (Vector3)(dir * UnityEngine.Random.Range(strollDistance.x, strollDistance.y));
            if(hasBounds)
            {
                p.x = Mathf.Clamp(p.x, bounds.min.x + 1.5f, bounds.max.x - 1.5f);
                p.y = Mathf.Clamp(p.y, bounds.min.y + 1.5f, bounds.max.y - 1.5f);
            }
            if(NavMesh.SamplePosition(p, out NavMeshHit hit, 1f, NavMesh.AllAreas) && CanReachPoint(hit.position)) return hit.position;
        }
        return RandomPointIn(room);
    }

    //Random floor spot anywhere in a room
    Vector3 RandomPointIn(Transform room)
    {
        if(room == null || !RoomMap.TryGetBounds(room, out Bounds b)) return transform.position;
        for(int i = 0; i < 10; i++)
        {
            Vector3 p = new Vector3(UnityEngine.Random.Range(b.min.x + 1.5f, b.max.x - 1.5f), UnityEngine.Random.Range(b.min.y + 1.5f, b.max.y - 1.5f), 0f);
            if(NavMesh.SamplePosition(p, out NavMeshHit hit, 1f, NavMesh.AllAreas)) return hit.position;
        }
        return new Vector3(b.center.x, b.center.y, 0f);
    }

    //Reached the stroll target and stood around for a moment
    bool ArrivedAndRested()
    {
        if(agent.pathPending || agent.remainingDistance > 0.15f)
        {
            resting = false;
            return false;
        }
        if(!resting)
        {
            resting = true;
            pauseTimer = UnityEngine.Random.Range(wanderPause.x, wanderPause.y);
        }
        pauseTimer -= Time.deltaTime;
        if(pauseTimer > 0f) return false;
        resting = false;
        return true;
    }

    //Ambling, not marching: each stroll at a slightly different pace
    void SetWanderSpeed()
    {
        agent.speed = moveSpeed * UnityEngine.Random.Range(wanderMinSpeed, 1f);
    }

    //Walk to a point, going through doors (greedy, nearest-to-target door each room) if it's in another room
    void TravelTo(Vector3 point)
    {
        travelRoute.Clear();
        travelTarget = point;
        Transform here = RoomMap.RoomAt(transform.position);
        Transform there = RoomMap.RoomAt(point);
        if(here != null && there != null && here != there)
        {
            List<RoomDoor> route = RoomMap.GreedyRoute(here, there, point);
            if(route != null) foreach(var door in route) travelRoute.Enqueue(door);
            else Debug.LogWarning($"Enemy: no door route from {here.name} to {there.name}", this);
        }
        NextTravelLeg();
    }

    void NextTravelLeg()
    {
        travelDoorTimer = 0f;
        travelStuckTimer = 0f;
        agent.isStopped = false;
        Vector3 dest = travelRoute.Count > 0 ? travelRoute.Peek().ExitPoint : travelTarget;
        if(NavMesh.SamplePosition(dest, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas)) dest = hit.position;
        agent.SetDestination(dest);
    }

    //True while there are still doors to go through
    bool UpdateTravel()
    {
        if(travelRoute.Count == 0) return false;
        RoomDoor door = travelRoute.Peek();
        if(door == null || door.linkedDoor == null)
        {
            travelRoute.Clear();
            NextTravelLeg();
            return false;
        }
        travelStuckTimer += Time.deltaTime;
        bool atDoor = Vector2.Distance(transform.position, door.ExitPoint) <= doorReachDistance;
        if(!atDoor && travelStuckTimer < travelStuckTime) return true;

        travelDoorTimer += Time.deltaTime;
        if(travelDoorTimer < travelDoorPause) return true;
        if(NavMesh.SamplePosition(door.linkedDoor.ExitPoint, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }
        travelRoute.Dequeue();
        NextTravelLeg();
        return true;
    }

    void StopTravel()
    {
        travelRoute.Clear();
    }

    void Chasing()
    {
        if(target == null)
        {
            loseTimer += Time.deltaTime;
            if(loseTimer >= loseTargetDelay)
            {
                reChaseTimer = reChaseCooldown;
                currentState = EnemyState.Return;
                agent.isStopped = false;
                GoBackToStart();
            }
            return;
        }
        float distancetoPlayer = Vector2.Distance(transform.position,target.position);
        //Just came through a door behind the player: they're usually across the room by now, don't give up on distance yet
        if(doorGraceTimer > 0f) doorGraceTimer -= Time.deltaTime;
        bool tooFar = distancetoPlayer > chaseLoseDistance && doorGraceTimer <= 0f;
        bool lostPlayer = tooFar || !cachedCanReach;
        //On top of it: the kill check / eating takes over
        if(distancetoPlayer <= enemyKillRange) return;
        if(!lostPlayer && NavMesh.SamplePosition(target.position, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
            loseTimer = 0f;
        }
        else
        {
            loseTimer += Time.deltaTime;
            if(loseTimer >= loseTargetDelay)
            {
                Debug.Log(tooFar ? $"Enemy lost the player: too far ({distancetoPlayer:0.0} > {chaseLoseDistance})" : "Enemy lost the player: no path to them", this);
                reChaseTimer = reChaseCooldown;
                currentState = EnemyState.Return;
                agent.isStopped = false;
                GoBackToStart();
            }
        }
    }

    void StartEating(Component bait)
    {
        eatingBody = bait;
        eatTimer = 0f;
        currentState = EnemyState.Eat;
        ClearDoorFollow();
        StopTravel();
        isWaitingAtSound = false;
        agent.isStopped = true;
        Debug.Log("Eating old body");
    }

    void Eating()
    {
        eatTimer += Time.deltaTime;
        if(eatTimer >= eatDuration)
        {
            if(eatingBody is BodyScript eatenBody) eatenBody.Consume();
            else if(eatingBody is DraggableProp eatenProp) eatenProp.Consume();
            eatingBody = null;
            target = null;
            targetInSight = false;
            currentState = EnemyState.Return;
            agent.isStopped = false;
            GoBackToStart();
        }
    }

    //Back to its patch, wherever the chase ended up (goes through doors if needed):
    //the room it's in if that's a wander room, else a random wander room, else the first wander point, else where it started
    void GoBackToStart()
    {
        ClearDoorFollow();
        if(wanderRooms.Length > 0)
        {
            Transform here = RoomMap.RoomAt(transform.position);
            Transform room = Array.IndexOf(wanderRooms, here) >= 0 ? here : PickWanderRoom(here);
            wanderRoom = room;
            TravelTo(RandomPointIn(room));
        }
        else if(wanderPoints.Length > 0)
        {
            TravelTo(wanderPoints[0].position);
        }
        else
        {
            TravelTo(spawnPosition);
        }
    }

    void HandleDoorUsed(BodyScript body, RoomDoor from, RoomDoor to)
    {
        if(this == null)
        {
            RoomDoor.OnBodyUsedDoor -= HandleDoorUsed;
            return;
        }
        if(!followThroughDoors || target == null || body.transform != target) return;
        if(currentState == EnemyState.FollowDoor)
        {
            //Already following, the player went through another door further on
            if(!from.isShortcut)
            {
                pendingDoors.Enqueue(from);
                return;
            }
            //Shortcut further on: queue the way round to where they came out
            List<RoomDoor> detour = RoomMap.FindRoute(RoomMap.RoomOf(from), RoomMap.RoomOf(to));
            if(detour != null) foreach(var door in detour) pendingDoors.Enqueue(door);
            return;
        }
        //Chasing, or lost you only a moment ago: it still heard which door you took
        bool hunting = currentState == EnemyState.Chase ||
                       (Time.time - lastChaseTime <= doorMemory && currentState != EnemyState.Eat && currentState != EnemyState.Stunned);
        if(!hunting) return;
        if(!from.isShortcut)
        {
            if(debugLogs && currentState != EnemyState.Chase) Debug.Log("Enemy: lost you a moment ago, still following through the door", this);
            StartFollowDoor(from);
            return;
        }

        //Can't fit through the Major's shortcut: walk round through the normal doors to the room they came out in
        Transform here = RoomMap.RoomAt(transform.position);
        if(here == null) here = RoomMap.RoomOf(from);
        List<RoomDoor> route = RoomMap.FindRoute(here, RoomMap.RoomOf(to));
        if(route == null || route.Count == 0)
        {
            Debug.Log("Enemy: no way round to the shortcut, giving up", this);
            return;
        }
        pendingDoors.Clear();
        for(int i = 1; i < route.Count; i++) pendingDoors.Enqueue(route[i]);
        StartFollowDoor(route[0]);
        Debug.Log($"Enemy: player took a shortcut, going the long way ({route.Count} doors)", this);
    }

    void StartFollowDoor(RoomDoor door)
    {
        StopTravel();
        followDoor = door;
        doorWaitTimer = 0f;
        currentState = EnemyState.FollowDoor;
        agent.isStopped = false;
        if(NavMesh.SamplePosition(door.ExitPoint, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
        lastState = currentState;
        ApplyStateLook();
    }

    void FollowingDoor()
    {
        if(followDoor == null || followDoor.linkedDoor == null)
        {
            ClearDoorFollow();
            currentState = EnemyState.Return;
            GoBackToStart();
            return;
        }
        if(Vector2.Distance(transform.position, followDoor.ExitPoint) > doorReachDistance) return;

        doorWaitTimer += Time.deltaTime;
        if(doorWaitTimer < doorFollowDelay) return;

        RoomDoor through = followDoor;
        if(NavMesh.SamplePosition(through.linkedDoor.ExitPoint, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }
        else
        {
            Debug.LogWarning("No NavMesh at " + through.linkedDoor.name + ", can't follow through", through.linkedDoor);
            ClearDoorFollow();
            currentState = EnemyState.Return;
            return;
        }

        if(pendingDoors.Count > 0)
        {
            StartFollowDoor(pendingDoors.Dequeue());
            return;
        }
        followDoor = null;
        currentState = EnemyState.Chase;
        loseTimer = 0f;
        doorGraceTimer = doorChaseGrace;
        cachedCanReach = CanReachPlayer();
    }

    void ClearDoorFollow()
    {
        pendingDoors.Clear();
        followDoor = null;
    }

    //Radio: goes to it whatever room it's in (through the doors), unless it's busy chasing, eating or stunned
    public void Lure(Vector3 position)
    {
        if(playerDead || agent == null || !isActiveAndEnabled) return;
        if(currentState == EnemyState.Chase || currentState == EnemyState.Eat || currentState == EnemyState.Stunned) return;
        ClearDoorFollow();
        soundTargetPos = position;
        currentState = EnemyState.SoundTriggered;
        isWaitingAtSound = false;
        agent.isStopped = false;
        TravelTo(position);
        if(debugLogs) Debug.Log("Enemy: lured by a radio", this);
    }

    public void SoundTrigger(Vector3 soundPos)
    {
        soundTargetPos = soundPos;
        if(currentState != EnemyState.Chase)
        {
            StopTravel();
            currentState = EnemyState.SoundTriggered;
            isWaitingAtSound = false;
            agent.isStopped = false;
            agent.SetDestination(soundTargetPos);
        }
    }

    void CheckSound()
    {
        //Still going through doors to a lure in another room
        if(UpdateTravel()) return;
        if(!isWaitingAtSound)
        {
            if(!agent.pathPending && agent.remainingDistance < 0.2f)
            {
                isWaitingAtSound = true;
                timeCheckCounter = 0f;
            }
        }
        else
        {
            timeCheckCounter += Time.deltaTime;
            if(timeCheckCounter >= timeChecking)
            {
                currentState = EnemyState.Return;
                GoBackToStart();
                isWaitingAtSound = false;
            }
        }
    }

    void ReturnBack()
    {
        if(UpdateTravel()) return;
        if(!agent.pathPending && agent.remainingDistance < 0.2f)
        {
            currentState = EnemyState.Wander;
            wanderCountTime = 0f;
            currentPointsindex = 0;
            //Linger in the room it came back to before roaming on
            resting = false;
            roomTimer = UnityEngine.Random.Range(roomStayTime.x, roomStayTime.y);
            if(invisibleWall != null)
            {
                invisibleWall.enabled = true;
            }
        }
    }

    //Only logs when something changes, so the Console doesn't flood
    void LogTargetChanges()
    {
        if(target != loggedTarget)
        {
            loggedTarget = target;
            loggedReach = true;
            if(target == null) Debug.Log($"Enemy [{currentState}]: no target", this);
            else
            {
                BodyScript body = target.GetComponent<BodyScript>();
                string kind = body == null ? (target.GetComponent<DraggableProp>() != null ? "corpse prop bait" : "not a body?") : body.isCurrentBody ? "player" : body.IsBait ? "bait" : "unpossessed body?";
                Debug.Log($"Enemy [{currentState}]: target {target.name} ({kind}), in sight {targetInSight}, reachable {cachedCanReach}", this);
            }
        }
        if(target != null && cachedCanReach != loggedReach)
        {
            loggedReach = cachedCanReach;
            if(!cachedCanReach)
            {
                bool onMesh = NavMesh.SamplePosition(target.position, out NavMeshHit hit, 1.5f, NavMesh.AllAreas);
                string why = !onMesh ? "no NavMesh within 1.5 of it" : "NavMesh path from the enemy doesn't connect (other room / gap in the bake)";
                Debug.LogWarning($"Enemy [{currentState}]: can't reach {target.name}: {why}", this);
            }
            else Debug.Log($"Enemy [{currentState}]: can reach {target.name} again", this);
        }
    }

    //Inside the vision cone, or close enough to sense whichever way it's facing
    bool InView(Vector2 point)
    {
        Vector2 delta = point - (Vector2)transform.position;
        if(delta.magnitude <= senseRadius) return true;
        return Vector2.Angle(facing, delta) <= viewAngle * 0.5f;
    }

    void UpdateFacing()
    {
        Vector3 v = agent != null ? agent.velocity : Vector3.zero;
        if(v.sqrMagnitude > 0.05f) facing = ((Vector2)v).normalized;
    }

    //Translucent fan on the floor showing what it can see (pale normally, red while chasing, hidden when stunned)
    void LateUpdate()
    {
        if(agent == null) return;
        UpdateFacing();
        if(!showVisionCone || playerDead || IsStunned)
        {
            if(coneRenderer != null) coneRenderer.enabled = false;
            return;
        }
        if(coneRenderer == null) BuildCone();
        if(coneRenderer == null) return;
        if(!Mathf.Approximately(builtRadius, trackDistance) || !Mathf.Approximately(builtAngle, viewAngle)) RebuildConeMesh();
        coneRenderer.enabled = true;
        bool chasing = currentState == EnemyState.Chase || currentState == EnemyState.FollowDoor;
        Color c = chasing ? chaseConeColor : coneColor;
        if(c != builtColor) RecolorCone(c);
        coneRenderer.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg);
    }

    void BuildCone()
    {
        if(coneShader == null) coneShader = Shader.Find("Jam/SpriteSilhouette");
        if(coneShader == null) return;
        GameObject obj = new GameObject("VisionCone");
        obj.transform.SetParent(transform, false);
        coneMesh = obj.AddComponent<MeshFilter>();
        coneRenderer = obj.AddComponent<MeshRenderer>();
        coneRenderer.sharedMaterial = new Material(coneShader);
        if(SortingLayer.NameToID("Prop") != 0) coneRenderer.sortingLayerName = "Prop";
        coneRenderer.sortingOrder = -20;
        coneMesh.mesh = new Mesh();
        RebuildConeMesh();
    }

    //Fan pointing along +x; rotated to face where it's going
    void RebuildConeMesh()
    {
        const int segments = 24;
        builtRadius = trackDistance;
        builtAngle = viewAngle;
        var verts = new Vector3[segments + 2];
        var tris = new int[segments * 3];
        verts[0] = Vector3.zero;
        for(int i = 0; i <= segments; i++)
        {
            float a = (-viewAngle * 0.5f + viewAngle * i / segments) * Mathf.Deg2Rad;
            verts[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * trackDistance;
        }
        for(int i = 0; i < segments; i++)
        {
            tris[i * 3] = 0;
            tris[i * 3 + 1] = i + 2;
            tris[i * 3 + 2] = i + 1;
        }
        Mesh mesh = coneMesh.mesh;
        mesh.Clear();
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.uv = new Vector2[verts.Length];
        builtColor = new Color(0, 0, 0, -1f);
    }

    void RecolorCone(Color c)
    {
        builtColor = c;
        Mesh mesh = coneMesh.mesh;
        var colors = new Color[mesh.vertexCount];
        for(int i = 0; i < colors.Length; i++) colors[i] = c;
        //Fades towards the edge of its sight
        for(int i = 1; i < colors.Length; i++) colors[i].a = c.a * 0.35f;
        mesh.colors = colors;
    }

    Transform FindTarget()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position,trackDistance,playerLayer | baitLayer);
        Transform nearest = null;
        float nearestDist = float.MaxValue;
        foreach(var hit in hits)
        {
            if(!IsValidTarget(hit.transform)) continue;
            if(!InView(hit.transform.position)) continue;
            if(sightBlockers != 0 && Physics2D.Linecast(transform.position,hit.transform.position,sightBlockers)) continue;
            float dist = Vector2.Distance(transform.position,hit.transform.position);
            if(dist < nearestDist)
            {
                nearestDist = dist;
                nearest = hit.transform;
            }
        }
        return nearest;
    }

    //Current body or an uneaten left behind body. Fresh hosts are ignored
    bool IsValidTarget(Transform t)
    {
        if(t == null) return false;
        BodyScript body = t.GetComponent<BodyScript>();
        //Corpse prop that's been dragged around bleeding
        if(body == null)
        {
            DraggableProp prop = t.GetComponent<DraggableProp>();
            return prop != null && prop.IsBait;
        }
        //Playing dead / in a locker: not worth chasing (still dies if the enemy walks into it, see the kill check)
        if(body.isCurrentBody) return !body.isPlayingDead && !body.isHiding;
        return IsFood(body);
    }

    //Left-behind bodies, and prey (Researcher F) even with nobody in her
    static bool IsFood(BodyScript body)
    {
        if(body.IsBait) return true;
        return !body.isCurrentBody && !body.isConsumed && body.customBodySO != null && body.customBodySO.isPrey;
    }

    Component BaitInRange()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position,enemyKillRange,baitLayer);
        foreach(var hit in hits)
        {
            BodyScript body = hit.GetComponent<BodyScript>();
            if(body != null && IsFood(body)) return body;
            DraggableProp prop = hit.GetComponent<DraggableProp>();
            if(prop != null && prop.IsBait) return prop;
        }
        return null;
    }

    bool CanReachPlayer()
    {
        if(target == null) return false;
        return CanReachPoint(target.position);
    }

    bool CanReachPoint(Vector3 position)
    {
        if(!NavMesh.SamplePosition(position, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            return false;
        //From where the agent really is on the NavMesh: the transform sits Base Offset away from it, and paths
        //started from there failed now and then, which made it drop chases
        if(agent == null || !agent.isOnNavMesh) return false;
        agent.CalculatePath(hit.position, pathCache);
        return pathCache.status == NavMeshPathStatus.PathComplete;
    }

    //Select the enemy to see its senses: yellow = sight, cyan = hearing (for loudness 1, e.g. sprinting)
    void OnDrawGizmosSelected()
    {
        //Vision cone (facing down until it moves), sense circle, hearing, kill range
        Gizmos.color = Color.yellow;
        Vector2 f = Application.isPlaying ? facing : Vector2.down;
        Vector3 l = Quaternion.Euler(0f, 0f, viewAngle * 0.5f) * f * trackDistance;
        Vector3 r = Quaternion.Euler(0f, 0f, -viewAngle * 0.5f) * f * trackDistance;
        Gizmos.DrawLine(transform.position, transform.position + l);
        Gizmos.DrawLine(transform.position, transform.position + r);
        Gizmos.DrawWireSphere(transform.position, senseRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, hearingRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, enemyKillRange);
    }
}
