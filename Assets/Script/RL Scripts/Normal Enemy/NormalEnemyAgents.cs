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

    [Header("Stats")]
    public float maxHealth = 100f;
    private float currentHealth;

    [Header("Movement Settings")]
    public float moveSpeed = 3.5f;
    public float turnSpeed = 120f;
    public float detectDistance = 10f;              // Range to detect player (line‐of‐sight)
    public float attackRange = 2.0f;             // Distance at which we “attack” the player

    [Header("Rewards")]
    public NormalEnemyRewards rewardCalculator;

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
        // Finds all active GameObjects tagged “PatrolPoint” in the Scene
        GameObject[] pts = GameObject.FindGameObjectsWithTag("Patrol Point");
        patrolPoints = new Transform[pts.Length];
        for(int i = 0; i < pts.Length; i++)
        {
            patrolPoints[i] = pts[i].transform;
        }
    }

    public override void Initialize()
    {
        navAgent = GetComponent<NavMeshAgent>();
        navAgent.speed = moveSpeed;
        navAgent.angularSpeed = turnSpeed;
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

        // 1) Reset health
        currentHealth = maxHealth;
        isDead = false;
        hasEverSeenPlayer = false;

        // 2) Reset position: choose a random patrol point
        if (patrolPoints.Length > 0)
        {
            int idx = Random.Range(0, patrolPoints.Length);
            transform.position = patrolPoints[idx].position + Vector3.up * 0.5f;
        }

        // 3) Reset movement
        navAgent.enabled = true;
        navAgent.Warp(transform.position);
        navAgent.velocity = Vector3.zero;
        navAgent.isStopped = false;
        currentPatrolIndex = 0;

        // 4) Reset “stuck” trackers
        lastPosition = transform.position;
        timeSinceLastMove = 0f;
        stepsSinceLastMove = 0;

        // 5) Reset distance cache
        prevDistanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Build the NormalEnemyState each FixedUpdate:
        NormalEnemyState s = new NormalEnemyState(
            agentTransform: transform,
            currentHealth: currentHealth,
            maxHealth: maxHealth,
            playerTransform: playerTransform,
            detectDistance: detectDistance,
            obstacleMask: obstacleLayerMask
        );

        // Overwrite the “mode flags” with our internal booleans:
        s.IsPatrolling = isPatrolling;
        s.IsChasing = isChasing;
        s.IsAttacking = isAttacking;
        s.IsDead = isDead;

        // 1) Agent position (x, z)
        sensor.AddObservation(s.AgentPosition.x / 50f);   // normalize by arena size (~50m)
        sensor.AddObservation(s.AgentPosition.z / 50f);

        // 2) Health fraction
        sensor.AddObservation(s.HealthFraction);

        // 3) Player position (x, z)
        sensor.AddObservation(s.PlayerPosition.x / 50f);
        sensor.AddObservation(s.PlayerPosition.z / 50f);

        // 4) Can see player (0 or 1)
        sensor.AddObservation(s.CanSeePlayer ? 1f : 0f);

        // 5) Distance to nearest obstacle in front (clamped at 10m then normalized)
        float obsDistNorm = Mathf.Clamp(s.DistToNearestObstacle, 0f, 10f) / 10f;
        sensor.AddObservation(obsDistNorm);

        // 6) Four mode‐flags (Patrol, Chase, Attack, Dead)
        sensor.AddObservation(s.IsPatrolling  ? 1f : 0f);
        sensor.AddObservation(s.IsChasing     ? 1f : 0f);
        sensor.AddObservation(s.IsAttacking   ? 1f : 0f);
        sensor.AddObservation(s.IsDead        ? 1f : 0f);

        // 7) Previous distance to player (to know if we’re getting closer or farther)
        float currDist = Vector3.Distance(transform.position, playerTransform.position);
        sensor.AddObservation(currDist / 50f);           // normalized current distance
        float distDelta;
        if (float.IsInfinity(prevDistanceToPlayer))
        {
            distDelta = 0f;
        }
        else
        {
            distDelta = currDist - prevDistanceToPlayer;
        }
        // Normalize the delta by 50 (or whatever your arena scale is):
        float distDeltaClamped = Mathf.Clamp(distDelta, -50f, +50f) / 50f;
        sensor.AddObservation(distDeltaClamped);
        
        prevDistanceToPlayer = currDist;
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        if (isDead)
        {
            // If already dead, do nothing (no movement).
            NormalEnemyActions.DoDead(transform, navAgent);
            return;
        }

        // ========== 1) Read actions: 3 continuous values ∈ [–1, +1]
        float moveX   = Mathf.Clamp(actionBuffers.ContinuousActions[0], -1f, +1f);
        float moveZ   = Mathf.Clamp(actionBuffers.ContinuousActions[1], -1f, +1f);
        float rotateY = Mathf.Clamp(actionBuffers.ContinuousActions[2], -1f, +1f);

        // ========== 2) Determine high‐level mode based on observations:
        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        bool  canSee       = false;
        // Re‐compute line‐of‐sight (we could cache it in CollectObservations if desired).
        {
            RaycastHit hit;
            if (Physics.Raycast(
                origin: transform.position + Vector3.up * 0.5f,
                direction: (playerTransform.position - transform.position).normalized,
                out hit,
                detectDistance,
                obstacleLayerMask
            ))
            {
                if (hit.transform == playerTransform)
                    canSee = true;
            }
        }

        // → If agent health ≤ 0 → Dead:
        if (currentHealth <= 0f)
        {
            isDead = true;
            isPatrolling = false;
            isChasing = false;
            isAttacking  = false;
            isDead = true;
            NormalEnemyActions.DoDead(transform, navAgent);
            AddReward(rewardCalculator.LostFightPenalty); // “Died by player” penalty
            EndEpisode();
            return;
        }

        // → If we detect the player, switch to “Chase” (unless in Attack range).
        if (canSee && distToPlayer > attackRange)
        {
            isChasing = true;
            isPatrolling = false;
            isAttacking = false;
            // Reward for “detecting the player” the very first frame we see them:
            if (!hasEverSeenPlayer)
            {
                AddReward(rewardCalculator.DetectPlayerReward);
                hasEverSeenPlayer = true;
            }
        }
        // → If within “attackRange,” switch to Attack:
        else if (canSee && distToPlayer <= attackRange)
        {
            isAttacking = true;
            isChasing = false;
            isPatrolling = false;
        }
        // → Else: we’re in “Patrol” or “Idle” if move is zero.
        else
        {
            isPatrolling = true;
            isChasing = false;
            isAttacking = false;
        }

        // ========== 3) Low‐level movement / rotation:
        if (isAttacking)
        {
            // Stop moving & attempt an attack
            NormalEnemyActions.DoAttack(navAgent);
        }
        else if (isChasing)
        {
            // Always “move towards” the player, ignoring our moveX/moveZ, but allow rotation:
            Vector3 dir = (playerTransform.position - transform.position).normalized;
            navAgent.SetDestination(playerTransform.position);

            // Grant a small “approach” reward if we got closer to the player this frame:
            float newDist = distToPlayer;
            if (newDist < prevDistanceToPlayer)
            {
                AddReward(rewardCalculator.ApproachPlayerReward);
            }
            else
            {
                AddReward(rewardCalculator.StayFarFromPlayerPenalty);
            }
        }
        else if (isPatrolling)
        {
            // Use our “ApplyMovement” for patrolling, so the network chooses how to move.
            NormalEnemyActions.ApplyMovement(
                transform, navAgent,
                moveX, moveZ, rotateY,
                moveSpeed, turnSpeed
            );
            // Grant a tiny reward for each nonzero movement step while patrolling:
            if (new Vector2(moveX, moveZ).sqrMagnitude > 0.01f)
            {
                AddReward(rewardCalculator.PatrolStepReward);
            }
            else
            {
                // If we stand idle for > 50 steps, punish slightly:
                AddReward(rewardCalculator.IdlePenalty);
            }
        }
        else
        {
            // If we’re “idle” (i.e. saw nothing, choice was effectively “0,0,0”), we do nothing:
            NormalEnemyActions.DoIdle(navAgent);
            AddReward(rewardCalculator.IdlePenalty * 0.5f); // small penalty for standing in place
        }

        // ========== 4) If attacking, check if we actually “hit” the player:
        if (isAttacking)
        {
            if (distToPlayer <= attackRange)
            {
                AddReward(rewardCalculator.AttackPlayerReward);

                // If this attack kills the player → very large reward:
                var player = playerTransform.GetComponent<RL_Player>();
                if (player.CurrentHealth <= 0f)
                {
                    // 1) Grant the “killed player” reward
                    AddReward(rewardCalculator.KilledPlayerReward);

                    // 2) Instead of destroying the player, just disable its Collider + Movement
                    var playerGO = playerTransform.gameObject;
                    var playerCollider = playerGO.GetComponent<Collider>();
                    if (playerCollider != null) playerCollider.enabled = false;

                    var playerRb = playerGO.GetComponent<Rigidbody>();
                    if (playerRb != null) playerRb.isKinematic = true; // freeze it in place

                    // Optionally, swap its team‐layer so no further detection raycasts “see” it:
                    playerGO.layer = LayerMask.NameToLayer("Ignore Raycast");

                    // 3) Now end the episode for this enemy.
                    EndEpisode();
                    return;
                }

            }
            else
            {
                // Missed → punish:
                AddReward(rewardCalculator.AttackMissedPenalty);
            }
        }

        // ========== 5) Check for collisions with walls (bump into obstacle):
        {
            RaycastHit hit;
            if (Physics.Raycast(
                origin: transform.position + Vector3.up * 0.5f,
                direction: transform.forward,
                out hit,
                0.5f,
                obstacleLayerMask
            ))
            {
                AddReward(rewardCalculator.ObstaclePenalty);
            }
        }

        // ========== 6) Check if “stuck” → if so, end episode with a penalty:
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
                // If stuck → end episode immediately with a big negative:
                AddReward(rewardCalculator.NoMovementPenalty);
                EndEpisode();
                return;
            }
        }

        // ========== 7) Update prevDistanceToPlayer for next frame:
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
        if (Input.GetKey(KeyCode.E)) ry = +1f;
        if (Input.GetKey(KeyCode.Q)) ry = -1f;

        cont[0] = mx;
        cont[1] = mz;
        cont[2] = ry;
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        AddReward(rewardCalculator.NoMovementPenalty * 2f); // small penalty for taking damage
        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            isDead = true;
            AddReward(rewardCalculator.DiedByPlayerPunishment);
            EndEpisode();
        }
    }
}
