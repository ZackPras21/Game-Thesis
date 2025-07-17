using UnityEngine;

public sealed class NormalEnemyStates
{
    #region Properties
    public Vector3 AgentPosition { get; private set; }
    public float HealthFraction { get; private set; }
    public Vector3 PlayerPosition { get; private set; }
    public bool CanSeePlayer { get; private set; }
    public Vector3 NextPatrolPointDirection { get; set; } = Vector3.zero; //unimplemented
    public float DistanceToNearestObstacle { get; private set; }
    public bool IsChasing { get; set; } = false; //unimplemented
    public bool IsDetecting { get; set; } = false; //unimplemented
    public bool IsIdle { get; set; } = false;
    #endregion

    #region State Components
    public PlayerTrackingState PlayerTracking { get; private set; }
    public WaypointNavigationState WaypointNavigation { get; private set; }
    public CombatState CombatState { get; private set; }
    public HealthState HealthState { get; private set; }
    #endregion

    #region Constants
    private const float RAYCAST_HEIGHT_OFFSET = 0.5f;
    private const float OBSTACLE_DETECTION_RADIUS = 0.5f;
    private const float MAX_OBSTACLE_DETECTION_DISTANCE = 10f;
    #endregion

    public NormalEnemyStates(
        Transform agentTransform,
        float currentHealth,
        float maxHealth,
        Transform playerTransform,
        float detectDistance,
        LayerMask obstacleMask,
        Transform[] waypoints = null,
        float startWaitTime = 4f)
    {
        InitializePositions(agentTransform, playerTransform);
        CalculateHealthFraction(currentHealth, maxHealth);
        DeterminePlayerVisibility(agentTransform, detectDistance);
        CalculateObstacleDistance(agentTransform, obstacleMask);
        InitializeStateComponents(waypoints, startWaitTime);
    }

    #region Initialization Methods
    private void InitializePositions(Transform agentTransform, Transform playerTransform)
    {
        AgentPosition = agentTransform.position;
        PlayerPosition = playerTransform != null ? playerTransform.position : Vector3.zero;
    }

    private void CalculateHealthFraction(float currentHealth, float maxHealth) => 
        HealthFraction = maxHealth > 0 ? currentHealth / maxHealth : 0f;

    private void DeterminePlayerVisibility(Transform agentTransform, float detectDistance)
    {
        if (PlayerPosition == Vector3.zero)
        {
            CanSeePlayer = false;
            return;
        }

        Vector3 directionToPlayer = (PlayerPosition - AgentPosition).normalized;
        Vector3 raycastOrigin = AgentPosition + Vector3.up * RAYCAST_HEIGHT_OFFSET;
        CanSeePlayer = Physics.Raycast(raycastOrigin, directionToPlayer, 
            out RaycastHit hit, detectDistance) && hit.transform.CompareTag("Player");
    }

    private void CalculateObstacleDistance(Transform agentTransform, LayerMask obstacleMask)
    {
        Vector3 sphereCastOrigin = AgentPosition + Vector3.up * RAYCAST_HEIGHT_OFFSET;
        DistanceToNearestObstacle = Physics.SphereCast(
            sphereCastOrigin, OBSTACLE_DETECTION_RADIUS, agentTransform.forward,
            out RaycastHit obstacleHit, MAX_OBSTACLE_DETECTION_DISTANCE, obstacleMask)
            ? obstacleHit.distance
            : MAX_OBSTACLE_DETECTION_DISTANCE;
    }

    private void InitializeStateComponents(Transform[] waypoints, float startWaitTime)
    {
        PlayerTracking = new PlayerTrackingState();
        WaypointNavigation = new WaypointNavigationState(waypoints, startWaitTime);
        CombatState = new CombatState();
        HealthState = new HealthState();
    }
    #endregion

    #region Update Methods
    public void UpdateStates(
        Transform agentTransform,
        float currentHealth,
        float maxHealth,
        Transform playerTransform,
        float detectDistance,
        LayerMask obstacleMask)
    {
        InitializePositions(agentTransform, playerTransform);
        CalculateHealthFraction(currentHealth, maxHealth);
        DeterminePlayerVisibility(agentTransform, detectDistance);
        CalculateObstacleDistance(agentTransform, obstacleMask);
        
        // Update player tracking if player transform exists
        if (playerTransform != null)
        {
            PlayerTracking.SetPlayerPosition(playerTransform.position);
        }
    }
    #endregion
}

#region State Classes
public class PlayerTrackingState
{
    public Vector3 PlayerPosition { get; private set; }
    public Transform PlayerTransform { get; private set; }
    public bool IsInRange { get; private set; }
    public bool IsPlayerAlive { get; private set; } = true;

    public void SetTarget(Transform target)
    {
        PlayerTransform = target;
        IsInRange = true;
        if (target != null)
            PlayerPosition = target.position;
    }

    public void SetInRange(bool inRange) => IsInRange = inRange;
    public void SetPlayerPosition(Vector3 position) => PlayerPosition = position;
    
    public void ClearTarget()
    {
        IsInRange = false;
        PlayerTransform = null;
    }

    public void HandlePlayerDestroyed()
    {
        IsInRange = false;
        IsPlayerAlive = false;
        PlayerTransform = null;
    }
}

public class WaypointNavigationState
{
    private readonly Transform[] waypoints;
    private readonly float startWaitTime;
    private int currentWaypointIndex;

    public bool IsPatrolling { get; private set; }
    public float WaitTime { get; private set; }

    public WaypointNavigationState(Transform[] waypoints, float waitTime)
    {
        this.waypoints = waypoints;
        startWaitTime = waitTime;
        currentWaypointIndex = Random.Range(0, waypoints?.Length ?? 0);
        WaitTime = waitTime;
    }

    public void SetPatrolling(bool patrolling) => IsPatrolling = patrolling;
    public void DecrementWaitTime() => WaitTime -= Time.deltaTime;

    public void MoveToNextWaypoint()
    {
        if (waypoints?.Length > 0)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            IsPatrolling = false;
            WaitTime = startWaitTime;
        }
    }

    public Vector3 GetCurrentWaypointPosition() =>
        waypoints?.Length > 0 && currentWaypointIndex < waypoints.Length
            ? waypoints[currentWaypointIndex].position
            : Vector3.zero;

    public float GetDistanceToCurrentWaypoint(Vector3 currentPosition) =>
        waypoints?.Length > 0 ? Vector3.Distance(currentPosition, GetCurrentWaypointPosition()) : -1f;

    public Vector3 GetDirectionToCurrentWaypoint(Vector3 currentPosition) =>
        waypoints?.Length > 0 ? (GetCurrentWaypointPosition() - currentPosition).normalized : Vector3.zero;
}

public class CombatState
{
    public bool IsAttacking { get; private set; }
    public bool CanAttack { get; private set; } = true;

    public void SetAttacking(bool attacking) => IsAttacking = attacking;
    public void SetCanAttack(bool canAttack) => CanAttack = canAttack;
}

public class HealthState
{
    public bool IsDead { get; private set; }
    public void SetDead(bool dead) => IsDead = dead;
}
#endregion