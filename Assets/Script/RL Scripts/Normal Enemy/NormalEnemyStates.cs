using UnityEngine;

public sealed class NormalEnemyStates
{
    #region Properties
    public Vector3 AgentPosition { get; private set; }
    public float HealthFraction { get; private set; }
    public Vector3 PlayerPosition { get; private set; }
    public bool CanSeePlayer { get; private set; }
    public Vector3 NextPatrolPointDirection { get; set; } = Vector3.zero;
    public float DistanceToNearestObstacle { get; private set; }
    
    public bool IsPatrolling { get; set; } = false;
    public bool IsChasing { get; set; } = false;
    public bool IsDetecting { get; set; } = false;
    public bool IsAttacking { get; set; } = false;
    public bool IsDead { get; set; } = false;
    public bool IsIdle { get; set; } = false;
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
        LayerMask obstacleMask)
    {
        InitializePositions(agentTransform, playerTransform);
        CalculateHealthFraction(currentHealth, maxHealth);
        DeterminePlayerVisibility(agentTransform, detectDistance);
        CalculateObstacleDistance(agentTransform, obstacleMask);
    }

    #region Initialization Methods
    private void InitializePositions(Transform agentTransform, Transform playerTransform)
    {
        AgentPosition = agentTransform.position;
        PlayerPosition = playerTransform.position;
    }

    private void CalculateHealthFraction(float currentHealth, float maxHealth) => 
        HealthFraction = currentHealth / maxHealth;

    private void DeterminePlayerVisibility(Transform agentTransform, float detectDistance)
    {
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
    #endregion
}