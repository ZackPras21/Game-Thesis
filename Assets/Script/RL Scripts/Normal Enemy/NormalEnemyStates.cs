using UnityEngine;
public class NormalEnemyState
{
    // The agent's own position (x,z) in world space, and its current health fraction (0..1).
    public Vector3 AgentPosition;
    public float   HealthFraction;         // 0..1

    // The player's position (x,z), and whether the agent has a direct line‐of‐sight to the player.
    public Vector3 PlayerPosition;
    public bool    CanSeePlayer;

    // Distance from the agent to the nearest obstacle directly in front (raycast).
    public float DistToNearestObstacle;

    // Current “mode” booleans (only one should be true at a time, except “IsDead” can override).
    public bool IsPatrolling;
    public bool IsChasing;
    public bool IsAttacking;
    public bool IsDead;

    // Constructor – pulls everything from the agent’s MonoBehaviour (requires a reference to the GameObject + Player).
    public NormalEnemyState(Transform agentTransform, float currentHealth, float maxHealth, 
                            Transform playerTransform, float detectDistance, LayerMask obstacleMask)
    {
        // 1) Agent position & health fraction.
        AgentPosition   = agentTransform.position;
        HealthFraction  = Mathf.Clamp01(currentHealth / maxHealth);

        // 2) Player position & line‐of‐sight (raycast from agent → player).
        PlayerPosition = playerTransform.position;
        Vector3 dirToPlayer = (PlayerPosition - AgentPosition).normalized;
        RaycastHit hit;
        if (Physics.Raycast(
            origin: agentTransform.position + Vector3.up * 0.5f,
            direction: dirToPlayer,
            out hit,
            detectDistance,
            obstacleMask    // anything in this mask blocks line‐of‐sight
        ))
        {
            if (hit.transform == playerTransform)
                CanSeePlayer = true;
            else
                CanSeePlayer = false;
        }
        else
        {
            // If raycast didn’t hit anything within detectDistance, we assume "cannot see"
            CanSeePlayer = false;
        }

        // 3) Distance to nearest obstacle in front (raycast forward + small offset).
        DistToNearestObstacle = Mathf.Infinity;
        {
            RaycastHit obstacleHit;
            if (Physics.Raycast(
                origin: agentTransform.position + Vector3.up * 0.5f,
                direction: agentTransform.forward,
                out obstacleHit,
                1.0f,             // only look 1 meter ahead
                obstacleMask
            ))
            {
                DistToNearestObstacle = obstacleHit.distance;
            }
        }

        // 4) Mode‐flags: the agent’s MonoBehaviour should set these externally each FixedUpdate or so.
        //    We’ll assume the MonoBehaviour writes these into some fields before CollectObservations.
        IsPatrolling = false;
        IsChasing    = false;
        IsAttacking  = false;
        IsDead       = false;
    }
}
