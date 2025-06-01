using UnityEngine;
public enum EnemyHighLevelAction
{
    Idle = 0,
    Patrol,
    Detect,
    Chase,
    Attack,
    Dead
}

public static class NormalEnemyActions
{
    public static void ApplyMovement(
    Transform agentTransform,
    UnityEngine.AI.NavMeshAgent navAgent,
    float moveX,
    float moveZ,
    float rotateY,
    float moveSpeed,
    float turnSpeed
    )
    {
        // 1) Build a local‐space direction vector from the network’s [moveX, moveZ]:
        Vector3 localMove = new Vector3(moveX, 0f, moveZ);
        if (localMove.sqrMagnitude > 1f)
        localMove.Normalize();

        // 2) Convert that to world‐space:
        Vector3 worldDir = agentTransform.TransformDirection(localMove).normalized;

        // 3) Let the NavMeshAgent “step” that way, letting it respect obstacle avoidance:
        navAgent.Move(worldDir * moveSpeed * Time.deltaTime);

        // 4) Rotate only around Y, clamping exclusively to horizontal rotation:
        agentTransform.Rotate(0f, rotateY * turnSpeed * Time.deltaTime, 0f);
    }

    public static void DoIdle(UnityEngine.AI.NavMeshAgent navAgent)
    {
        navAgent.velocity = Vector3.zero;
    }

    public static void DoDead(Transform agentTransform, UnityEngine.AI.NavMeshAgent navAgent)
    {
        navAgent.velocity = Vector3.zero;
        navAgent.isStopped = true;
        // Optionally, you can set a “dead” animation or remove the GameObject after a short delay.
    }

    public static void DoPatrol(
        UnityEngine.AI.NavMeshAgent navAgent,
        Transform[] patrolPoints,
        ref int currentPatrolIndex
    )
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;

        Transform targetPoint = patrolPoints[currentPatrolIndex];
        if (navAgent.remainingDistance <= navAgent.stoppingDistance + 0.1f)
        {
            // Arrived at waypoint → go to next, looping around
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            targetPoint = patrolPoints[currentPatrolIndex];
        }

        navAgent.SetDestination(targetPoint.position);
    }

    public static void DoDetect()
    {
        // No built‐in movement needed; detection logic is handled in the Agent class via raycasts/spheres.
    }

    public static void DoChase(
        UnityEngine.AI.NavMeshAgent navAgent,
        Transform playerTransform
    )
    {
        navAgent.SetDestination(playerTransform.position);
    }
    public static void DoAttack(UnityEngine.AI.NavMeshAgent navAgent)
    {
        navAgent.velocity = Vector3.zero;
        navAgent.isStopped = true;
    }
}
