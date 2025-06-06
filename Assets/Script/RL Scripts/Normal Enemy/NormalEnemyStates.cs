using UnityEngine;

public class NormalEnemyStates
{
    // ───── PUBLIC STATE FIELDS ────────────────────────────────────────────────
    public Vector3 AgentPosition;       // Current (x,z) position in world
    public float HealthFraction;        // currentHealth / maxHealth

    public Vector3 PlayerPosition;      // (x,z) of player
    public bool CanSeePlayer;           // True if LOS to player within detectDistance

    public Vector3 NextPatrolPointDir;  // Direction (normalized) to next patrol waypoint
    public float DistToNearestObstacle; // Distance to nearest obstacle ahead (0..10)

    // High‐level modes:
    public bool IsPatrolling;
    public bool IsChasing;
    public bool IsDetecting;
    public bool IsAttacking;
    public bool IsDead;
    public bool IsIdle;

    // ───── CONSTRUCTOR ─────────────────────────────────────────────────────────
    public NormalEnemyStates(
        Transform agentTransform,
        float currentHealth,
        float maxHealth,
        Transform playerTransform,
        float detectDistance,
        LayerMask obstacleMask
    )
    {
        // 1) Agent’s world position:
        AgentPosition = agentTransform.position;

        // 2) Health fraction:
        HealthFraction = currentHealth / maxHealth;

        // 3) Player’s world position:
        PlayerPosition = playerTransform.position;

        // 4) Can see player? (raycast from agent → player)
        Vector3 dirToPlayer = (PlayerPosition - AgentPosition).normalized;
        RaycastHit hit;
        if (Physics.Raycast(
                origin: AgentPosition + Vector3.up * 0.5f,
                direction: dirToPlayer,
                out hit,
                maxDistance: detectDistance
            ))
        {
            CanSeePlayer = (hit.transform == playerTransform);
        }
        else
        {
            CanSeePlayer = false;
        }

        // 5) NextPatrolPointDir: 
        // NOTE: The Agent script overwrote this via CollectObservations, so here we default to zero.
        NextPatrolPointDir = Vector3.zero;

        // 6) Dist to nearest obstacle (spherecast):
        RaycastHit obsHit;
        if (Physics.SphereCast(
                origin: AgentPosition + Vector3.up * 0.5f,
                radius: 0.5f,
                direction: agentTransform.forward,
                out obsHit,
                maxDistance: 10f,
                layerMask: obstacleMask
            ))
        {
            DistToNearestObstacle = obsHit.distance;
        }
        else
        {
            DistToNearestObstacle = 10f;
        }

        // 7) Initialize all booleans to false (will be overridden by CollectObservations):
        IsPatrolling = false;
        IsChasing    = false;
        IsDetecting  = false;
        IsAttacking  = false;
        IsDead       = false;
        IsIdle       = false;
    }
}
