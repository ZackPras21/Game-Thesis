using UnityEngine;
using UnityEngine.AI;

public enum EnemyHighLevelAction
{
    Idle     = 0,
    Patrol   = 1,
    Detect   = 2,
    Chase    = 3,
    Attack   = 4,
    Dead     = 5
}

public static class NormalEnemyActions
{
    public static void ApplyMovement(
        Transform agentTransform,
        NavMeshAgent navAgent,
        float moveX,
        float moveZ,
        float rotateY,
        float moveSpeed,
        float turnSpeed
    )
    {
        // Rotate horizontally:
        agentTransform.Rotate(0f, rotateY * turnSpeed * Time.deltaTime, 0f);

        // Compute desired direction in local space:
        Vector3 localMove = new Vector3(moveX, 0f, moveZ);
        Vector3 worldMove = agentTransform.TransformDirection(localMove).normalized;

        // Let NavMeshAgent handle collisions & path‐smoothing:
        if (moveZ != 0f || moveX != 0f)
        {
            navAgent.isStopped = false;
            navAgent.speed = moveSpeed;
            navAgent.SetDestination(agentTransform.position + worldMove * 0.5f);
        }
        else
        {
            navAgent.velocity = Vector3.zero;
            navAgent.isStopped = true;
        }
    }

    public static void DoIdle(NavMeshAgent navAgent)
    {
        navAgent.velocity = Vector3.zero;
        navAgent.isStopped = true;
    }

    public static void DoPatrol(
        NavMeshAgent navAgent,
        Transform[] patrolPoints,
        ref int currentPatrolIndex
    )
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;

        Vector3 targetPos = patrolPoints[currentPatrolIndex].position;
        navAgent.isStopped = false;
        navAgent.SetDestination(targetPos);

        // If close enough to this waypoint, advance to next:
        if (!navAgent.pathPending && navAgent.remainingDistance <= navAgent.stoppingDistance + 0.1f)
        {
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
        }
    }

    public static void DoDetect()
    {
        // Optionally: play an “alert” animation or sound.
    }

    public static void DoChase(
        NavMeshAgent navAgent,
        Transform playerTransform
    )
    {
        navAgent.isStopped = false;
        navAgent.SetDestination(playerTransform.position);
    }

    public static void DoAttack(NavMeshAgent navAgent)
    {
        navAgent.velocity = Vector3.zero;
        navAgent.isStopped = true;
    }

    public static void DoDead(
        Transform agentTransform,
        NavMeshAgent navAgent
    )
    {
        navAgent.velocity = Vector3.zero;
        navAgent.isStopped = true;
        // Optionally: play ragdoll or sink into ground, etc.
    }
}
