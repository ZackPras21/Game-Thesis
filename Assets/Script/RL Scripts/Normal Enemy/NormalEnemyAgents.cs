using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine.AI;

public class NormalEnemyAgent : Agent
{
    // ───── PUBLIC INSPECTOR FIELDS ────────────────────────────────────────────
    [Header("Movement / Combat Parameters")]
    public float moveSpeed = 3f;
    public float turnSpeed = 120f;
    public float attackRange = 2f;
    public float attackDamage = 10f;
    public float detectDistance = 10f;   // Unique per‐prefab in Inspector
    public float maxHealth = 100f;

    [Header("References")]
    public Transform[] patrolPoints;       // Array of waypoints to patrol between
    public Transform playerTransform;      // Reference to the player
    public LayerMask obstacleLayerMask;    // LayerMask used for obstacle‐raycasts
    public Animator animator;              // Assign the Animator component here

    
    public enum EnemyType { Creep, Humanoid, Bull }

    [Header("Enemy Type (HP≤20% Behavior)")]
    public EnemyType enemyType = EnemyType.Creep;

    // ───── PRIVATE STATE FIELDS ───────────────────────────────────────────────
    private NavMeshAgent navAgent;
    private int currentPatrolIndex = 0;

    // High‐level mode flags:
    private bool isPatrolling = false;
    private bool isChasing   = false;
    private bool isAttacking = false;
    private bool isDetecting = false;
    private bool isIdle      = false;
    private bool isDead      = false;

    // Health + detection timer:
    private float currentHealth;
    private float detectionTimer = 0f;
    private const float detectPhaseDuration = 0.5f; // seconds spent in “Detect” state

    // Reward tracking / bookkeeping:
    private bool hasEverSeenPlayer = false;
    private float prevDistanceToPlayer = float.PositiveInfinity;

    // ───── INITIALIZE AGENT ───────────────────────────────────────────────────
    public override void Initialize()
    {
        navAgent = GetComponent<NavMeshAgent>();
        navAgent.speed = moveSpeed;
        navAgent.angularSpeed = turnSpeed;
        navAgent.stoppingDistance = 0.1f;
        navAgent.autoBraking = false;

        // Reset health & mode flags:
        currentHealth     = maxHealth;
        isDead            = false;
        hasEverSeenPlayer = false;

        isPatrolling    = false;
        isChasing       = false;
        isAttacking     = false;
        isDetecting     = false;
        isIdle          = true;        // Start in Idle
        detectionTimer  = 0f;

        // Reset NavMeshAgent so it truly idles for the first frame:
        navAgent.enabled  = true;
        navAgent.Warp(transform.position);
        navAgent.velocity = Vector3.zero;
        navAgent.isStopped = true;

        // Force Idle animation on spawn:
        if (animator != null)
        {
            animator.SetBool("isWalking", false);
            animator.SetBool("isAttacking", false);
            animator.ResetTrigger("getHit");
            animator.SetBool("isDead", false);
        }
        NormalEnemyActions.DoIdle(navAgent);
    }

    // ───── COLLECT OBSERVATIONS ────────────────────────────────────────────────
    public override void CollectObservations(VectorSensor sensor)
    {
        // Build a temporary state object (so we can reuse any logic you had before)
        NormalEnemyState s = new NormalEnemyState(
            agentTransform: transform,
            currentHealth: currentHealth,
            maxHealth: maxHealth,
            playerTransform: playerTransform,
            detectDistance: detectDistance,
            obstacleMask: obstacleLayerMask
        );

        // Override the state’s mode‐flags with our local booleans:
        s.IsPatrolling = isPatrolling;
        s.IsChasing    = isChasing;
        s.IsDetecting  = isDetecting;
        s.IsAttacking  = isAttacking;
        s.IsDead       = isDead;
        s.IsIdle       = isIdle;

        // 1) Agent’s X,Z position (normalized by arena size ≈50 units):
        sensor.AddObservation(s.AgentPosition.x / 50f);
        sensor.AddObservation(s.AgentPosition.z / 50f);

        // 2) Health fraction (0..1):
        sensor.AddObservation(s.HealthFraction);

        // 3) Direction to next patrol point (X,Z):
        sensor.AddObservation(s.NextPatrolPointDir.x);
        sensor.AddObservation(s.NextPatrolPointDir.z);

        // 4) Player’s X,Z position (normalized):
        sensor.AddObservation(s.PlayerPosition.x / 50f);
        sensor.AddObservation(s.PlayerPosition.z / 50f);

        // 5) Can see player? (1 or 0)
        sensor.AddObservation(s.CanSeePlayer ? 1f : 0f);

        // 6) Distance to nearest obstacle (clamped 0..10, then normalized):
        float obsDistNorm = Mathf.Clamp(s.DistToNearestObstacle, 0f, 10f) / 10f;
        sensor.AddObservation(obsDistNorm);

        // 7) Mode flags (six booleans: Patrol, Chase, Detect, Attack, Dead, Idle):
        sensor.AddObservation(s.IsPatrolling ? 1f : 0f);
        sensor.AddObservation(s.IsChasing    ? 1f : 0f);
        sensor.AddObservation(s.IsDetecting  ? 1f : 0f);
        sensor.AddObservation(s.IsAttacking  ? 1f : 0f);
        sensor.AddObservation(s.IsDead       ? 1f : 0f);
        sensor.AddObservation(s.IsIdle       ? 1f : 0f);

        // 8) Current distance to player & delta‐distance since last step:
        float currDist = Vector3.Distance(transform.position, playerTransform.position);
        sensor.AddObservation(currDist / 50f);

        float distDelta = float.IsInfinity(prevDistanceToPlayer)
            ? 0f
            : currDist - prevDistanceToPlayer;
        sensor.AddObservation(Mathf.Clamp(distDelta, -50f, +50f) / 50f);
        prevDistanceToPlayer = currDist;
    }

    // ───── ONACTIONRECEIVED ────────────────────────────────────────────────────
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        // 1) If already dead → play death routine and exit:
        if (isDead)
        {
            NormalEnemyActions.DoDead(transform, navAgent);
            return;
        }

        // 2) HP ≤ 20% → Type‐specific behavior:
        float healthFraction = currentHealth / maxHealth;
        if (healthFraction <= 0.2f && !isDead)
        {
            switch (enemyType)
            {
                case EnemyType.Creep:
                {
                    // Creep: run behind player (180° away) to attack from behind
                    Vector3 toPlayer    = (playerTransform.position - transform.position).normalized;
                    Vector3 behindPoint = playerTransform.position - toPlayer * attackRange;
                    navAgent.isStopped = false;
                    navAgent.SetDestination(behindPoint);

                    isPatrolling   = false;
                    isChasing      = false;
                    isDetecting    = false;
                    isAttacking    = false;
                    isIdle         = false;
                    return;
                }
                case EnemyType.Humanoid:
                {
                    // Humanoid: if other enemies still alive → idle; else chase
                    bool othersAlive = false;
                    foreach (var other in FindObjectsOfType<NormalEnemyAgent>())
                    {
                        if (other != this && other.currentHealth > 0f)
                        {
                            othersAlive = true;
                            break;
                        }
                    }
                    if (othersAlive)
                    {
                        isPatrolling = false;
                        isChasing    = false;
                        isDetecting  = false;
                        isAttacking  = false;
                        isIdle       = true;
                        // half‐penalty for waiting
                        AddReward(-0.005f);
                        return;
                    }
                    // no allies → fall through to normal chase logic
                    break;
                }
                case EnemyType.Bull:
                {
                    // Bull: if any allies remain → flee; else chase
                    bool alliesRemain = false;
                    foreach (var other in FindObjectsOfType<NormalEnemyAgent>())
                    {
                        if (other != this && other.currentHealth > 0f)
                        {
                            alliesRemain = true;
                            break;
                        }
                    }
                    if (alliesRemain)
                    {
                        // Flee straight away from player up to detectDistance
                        Vector3 fleeDir   = (transform.position - playerTransform.position).normalized;
                        Vector3 fleePoint = transform.position + fleeDir * detectDistance;
                        navAgent.isStopped = false;
                        navAgent.SetDestination(fleePoint);

                        isPatrolling = false;
                        isChasing    = false;
                        isDetecting  = false;
                        isAttacking  = false;
                        isIdle       = false;
                        return;
                    }
                    // otherwise, no allies → fall through to chase
                    break;
                }
            }
        }

        // 3) Check line‐of‐sight (raycast) and distance to player:
        Vector3 dirToPlayer = (playerTransform.position - transform.position).normalized;
        bool canSee = false;
        if (Physics.Raycast(
                origin: transform.position + Vector3.up * 0.5f,
                direction: dirToPlayer,
                hitInfo: out RaycastHit hit,
                maxDistance: detectDistance,
                layerMask: ~0 // Raycast against all layers; we'll check if hit == player
            ))
        {
            if (hit.transform == playerTransform)
                canSee = true;
        }
        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // 4) Enter “Detect” as soon as the player becomes visible:
        if (canSee && !isDetecting && !isChasing && !isAttacking && !isDead)
        {
            isDetecting    = true;
            isChasing      = false;
            isPatrolling   = false;
            isAttacking    = false;
            isIdle         = false;
            detectionTimer = 0f;

            if (animator != null)
            {
                // Optional: trigger a Detect/Alert animation, if one exists.
                // animator.SetTrigger("detectTrigger");
            }
            // Reward for spotting the player:
            AddReward(+0.01f);
        }

        // 5) If currently in Detect state → wait detectPhaseDuration, then begin chase:
        if (isDetecting)
        {
            detectionTimer += Time.deltaTime;
            NormalEnemyActions.DoDetect(); // Currently a no‐op, but you can add visual cues here.

            if (detectionTimer >= detectPhaseDuration)
            {
                isDetecting = false;
                isChasing   = true;
                isIdle      = false;
                if (animator != null)
                {
                    animator.SetBool("isWalking", true);
                }
            }
            return; // Skip the rest of logic this FixedUpdate
        }

        // 6) If can see && outside attack range → Chase:
        if (canSee && distToPlayer > attackRange && !isChasing && !isAttacking && !isDead)
        {
            isChasing    = true;
            isPatrolling = false;
            isAttacking  = false;
            isIdle       = false;

            if (!hasEverSeenPlayer)
            {
                AddReward(+0.01f); // extra reward for first detection
                hasEverSeenPlayer = true;
            }
            if (animator != null)
            {
                animator.SetBool("isWalking", true);
            }
        }

        // 7) If already in Attacking state:
        if (isAttacking)
        {
            NormalEnemyActions.DoAttack(navAgent);

            // If still within attackRange, deal damage:
            if (distToPlayer <= attackRange + 0.1f)
            {
                RL_Player player = playerTransform.GetComponent<RL_Player>();
                if (player != null)
                {
                    player.TakeDamage(attackDamage);
                    AddReward(+0.05f); // reward for landing an attack

                    if (player.CurrentHealth <= 0f)
                    {
                        AddReward(+0.2f); // big reward for killing the player
                        EndEpisode();
                        return;
                    }
                }
            }
        }
        // 8) If can see && within attack range → switch to Attacking:
        else if (canSee && distToPlayer <= attackRange)
        {
            isAttacking   = true;
            isChasing     = false;
            isPatrolling  = false;
            isIdle        = false;

            if (animator != null)
            {
                animator.SetBool("isAttacking", true);
            }
            navAgent.isStopped = true;
        }
        // 9) If in Chasing state → continue moving toward player:
        else if (isChasing)
        {
            NormalEnemyActions.DoChase(navAgent, playerTransform);
        }
        // 10) If cannot see & not attacking/dead → Patrol:
        else if (!canSee && !isChasing && !isAttacking && !isDead)
        {
            isPatrolling  = true;
            isChasing     = false;
            isAttacking   = false;
            isIdle        = false;

            NormalEnemyActions.DoPatrol(navAgent, patrolPoints, ref currentPatrolIndex);
            if (animator != null)
            {
                animator.SetBool("isWalking", true);
            }
            AddReward(+0.001f); // small reward for patrolling step
        }
        // 11) Otherwise → Idle (fallback):
        else
        {
            isIdle        = true;
            isPatrolling  = false;
            isChasing     = false;
            isDetecting   = false;
            isAttacking   = false;

            if (animator != null)
            {
                animator.SetBool("isWalking", false);
            }
            NormalEnemyActions.DoIdle(navAgent);
            AddReward(-0.005f); // slight penalty for standing idle
        }

        // 12) Collision penalty (raycast forward into obstacle):
        {
            if (Physics.Raycast(
                    origin: transform.position + Vector3.up * 0.5f,
                    direction: transform.forward,
                    out RaycastHit hitObs,
                    maxDistance: 0.5f,
                    layerMask: obstacleLayerMask
                ))
            {
                AddReward(-0.01f);
            }
        }

        // 13) Apply continuous‐action movement (if not Attacking or Dead):
        if (!isAttacking && !isDead)
        {
            float moveX  = actionBuffers.ContinuousActions[0]; // -1..+1
            float moveZ  = actionBuffers.ContinuousActions[1]; // -1..+1
            float rotateY = actionBuffers.ContinuousActions[2]; // -1..+1

            NormalEnemyActions.ApplyMovement(
                agentTransform: transform,
                navAgent: navAgent,
                moveX: moveX,
                moveZ: moveZ,
                rotateY: rotateY,
                moveSpeed: moveSpeed,
                turnSpeed: turnSpeed
            );
        }
    }

    // ───── HEURISTIC (for human testing) ──────────────────────────────────────
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var cont = actionsOut.ContinuousActions;
        // Forward/Backward:
        cont[1] = Input.GetAxis("Vertical");   // W/S or Up/Down arrow
        // Strafe Left/Right:
        cont[0] = Input.GetAxis("Horizontal"); // A/D or Left/Right arrow
        // Rotate Left/Right:
        float rot = 0f;
        if (Input.GetKey(KeyCode.Q)) rot = -1f;
        if (Input.GetKey(KeyCode.E)) rot = +1f;
        cont[2] = rot;
    }

    // ───── TAKE DAMAGE ────────────────────────────────────────────────────────
    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        AddReward(-0.02f); // penalty for being hit

        if (animator != null)
        {
            animator.SetTrigger("getHit");
        }

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            isDead = true;

            // Clear all other flags:
            isIdle       = false;
            isPatrolling = false;
            isChasing    = false;
            isDetecting  = false;
            isAttacking  = false;

            AddReward(-0.2f); // punishment for dying

            if (animator != null)
            {
                animator.SetBool("isDead", true);
            }

            EndEpisode();
        }
    }

    // ───── ON EPISODE BEGIN ───────────────────────────────────────────────────
    public override void OnEpisodeBegin()
    {
        // Reset position, health, flags, etc.
        Initialize();
    }
}
