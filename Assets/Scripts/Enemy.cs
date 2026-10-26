using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour
{
    public enum EnemyState {Wander , SoundTriggered , Chase , Return}
    public EnemyState currentState = EnemyState.Wander;
    [Header("References")]
    [SerializeField] Transform target;
    NavMeshAgent agent;
    public Collider2D wanderRoomBound;
    public LayerMask playerLayer;
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
    private NavMeshPath pathCache;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
        agent.updateRotation = false;
        agent.updateUpAxis = false;
        wanderCountTime = wanderInterval;
        pathCache = new NavMeshPath();
    }

    void Update()
    {
        if(playerDead) return;
        if(Physics2D.OverlapCircle(transform.position, enemyKillRange, playerLayer) != null)
        {
            playerDead = true;
            agent.isStopped = true;
            Debug.Log("Player ded.");
            //TODO Gameover
            return;
        }
        if(reChaseTimer > 0f) reChaseTimer -= Time.deltaTime;
        pathCheckTimer -= Time.deltaTime;

        if(pathCheckTimer <= 0f)
        {
            pathCheckTimer = pathCheckInterval;
            cachedCanReach = CanReachPlayer();
        }

        if(PlayerinSight()&& cachedCanReach && currentState != EnemyState.Chase && reChaseTimer <= 0f)
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
            
        }
    }

    void Wandering()
    {
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
                agent.SetDestination(wanderPoints[0].position);
            }
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
                agent.SetDestination(wanderPoints[0].position);
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

    bool PlayerinSight()
    {
        Collider2D detectBody = Physics2D.OverlapCircle(transform.position,trackDistance,playerLayer);
        return detectBody != null;
    }

    bool CanReachPlayer()
    {
        if(!NavMesh.SamplePosition(target.position, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            return false;
 
        NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, pathCache);
        return pathCache.status == NavMeshPathStatus.PathComplete;
    }
}
