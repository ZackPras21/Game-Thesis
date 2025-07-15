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

public sealed class NormalEnemyActions
{
    #region Fields
    private float patrolIdleTimer;
    private bool isIdlingAtSpawn;
    #endregion

    #region Constants
    private const float IDLE_DURATION_AT_SPAWN = 5.0f;
    private const float MOVEMENT_DESTINATION_OFFSET = 0.5f;
    private const float WAYPOINT_ARRIVAL_TOLERANCE = 0.1f;
    #endregion

    #region Movement Actions
    public void ApplyMovement(
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

    private static void RotateAgent(Transform agentTransform, float rotateY, float turnSpeed) => 
        agentTransform.Rotate(0f, rotateY * turnSpeed * Time.deltaTime, 0f);

    private void MoveAgent(Transform agentTransform, NavMeshAgent navAgent, float moveX, float moveZ, float moveSpeed)
    {
        Vector3 localMovement = new(moveX, 0f, moveZ);
        Vector3 worldMovement = agentTransform.TransformDirection(localMovement).normalized;

        if (HasMovementInput(moveX, moveZ))
            SetAgentDestination(navAgent, agentTransform.position + worldMovement * MOVEMENT_DESTINATION_OFFSET, moveSpeed);
        else
            StopAgent(navAgent);
    }

    private static bool HasMovementInput(float moveX, float moveZ) => 
        moveZ != 0f || moveX != 0f;

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
    #endregion

    #region Behavior Actions
    public static void DoIdle(NavMeshAgent navAgent) => StopAgent(navAgent);
    
    public static void DoChase(NavMeshAgent navAgent, Transform playerTransform) => 
        SetAgentDestination(navAgent, playerTransform.position);
    
    public static void DoAttack(NavMeshAgent navAgent) => StopAgent(navAgent);
    
    public static void DoDead(Transform agentTransform, NavMeshAgent navAgent) => StopAgent(navAgent);

    public void DoPatrol(
        NavMeshAgent navAgent,
        Transform[] patrolPoints,
        ref int currentPatrolIndex,
        ref int patrolLoopsCompleted,
        Animator animator = null)
    {
        if (IsPatrolPointsEmpty(patrolPoints)) return;

        if (isIdlingAtSpawn)
            HandleSpawnIdling(navAgent, patrolPoints, ref currentPatrolIndex, animator);
        else
            ExecutePatrolMovement(navAgent, patrolPoints, ref currentPatrolIndex, ref patrolLoopsCompleted, animator);
    }

    private static bool IsPatrolPointsEmpty(Transform[] patrolPoints) => 
        patrolPoints == null || patrolPoints.Length == 0;

    private void HandleSpawnIdling(NavMeshAgent navAgent, Transform[] patrolPoints, ref int currentPatrolIndex, Animator animator)
    {
        patrolIdleTimer -= Time.deltaTime;
        StopAgent(navAgent);
        SetAnimationState(animator, isWalking: false, isIdle: true);

        if (patrolIdleTimer <= 0f)
            CompleteSpawnIdling(patrolPoints, ref currentPatrolIndex);
    }

    private void CompleteSpawnIdling(Transform[] patrolPoints, ref int currentPatrolIndex)
    {
        isIdlingAtSpawn = false;
        currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
    }

    private void ExecutePatrolMovement(
        NavMeshAgent navAgent,
        Transform[] patrolPoints,
        ref int currentPatrolIndex,
        ref int patrolLoopsCompleted,
        Animator animator)
    {
        SetAgentDestination(navAgent, patrolPoints[currentPatrolIndex].position);
        SetAnimationState(animator, isWalking: true, isIdle: false);

        if (HasReachedWaypoint(navAgent))
            HandleWaypointReached(patrolPoints, ref currentPatrolIndex, ref patrolLoopsCompleted);
    }

    private static bool HasReachedWaypoint(NavMeshAgent navAgent) => 
        !navAgent.pathPending && 
        navAgent.remainingDistance <= navAgent.stoppingDistance + WAYPOINT_ARRIVAL_TOLERANCE;

    private void HandleWaypointReached(Transform[] patrolPoints, ref int currentPatrolIndex, ref int patrolLoopsCompleted)
    {
        int nextIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
        
        if (nextIndex == 0)
            InitiateSpawnIdling(ref patrolLoopsCompleted);
        else
            currentPatrolIndex = nextIndex;
    }

    private void InitiateSpawnIdling(ref int patrolLoopsCompleted)
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
    #endregion
}