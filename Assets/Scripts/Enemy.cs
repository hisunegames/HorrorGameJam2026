using System;
using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour
{
    public static event Action OnPlayerKilled;
    public enum EnemyState {Wander , SoundTriggered , Chase , Return , Eat}
    public EnemyState currentState = EnemyState.Wander;
    [Header("References")]
    [SerializeField] Transform target;
    NavMeshAgent agent;
    public Collider2D wanderRoomBound;
    public LayerMask playerLayer;
    public LayerMask baitLayer;
    public NavMeshObstacle invisibleWall;

    [Header("Wander Settings")]
    public Transform[] wanderPoints;
    private int currentPointsindex = 0;
    public float wanderInterval = 5f;
    public float wanderCountTime;

    [Header("Check Sound")]
    private bool isWaitingAtSound = false;
    public float timeChecking = 5f;
    private float timeCheckCounter;

    [Header("Enemy Stats")]
    public float moveSpeed = 8f;
    public float trackDistance = 3f;
    public float chaseLoseDistance = 12f;
    public float enemyKillRange = 0.5f;
    private Vector2 soundTargetPos;

    [Header("Chasing")]
    public float loseTargetDelay = 1.5f;
    public float reChaseCooldown = 2f;
    public float navSampleRadius = 1.5f;
    public float pathCheckInterval = 0.2f;
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
    private BodyScript eatingBody;

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

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
        agent.updateRotation = false;
        agent.updateUpAxis = false;
        wanderCountTime = wanderInterval;
        pathCache = new NavMeshPath();
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
        if(Physics2D.OverlapCircle(transform.position, enemyKillRange, playerLayer) != null)
        {
            playerDead = true;
            agent.isStopped = true;
            Debug.Log("Player ded.");
            if(chaseAudio != null) chaseAudio.Stop();
            OnPlayerKilled?.Invoke();
            return;
        }
        if(currentState != EnemyState.Eat)
        {
            BodyScript bait = BaitInRange();
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
        }

        if(targetInSight && cachedCanReach && currentState != EnemyState.Chase && currentState != EnemyState.Eat && reChaseTimer <= 0f)
        {
            currentState = EnemyState.Chase;
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

        }
        if(currentState != lastState)
        {
            lastState = currentState;
            ApplyStateLook();
        }
    }

    //Sprite, crying audio and speed all switch the moment the chase starts
    void ApplyStateLook()
    {
        bool chasing = currentState == EnemyState.Chase;
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
        if(wanderPoints.Length == 0) return;
        wanderCountTime += Time.deltaTime;
        if(wanderCountTime >= wanderInterval||(!agent.pathPending && agent.remainingDistance < 0.1f))
        {
            currentPointsindex = (currentPointsindex+1)%wanderPoints.Length;
            agent.SetDestination(wanderPoints[currentPointsindex].position);
            wanderCountTime = 0f;
        }
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
        bool lostPlayer = distancetoPlayer > chaseLoseDistance || !cachedCanReach;
        if(distancetoPlayer <= enemyKillRange)
        {
            Debug.Log("Player ded.");
            //TODO Gameover
            return;
        }
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
                reChaseTimer = reChaseCooldown;
                currentState = EnemyState.Return;
                agent.isStopped = false;
                GoBackToStart();
            }
        }
    }

    void StartEating(BodyScript bait)
    {
        eatingBody = bait;
        eatTimer = 0f;
        currentState = EnemyState.Eat;
        isWaitingAtSound = false;
        agent.isStopped = true;
        Debug.Log("Eating old body");
    }

    void Eating()
    {
        eatTimer += Time.deltaTime;
        if(eatTimer >= eatDuration)
        {
            if(eatingBody != null)
            {
                eatingBody.Consume();
            }
            eatingBody = null;
            target = null;
            targetInSight = false;
            currentState = EnemyState.Return;
            agent.isStopped = false;
            GoBackToStart();
        }
    }

    void GoBackToStart()
    {
        if(wanderPoints.Length > 0)
        {
            agent.SetDestination(wanderPoints[0].position);
        }
    }

    public void SoundTrigger(Vector3 soundPos)
    {
        soundTargetPos = soundPos;
        if(currentState != EnemyState.Chase)
        {
            currentState = EnemyState.SoundTriggered;
            isWaitingAtSound = false;
            agent.isStopped = false;
            agent.SetDestination(soundTargetPos);
        }
    }

    void CheckSound()
    {
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
        if(!agent.pathPending && agent.remainingDistance < 0.2f)
        {
            currentState = EnemyState.Wander;
            wanderCountTime = 0f;
            currentPointsindex = 0;
            if(invisibleWall != null)
            {
                invisibleWall.enabled = true;
            }
        }
    }

    Transform FindTarget()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position,trackDistance,playerLayer | baitLayer);
        Transform nearest = null;
        float nearestDist = float.MaxValue;
        foreach(var hit in hits)
        {
            if(!IsValidTarget(hit.transform)) continue;
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
        if(body == null) return false;
        return body.isCurrentBody || body.IsBait;
    }

    BodyScript BaitInRange()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position,enemyKillRange,baitLayer);
        foreach(var hit in hits)
        {
            BodyScript body = hit.GetComponent<BodyScript>();
            if(body != null && body.IsBait) return body;
        }
        return null;
    }

    bool CanReachPlayer()
    {
        if(target == null) return false;
        if(!NavMesh.SamplePosition(target.position, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            return false;
 
        NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, pathCache);
        return pathCache.status == NavMeshPathStatus.PathComplete;
    }
}
