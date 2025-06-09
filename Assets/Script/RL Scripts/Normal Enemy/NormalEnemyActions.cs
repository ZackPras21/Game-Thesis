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
    private static float patrolIdleTimer = 0f;
    private static bool   isIdlingAtSpawn = false;

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
        ref int currentPatrolIndex,
        ref int patrolLoopsCompleted,
        Animator animator = null
    )

    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;

        // If we are currently in “spawn‐idle” mode, count down:
        if (isIdlingAtSpawn)
        {
            patrolIdleTimer -= Time.deltaTime;
            navAgent.isStopped = true;
            if (animator != null)
            {
                animator.SetBool("isWalking", false);
                animator.SetBool("isIdle", true);
            }
            if (patrolIdleTimer <= 0f)
            {
                // Done idling → leave idle mode and move to next waypoint
                isIdlingAtSpawn = false;
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            }
            return;
        }

        // Normal patrol: go to the “currentPatrolIndex” waypoint
        Vector3 targetPos = patrolPoints[currentPatrolIndex].position;
        navAgent.isStopped = false;
        navAgent.SetDestination(targetPos);
        if (animator != null)
        {
            animator.SetBool("isWalking", true);
            animator.SetBool("isIdle", false);
        }

        // If we are close enough to this waypoint:
        if (!navAgent.pathPending && navAgent.remainingDistance <= navAgent.stoppingDistance + 0.1f)
        {
            // If advancing from the last index back to zero ⇒ “spawn” point reached
            int nextIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            if (nextIndex == 0)
            {
                // We have just completed a full patrol loop
                patrolLoopsCompleted++;
                // We have just arrived at the spawn‐adjacent waypoint.
                // ⇒ Enter idle for 5 seconds before setting currentPatrolIndex = 0 again
                isIdlingAtSpawn = true;
                patrolIdleTimer = 5.0f;
                // Keep currentPatrolIndex = 0 so we actually "wait" here.
                return; // Indicate loop completion
            }
            else
            {
                // Normal wrap‐around
                currentPatrolIndex = nextIndex;
            }
            return;
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
    }
}
