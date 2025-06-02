using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

[RequireComponent(typeof(NavMeshAgent))]
public class NormalEnemyAgent : Agent
{
    [Header("References")]
    public Transform playerTransform;           // The “Player” Transform (tagged “Player”)
    private Transform[] patrolPoints;
    public LayerMask obstacleLayerMask;         // Walls/obstacles for raycasts

    [Header("Animator Controller (optional)")]
    // If you want to assign/swapping the controller at runtime, assign it in Inspector:
    public RuntimeAnimatorController enemyController;

    [Header("Stats")]
    public float maxHealth = 100f;
    private float currentHealth;
    public float attackDamage = 10f;            // Damage dealt to the player on a successful hit

    [Header("Movement Settings")]
    public float moveSpeed = 3.5f;
    public float turnSpeed = 120f;
    public float detectDistance = 10f;          // Range to detect player (line‑of‑sight)
    public float attackRange = 2.0f;            // Distance at which we “attack” the player

    [Header("Rewards")]
    public NormalEnemyRewards rewardCalculator;

    // Animator for playing animations:
    private Animator animator;

    // Internal state:
    private NavMeshAgent navAgent;
    private int currentPatrolIndex = 0;
    private Vector3 lastPosition;
    private float timeSinceLastMove = 0f;
    private int stepsSinceLastMove = 0;
    private bool hasEverSeenPlayer = false;

    // Mode flags:
    private bool isPatrolling = false;
    private bool isChasing = false;
    private bool isAttacking = false;
    private bool isDead = false;

    // Cache for relative distances:
    private float prevDistanceToPlayer = Mathf.Infinity;

    void Awake()
    {
        // Build the array of patrol points if you have them set up as children of this GameObject:
        Transform[] pts = GetComponentsInChildren<Transform>();
        patrolPoints = new Transform[pts.Length - 1];
        int idx = 0;
        foreach (var t in pts)
        {
            if (t != this.transform)
            {
                patrolPoints[idx++] = t;
            }
        }
    }

    public override void Initialize()
    {
        navAgent = GetComponent<NavMeshAgent>();
        navAgent.speed = moveSpeed;
        navAgent.angularSpeed = turnSpeed;

        // 1) Grab the Animator component
         animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogWarning("No Animator component found on NormalEnemyAgent: " + name);
        }
        else
        {
            if (enemyController != null)
            {
                animator.runtimeAnimatorController = enemyController;
            }
            else
            {
                if (animator.runtimeAnimatorController == null)
                {
                    Debug.LogWarning("Animator has no Controller assigned on " + name +
                                     ". Either assign in Inspector or set enemyController in this script.");
                }
            }
        }

        currentHealth = maxHealth;
        lastPosition = transform.position;
        timeSinceLastMove = 0f;
        stepsSinceLastMove = 0;
    }

    public override void OnEpisodeBegin()
    {
        if (!Academy.Instance.IsCommunicatorOn)
        {
            return;
        }

        // Reset health
        currentHealth = maxHealth;
        isDead = false;
        hasEverSeenPlayer = false;

        navAgent.enabled = true;
        navAgent.Warp(transform.position);
        navAgent.velocity = Vector3.zero;
        navAgent.isStopped = false;
        currentPatrolIndex = 0;

        // Reset “stuck” trackers
        lastPosition = transform.position;
        timeSinceLastMove = 0f;
        stepsSinceLastMove = 0;

        // Reset distance cache
        prevDistanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // Reset Animator parameters
        if (animator != null)
        {
            animator.SetBool("isWalking", false);
            animator.SetBool("isAttacking", false);
            animator.ResetTrigger("getHit");
            animator.SetBool("isDead", false);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        NormalEnemyState s = new NormalEnemyState(
            agentTransform: transform,
            currentHealth: currentHealth,
            maxHealth: maxHealth,
            playerTransform: playerTransform,
            detectDistance: detectDistance,
            obstacleMask: obstacleLayerMask
        );

        s.IsPatrolling = isPatrolling;
        s.IsChasing = isChasing;
        s.IsAttacking = isAttacking;
        s.IsDead = isDead;

        // [snip: same observation code as before…]
        // 1) Agent pos
        sensor.AddObservation(s.AgentPosition.x / 50f);
        sensor.AddObservation(s.AgentPosition.z / 50f);
        // 2) Health fraction
        sensor.AddObservation(s.HealthFraction);
        // 3) Player pos
        sensor.AddObservation(s.PlayerPosition.x / 50f);
        sensor.AddObservation(s.PlayerPosition.z / 50f);
        // 4) Can see player
        sensor.AddObservation(s.CanSeePlayer ? 1f : 0f);
        // 5) Dir to nearest obstacle normalized
        float obsDistNorm = Mathf.Clamp(s.DistToNearestObstacle, 0f, 10f) / 10f;
        sensor.AddObservation(obsDistNorm);
        // 6) Mode flags
        sensor.AddObservation(s.IsPatrolling  ? 1f : 0f);
        sensor.AddObservation(s.IsChasing     ? 1f : 0f);
        sensor.AddObservation(s.IsAttacking   ? 1f : 0f);
        sensor.AddObservation(s.IsDead        ? 1f : 0f);
        // 7) Relative distance delta
        float currDist = Vector3.Distance(transform.position, playerTransform.position);
        sensor.AddObservation(currDist / 50f);
        float distDelta = float.IsInfinity(prevDistanceToPlayer)
            ? 0f
            : currDist - prevDistanceToPlayer;
        sensor.AddObservation(Mathf.Clamp(distDelta, -50f, +50f) / 50f);
        prevDistanceToPlayer = currDist;
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        if (isDead)
        {
            NormalEnemyActions.DoDead(transform, navAgent);
            return;
        }

        float moveX   = Mathf.Clamp(actionBuffers.ContinuousActions[0], -1f, +1f);
        float moveZ   = Mathf.Clamp(actionBuffers.ContinuousActions[1], -1f, +1f);
        float rotateY = Mathf.Clamp(actionBuffers.ContinuousActions[2], -1f, +1f);

        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        bool  canSee       = false;

        // Recompute line‑of‑sight
        {
            RaycastHit getHit;
            if (Physics.Raycast(
                origin: transform.position + Vector3.up * 0.5f,
                direction: (playerTransform.position - transform.position).normalized,
                out getHit,
                detectDistance,
                obstacleLayerMask
            ))
            {
                if (getHit.transform == playerTransform)
                    canSee = true;
            }
        }

        // → If health ≤ 0 → go to Death
        if (currentHealth <= 0f)
        {
            isDead = true;
            isPatrolling = false;
            isChasing = false;
            isAttacking = false;
            if (animator != null)
            {
                animator.SetBool("isDead", true);
            }
            NormalEnemyActions.DoDead(transform, navAgent);
            AddReward(rewardCalculator.LostFightPenalty);
            EndEpisode();
            return;
        }

        // → Decide between Chase, Attack, or Patrol
        if (canSee && distToPlayer > attackRange)
        {
            isChasing = true;
            isPatrolling = false;
            isAttacking = false;

            if (!hasEverSeenPlayer)
            {
                AddReward(rewardCalculator.DetectPlayerReward);
                hasEverSeenPlayer = true;
            }

            if (animator != null)
            {
                animator.SetBool("isWalking", true);
                animator.SetBool("isAttacking", false);
                animator.ResetTrigger("getHit");
                animator.SetBool("isDead", false);
            }
        }
        else if (canSee && distToPlayer <= attackRange)
        {
            isAttacking = true;
            isChasing = false;
            isPatrolling = false;

            if (animator != null)
            {
                animator.SetBool("isAttacking", true);
                animator.SetBool("isWalking", false);
                animator.ResetTrigger("getHit");
                animator.SetBool("isDead", false);
            }
        }
        else
        {
            isPatrolling = true;
            isChasing = false;
            isAttacking = false;

            if (animator != null)
            {
                animator.SetBool("isWalking", true);
                animator.SetBool("isAttacking", false);
                animator.ResetTrigger("getHit");
                animator.SetBool("isDead", false);
            }
        }

        // ========== Movement and Attack Logic ==========
        if (isAttacking)
        {
            NormalEnemyActions.DoAttack(navAgent);

            // ——————————————— New: Damage the Player ———————————————
            if (distToPlayer <= attackRange)
            {
                RL_Player player = playerTransform.GetComponent<RL_Player>();
                if (player != null)
                {
                    player.TakeDamage(attackDamage);
                    AddReward(rewardCalculator.AttackPlayerReward);
                    if (player.CurrentHealth <= 0f)
                    {
                        AddReward(rewardCalculator.KilledPlayerReward);

                        Collider playerCollider = playerTransform.GetComponent<Collider>();
                        if (playerCollider != null) playerCollider.enabled = false;

                        Rigidbody playerRb = playerTransform.GetComponent<Rigidbody>();
                        if (playerRb != null) playerRb.isKinematic = true;

                        playerTransform.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");

                        EndEpisode();
                        return;
                    }
                }
            }
            else
            {
                AddReward(rewardCalculator.AttackMissedPenalty);
            }
        }
        else if (isChasing)
        {
            navAgent.SetDestination(playerTransform.position);

            float newDist = distToPlayer;
            if (newDist < prevDistanceToPlayer)
                AddReward(rewardCalculator.ApproachPlayerReward);
            else
                AddReward(rewardCalculator.StayFarFromPlayerPenalty);

            if (animator != null)
            {
                animator.SetBool("isWalking", true);
            }
        }
        else if (isPatrolling)
        {
            NormalEnemyActions.ApplyMovement(
                transform, navAgent,
                moveX, moveZ, rotateY,
                moveSpeed, turnSpeed
            );

            if (new Vector2(moveX, moveZ).sqrMagnitude > 0.01f)
                AddReward(rewardCalculator.PatrolStepReward);
            else
                AddReward(rewardCalculator.IdlePenalty);
        }
        else
        {
            NormalEnemyActions.DoIdle(navAgent);
            AddReward(rewardCalculator.IdlePenalty * 0.5f);
            if (animator != null)
                animator.SetBool("isWalking", false);
        }

        // ========== Collision Penalty ==========
        {
            RaycastHit getHit;
            if (Physics.Raycast(
                origin: transform.position + Vector3.up * 0.5f,
                direction: transform.forward,
                out getHit,
                0.5f,
                obstacleLayerMask
            ))
            {
                AddReward(rewardCalculator.ObstaclePenalty);
            }
        }

        // ========== Stuck Detection ==========
        {
            Vector3 currPos = transform.position;
            float   distMove = Vector3.Distance(lastPosition, currPos);
            if (distMove < 0.01f)
            {
                stepsSinceLastMove++;
                timeSinceLastMove += Time.fixedDeltaTime;
            }
            else
            {
                stepsSinceLastMove = 0;
                timeSinceLastMove = 0f;
                lastPosition = currPos;
            }

            if (rewardCalculator.CheckIfStuck(
                    prevPos: lastPosition,
                    currPos: currPos,
                    stepsSinceLastMove: stepsSinceLastMove,
                    timeSinceLastMove: timeSinceLastMove
                ))
            {
                AddReward(rewardCalculator.NoMovementPenalty);
                EndEpisode();
                return;
            }
        }

        prevDistanceToPlayer = distToPlayer;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> cont = actionsOut.ContinuousActions;
        float mx = 0f;
        float mz = 0f;
        float ry = 0f;

        if (Input.GetKey(KeyCode.W)) mz = +1f;
        if (Input.GetKey(KeyCode.S)) mz = -1f;
        if (Input.GetKey(KeyCode.D)) mx = +1f;
        if (Input.GetKey(KeyCode.A)) mx = -1f;
        if (Input.GetKey(KeyCode.Q)) ry = -1f;
        if (Input.GetKey(KeyCode.E)) ry = +1f;

        cont[0] = mx;
        cont[1] = mz;
        cont[2] = ry;
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        AddReward(rewardCalculator.NoMovementPenalty * 2f);

        if (animator != null)
        {
            animator.SetTrigger("getHit"); // <— changed from “hit” to “getHit”
        }

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            isDead = true;
            AddReward(rewardCalculator.DiedByPlayerPunishment);

            if (animator != null)
            {
                animator.SetBool("isDead", true);
            }

            EndEpisode();
        }
    }
}
