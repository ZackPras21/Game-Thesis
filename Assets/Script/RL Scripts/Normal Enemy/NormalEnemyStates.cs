using UnityEngine;

public class NormalEnemyStates
{
    public Vector3 AgentPosition { get; private set; }
    public float HealthFraction { get; private set; }
    public Vector3 PlayerPosition { get; private set; }
    public bool CanSeePlayer { get; private set; }
    public Vector3 NextPatrolPointDirection { get; set; }
    public float DistanceToNearestObstacle { get; private set; }

    public bool IsPatrolling { get; set; }
    public bool IsChasing { get; set; }
    public bool IsDetecting { get; set; }
    public bool IsAttacking { get; set; }
    public bool IsDead { get; set; }
    public bool IsIdle { get; set; }

    private const float RAYCAST_HEIGHT_OFFSET = 0.5f;
    private const float OBSTACLE_DETECTION_RADIUS = 0.5f;
    private const float MAX_OBSTACLE_DETECTION_DISTANCE = 10f;

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
        DeterminePlayerVisibility(agentTransform, playerTransform, detectDistance);
        CalculateObstacleDistance(agentTransform, obstacleMask);
        InitializeStateFlags();
        
        NextPatrolPointDirection = Vector3.zero;
    }

    private void InitializePositions(Transform agentTransform, Transform playerTransform)
    {
        AgentPosition = agentTransform.position;
        PlayerPosition = playerTransform.position;
    }

    private void CalculateHealthFraction(float currentHealth, float maxHealth)
    {
        HealthFraction = currentHealth / maxHealth;
    }

    private void DeterminePlayerVisibility(Transform agentTransform, Transform playerTransform, float detectDistance)
    {
        Vector3 directionToPlayer = (PlayerPosition - AgentPosition).normalized;
        Vector3 raycastOrigin = AgentPosition + Vector3.up * RAYCAST_HEIGHT_OFFSET;

        if (Physics.Raycast(raycastOrigin, directionToPlayer, out RaycastHit hit, detectDistance))
        {
            CanSeePlayer = hit.transform == playerTransform;
        }
        else
        {
            CanSeePlayer = false;
        }
    }

    private void CalculateObstacleDistance(Transform agentTransform, LayerMask obstacleMask)
    {
        Vector3 sphereCastOrigin = AgentPosition + Vector3.up * RAYCAST_HEIGHT_OFFSET;

        if (Physics.SphereCast(
            sphereCastOrigin,
            OBSTACLE_DETECTION_RADIUS,
            agentTransform.forward,
            out RaycastHit obstacleHit,
            MAX_OBSTACLE_DETECTION_DISTANCE,
            obstacleMask))
        {
            DistanceToNearestObstacle = obstacleHit.distance;
        }
        else
        {
            DistanceToNearestObstacle = MAX_OBSTACLE_DETECTION_DISTANCE;
        }
    }

    private void InitializeStateFlags()
    {
        IsPatrolling = false;
        IsChasing = false;
        IsDetecting = false;
        IsAttacking = false;
        IsDead = false;
        IsIdle = false;
    }
}