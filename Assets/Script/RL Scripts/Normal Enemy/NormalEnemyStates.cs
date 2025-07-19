using UnityEngine;

public class NormalEnemyStates : MonoBehaviour 
{
    #region Properties
    public Vector3 AgentPosition { get; private set; }
    public float HealthFraction { get; private set; }
    public Vector3 PlayerPosition { get; private set; }
    public bool CanSeePlayer { get; private set; }
    public Vector3 NextPatrolPointDirection { get; set; } = Vector3.zero;
    public float DistanceToNearestObstacle { get; private set; }
    public bool IsChasing { get; set; } = false;
    public bool IsDetecting { get; set; } = false;
    public bool IsIdle { get; set; } = false;
    #endregion

    #region State Components
    public PlayerTrackingState PlayerTracking { get; private set; }
    public WaypointNavigationState WaypointNavigation { get; private set; }
    public CombatState CombatState { get; private set; }
    public HealthState HealthState { get; private set; }

    public void SetChasing(bool chasing)
    {
        IsChasing = chasing;
        if (chasing)
        {
            IsIdle = false;
            IsDetecting = false;
        }
    }

    public void SetDetecting(bool detecting)
    {
        IsDetecting = detecting;
        if (detecting)
        {
            IsIdle = false;
        }
    }

    public void SetIdle(bool idle)
    {
        IsIdle = idle;
        if (idle)
        {
            IsChasing = false;
            IsDetecting = false;
        }
    }

    public void UpdateBehavioralStates(bool playerVisible, bool playerInRange, bool playerInAttackRange)
    {
        if (playerInAttackRange)
        {
            SetChasing(false);
            SetDetecting(false);
            SetIdle(false);
        }
        else if (playerVisible && playerInRange)
        {
            SetChasing(true);
        }
        else if (playerVisible)
        {
            SetDetecting(true);
        }
        else
        {
            SetIdle(true);
        }
    }
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