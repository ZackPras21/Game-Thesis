using UnityEngine;
using UnityEngine.AI;

public enum EnemyHighLevelAction
{
    Idle = 0,
    Patrol = 1,
    Detect = 2,
    Chase = 3,
    Attack = 4,
    Dead = 5
}

public static class NormalEnemyActions
{
    private static float patrolIdleTimer = 0f;
    private static bool isIdlingAtSpawn = false;

    private const float IDLE_DURATION_AT_SPAWN = 5.0f;
    private const float MOVEMENT_DESTINATION_OFFSET = 0.5f;
    private const float WAYPOINT_ARRIVAL_TOLERANCE = 0.1f;

    public static void ApplyMovement(
        Transform agentTransform,
        NavMeshAgent navAgent,
        float moveX,
        float moveZ,
        float rotateY,
        float moveSpeed,
        float turnSpeed)
    {
        RotateAgent(agentTransform, rotateY, turnSpeed);
        MoveAgent(agentTransform, navAgent, moveX, moveZ, moveSpeed);
    }

    public static void DoIdle(NavMeshAgent navAgent)
    {
        StopAgent(navAgent);
    }

    public static void DoPatrol(
        NavMeshAgent navAgent,
        Transform[] patrolPoints,
        ref int currentPatrolIndex,
        ref int patrolLoopsCompleted,
        Animator animator = null)
    {
        if (IsPatrolPointsEmpty(patrolPoints)) return;

        if (isIdlingAtSpawn)
        {
            HandleSpawnIdling(navAgent, patrolPoints, ref currentPatrolIndex, animator);
            return;
        }

        ExecutePatrolMovement(navAgent, patrolPoints, ref currentPatrolIndex, ref patrolLoopsCompleted, animator);
    }

    public static void DoDetect()
    {
        // Placeholder for alert animations or sounds
    }

    public static void DoChase(NavMeshAgent navAgent, Transform playerTransform)
    {
        SetAgentDestination(navAgent, playerTransform.position);
    }

    public static void DoAttack(NavMeshAgent navAgent)
    {
        StopAgent(navAgent);
    }

    public static void DoDead(Transform agentTransform, NavMeshAgent navAgent)
    {
        StopAgent(navAgent);
    }

    private static void RotateAgent(Transform agentTransform, float rotateY, float turnSpeed)
    {
        agentTransform.Rotate(0f, rotateY * turnSpeed * Time.deltaTime, 0f);
    }

    private static void MoveAgent(Transform agentTransform, NavMeshAgent navAgent, float moveX, float moveZ, float moveSpeed)
    {
        Vector3 localMovement = new Vector3(moveX, 0f, moveZ);
        Vector3 worldMovement = agentTransform.TransformDirection(localMovement).normalized;

        if (HasMovementInput(moveX, moveZ))
        {
            Vector3 destination = agentTransform.position + worldMovement * MOVEMENT_DESTINATION_OFFSET;
            SetAgentDestination(navAgent, destination, moveSpeed);
        }
        else
        {
            StopAgent(navAgent);
        }
    }

    private static bool HasMovementInput(float moveX, float moveZ)
    {
        return moveZ != 0f || moveX != 0f;
    }

    private static void SetAgentDestination(NavMeshAgent navAgent, Vector3 destination, float speed = 0f)
    {
        navAgent.isStopped = false;
        if (speed > 0f) navAgent.speed = speed;
        navAgent.SetDestination(destination);
    }

    private static void StopAgent(NavMeshAgent navAgent)
    {
        navAgent.velocity = Vector3.zero;
        navAgent.isStopped = true;
    }

    private static bool IsPatrolPointsEmpty(Transform[] patrolPoints)
    {
        return patrolPoints == null || patrolPoints.Length == 0;
    }

    private static void HandleSpawnIdling(NavMeshAgent navAgent, Transform[] patrolPoints, ref int currentPatrolIndex, Animator animator)
    {
        patrolIdleTimer -= Time.deltaTime;
        StopAgent(navAgent);
        SetAnimationState(animator, isWalking: false, isIdle: true);

        if (patrolIdleTimer <= 0f)
        {
            CompleteSpawnIdling(patrolPoints, ref currentPatrolIndex);
        }
    }

    private static void CompleteSpawnIdling(Transform[] patrolPoints, ref int currentPatrolIndex)
    {
        isIdlingAtSpawn = false;
        currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
    }

    private static void ExecutePatrolMovement(
        NavMeshAgent navAgent,
        Transform[] patrolPoints,
        ref int currentPatrolIndex,
        ref int patrolLoopsCompleted,
        Animator animator)
    {
        Vector3 targetPosition = patrolPoints[currentPatrolIndex].position;
        SetAgentDestination(navAgent, targetPosition);
        SetAnimationState(animator, isWalking: true, isIdle: false);

        if (HasReachedWaypoint(navAgent))
        {
            HandleWaypointReached(patrolPoints, ref currentPatrolIndex, ref patrolLoopsCompleted);
        }
    }

    private static bool HasReachedWaypoint(NavMeshAgent navAgent)
    {
        return !navAgent.pathPending && navAgent.remainingDistance <= navAgent.stoppingDistance + WAYPOINT_ARRIVAL_TOLERANCE;
    }

    private static void HandleWaypointReached(Transform[] patrolPoints, ref int currentPatrolIndex, ref int patrolLoopsCompleted)
    {
        int nextIndex = (currentPatrolIndex + 1) % patrolPoints.Length;

        if (IsReturningToSpawn(nextIndex))
        {
            InitiateSpawnIdling(ref patrolLoopsCompleted);
        }
        else
        {
            currentPatrolIndex = nextIndex;
        }
    }

    private static bool IsReturningToSpawn(int nextIndex)
    {
        return nextIndex == 0;
    }

    private static void InitiateSpawnIdling(ref int patrolLoopsCompleted)
    {
        patrolLoopsCompleted++;
        isIdlingAtSpawn = true;
        patrolIdleTimer = IDLE_DURATION_AT_SPAWN;
    }

    private static void SetAnimationState(Animator animator, bool isWalking, bool isIdle)
    {
        if (animator == null) return;

        animator.SetBool("isWalking", isWalking);
        animator.SetBool("isIdle", isIdle);
    }
}