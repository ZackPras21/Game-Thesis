using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEngine.AI;


[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(RayPerceptionSensorComponent3D))]
public class NormalEnemyAgent : Agent
{
    // ───── PUBLIC INSPECTOR FIELDS ───────────────
    [Header("Movement / Combat Parameters")]
    public float moveSpeed = 3f;
    public float turnSpeed = 120f;
    public float attackRange = 2f;
    public float detectThreshold = 0.5f; 
    // (If RayPerceptionSensor returns a normalized distance < detectThreshold for "Player" tag, we say “I see the player”.)

    [Header("Health / Damage")]
    public float maxHealth = 100f;
    public float attackDamage = 10f;
    public float HP_FleeThreshold = 0.2f; 
    // (If current health / maxHealth < HP_FleeThreshold, we switch to “Flee” or “LowHP” policy.)

    [Header("Rewards Config")]
    public NormalEnemyRewards rewardConfig;   // (Reference your existing reward config ScriptableObject)
    public NormalEnemyStates stateConfig;     // (Reference your existing state machine/enum)

    [Header("References")]
    public Animator animator;
    public NavMeshAgent navAgent;

    // ───── PRIVATE FIELDS ─────────────────────────
    private float currentHealth;
    private Transform playerTransform;
    private bool hasEverSeenPlayer = false;
    private float prevDistanceToPlayer = Mathf.Infinity;
    private Vector3 lastPosition;
    private int stepsSinceLastMove = 0;
    private float timeSinceLastMove = 0f;

    // Ray sensor component reference (set automatically in Initialize())
    private RayPerceptionSensorComponent3D raySensor;

    // States (driven by sensor observations + health checks)
    private bool isPatrolling = true;
    private bool isDetecting = false;
    private bool isChasing = false;
    private bool isAttacking = false;
    private bool isFleeing = false;  // e.g. if HP < threshold and no allies
    private bool playerVisible = false;

    // Cached reward‐state references
    private float detectPhaseTimer = 0f;
    private const float detectPhaseDuration = 0.5f;

    // Patrol state
    [Header("Patrol Settings")]
    [Tooltip("Tag name for patrol points in the scene")]
    public string patrolPointTag = "PatrolPoint";
    private Transform[] patrolPoints;
    private int currentPatrolIndex = 0;
    private int patrolLoopsCompleted = 0;
    
    [Header("Debug Visualization")]
    public bool showDebugInfo = true;
    public Vector2 debugTextOffset = new Vector2(10, 10);
    public Color debugTextColor = Color.white;
    public int debugFontSize = 14;
    private float cumulativeReward = 0f;
    private int episodeSteps = 0;
    private int successfulKills = 0;

    // ───── UNITY / ML‑AGENTS CALLBACKS ────────────────

    public override void Initialize()
    {
        // 1) Grab required components
        navAgent = GetComponent<NavMeshAgent>();
        raySensor = GetComponent<RayPerceptionSensorComponent3D>();
        // Enable debug rays in editor
        #if UNITY_EDITOR
        // Debug visualization removed due to API changes
        #endif

        navAgent.speed = moveSpeed;
        navAgent.angularSpeed = turnSpeed;
        navAgent.stoppingDistance = 0.1f;

        // 2) Assign initial health
        currentHealth = maxHealth;

        // 3) Find the Player in scene (assuming tag = "Player")
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerTransform = p.transform;

        // 4) Find patrol points by tag
        GameObject[] patrolObjs = GameObject.FindGameObjectsWithTag(patrolPointTag);
        patrolPoints = new Transform[patrolObjs.Length];
        for (int i = 0; i < patrolObjs.Length; i++)
        {
            patrolPoints[i] = patrolObjs[i].transform;
        }

        // 5) Initialize "lastPosition" for stuck detection
        lastPosition = transform.position;
        stepsSinceLastMove = 0;
        timeSinceLastMove = 0f;
    }
    
    // Unity's OnGUI method is called every frame when rendering GUI elements.
    void OnGUI()
    {
        if (showDebugInfo)
        {
            // Set up the label style with the color, font size, etc.
            GUIStyle labelStyle = new GUIStyle();
            labelStyle.fontSize = debugFontSize;
            labelStyle.normal.textColor = debugTextColor;

            // Display the agent state and other debug info at the top-left corner
            string debugText = $"State: {(isPatrolling ? "Patrol" : isDetecting ? "Detect" : isChasing ? "Chase" : isAttacking ? "Attack" : "Idle")}\n" +
            $"Steps: {episodeSteps} | Reward: {cumulativeReward:F2} | Patrol Loops: {patrolLoopsCompleted}";
            
            // Draw the text in the GUI
            GUI.Label(new Rect(debugTextOffset.x, debugTextOffset.y, 300, 100), debugText, labelStyle);
        }
    }

    public override void OnEpisodeBegin()
    {
        // Reset metrics
        cumulativeReward = 0f;
        episodeSteps = 0;
        patrolLoopsCompleted = 0;

        // Reset agent's position, health, states, etc.
        currentHealth = maxHealth;
        isPatrolling = true;
        isDetecting = false;
        isChasing = false;
        isAttacking = false;
        isFleeing = false;
        hasEverSeenPlayer = false;
        prevDistanceToPlayer = Mathf.Infinity;

        // Reset NavMeshAgent
        navAgent.Warp(/* your spawn position logic here */ transform.position);

        // Reset timing flags
        stepsSinceLastMove = 0;
        timeSinceLastMove = 0f;
        lastPosition = transform.position;

        // (If you have a training‐arena spawner, call its ResetArena())
        RL_TrainingTargetSpawner targetSpawner = Object.FindFirstObjectByType<RL_TrainingTargetSpawner>();
        if (targetSpawner != null)
        {
            targetSpawner.ResetArena();
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // 1) **Inject RayPerception observations automatically**  
        // 2) Add scalar observations (e.g. currentHealth, normalized by maxHealth)
        sensor.AddObservation(currentHealth / maxHealth);

        // 3) If desired, also provide normalized distance to player & agent’s current velocity
        if (playerTransform != null)
        {
            float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            sensor.AddObservation(distToPlayer / 20f); // normalize by some max expected distance (e.g. 20 units)
        }
        else
        {
            sensor.AddObservation(1f); // “Player is far-away or null”
        }

        Vector3 localVelocity = transform.InverseTransformDirection(navAgent.velocity);
        sensor.AddObservation(localVelocity.x / moveSpeed);
        sensor.AddObservation(localVelocity.z / moveSpeed);
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        // 0) **Stuck Detection** (similar to your existing code)
        float distanceSinceLast = Vector3.Distance(transform.position, lastPosition);
        if (distanceSinceLast < 0.01f)
        {
            stepsSinceLastMove++;
            timeSinceLastMove += Time.deltaTime;
        }
        else
        {
            stepsSinceLastMove = 0;
            timeSinceLastMove = 0f;
            lastPosition = transform.position;
        }

        if (rewardConfig.CheckIfStuck(lastPosition, transform.position, stepsSinceLastMove, timeSinceLastMove))
        {
            // Penalize & end episode if agent is stuck
            AddReward(rewardConfig.NoMovementPunishment);
            EndEpisode();
            return;
        }

        // 1) Read ActionBuffers (discrete or continuous) → convert to movement commands
        //     (Example: assume continuous actions [moveX, moveZ, turnY, attackFlag])
        float moveX = Mathf.Clamp(actionBuffers.ContinuousActions[0], -1f, 1f);
        float moveZ = Mathf.Clamp(actionBuffers.ContinuousActions[1], -1f, 1f);
        float turnY = Mathf.Clamp(actionBuffers.ContinuousActions[2], -1f, 1f);
        float attackSignal = Mathf.Clamp(actionBuffers.ContinuousActions[3], 0f, 1f);

        // 1.a) Apply turning
        transform.Rotate(Vector3.up, turnY * turnSpeed * Time.deltaTime);

        // 1.b) Apply movement (via NavMesh or direct CharacterController)
        Vector3 forward = transform.forward * moveZ * moveSpeed * Time.deltaTime;
        Vector3 strafe = transform.right * moveX * moveSpeed * Time.deltaTime;
        Vector3 desiredMove = forward + strafe;

        navAgent.isStopped = false;
        navAgent.Move(desiredMove);
        if (playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist <= detectThreshold * raySensor.RayLength)
            {
                // Further confirm line‐of‐sight with a single raycast to the player’s center
                RaycastHit hit;
                Vector3 dir = (playerTransform.position - transform.position).normalized;
                if (Physics.Raycast(transform.position + Vector3.up * 0.5f, dir, out hit, raySensor.RayLength))
                {
                    if (hit.collider != null && hit.collider.CompareTag("Player"))
                    {
                        playerVisible = true;
                    }
                }
            }
        }

        // 2.b) Health‐based logic: if HP < HP_FleeThreshold, enter “Flee” if no allies
        if (currentHealth / maxHealth < HP_FleeThreshold)
        {
            bool alliesAlive = false;
            foreach (var other in Object.FindObjectsByType<NormalEnemyAgent>(FindObjectsSortMode.None))
            {
                if (other != this && other.currentHealth > 0f)
                {
                    alliesAlive = true;
                    break;
                }
            }
            if (!alliesAlive)
            {
                // Flee from player
                if (playerTransform != null)
                {
                    Vector3 fleeDir = (transform.position - playerTransform.position).normalized;
                    Vector3 fleeTarget = transform.position + fleeDir * detectThreshold; 
                    navAgent.SetDestination(fleeTarget);
                }
                isFleeing = true;
                isChasing = false;
                isDetecting = false;
                isPatrolling = false;
                isAttacking = false;
                return;
            }
        }

        // 2.c) If you see the player AND you have sufficient HP, go into chase/attack states
        if (playerVisible)
        {
            hasEverSeenPlayer = true;
            float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);

            // If just detected and not already chasing, enter “Detecting” for a short delay
            if (!isChasing && !isDetecting && distToPlayer <= raySensor.RayLength)
            {
                isDetecting = true;
                detectPhaseTimer = 0f;

                // Reward for initial “see”
                AddReward(rewardConfig.DetectPlayerReward);
            }

            // If verifying “Detect” phase
            if (isDetecting)
            {
                detectPhaseTimer += Time.deltaTime;
                // You can animate a “detecting” pose here (e.g. play animation)
                if (detectPhaseTimer >= detectPhaseDuration)
                {
                    isDetecting = false;
                    isChasing = true;
                    // Reward for finishing detection
                    AddReward(rewardConfig.FinishDetectReward);
                }
                // When in detect state, do not move.  
                navAgent.isStopped = true;
                return;
            }

            // Now “chase” the player
            if (isChasing)
            {
                // If within attack range, switch to “Attack”
                if (distToPlayer <= attackRange)
                {
                    isAttacking = true;
                    isChasing = false;
                    // Stop NavMesh movement
                    navAgent.isStopped = true;
                }
                else
                {
                    // Continue chasing via NavMesh
                    navAgent.isStopped = false;
                    navAgent.SetDestination(playerTransform.position);
                }
                // Reward for moving closer
                float deltaDist = prevDistanceToPlayer - distToPlayer;
                AddReward(rewardConfig.ChaseStepReward * Mathf.Clamp01(deltaDist)); 
                prevDistanceToPlayer = distToPlayer;
                return;
            }

            // “Attack” State
            if (isAttacking)
            {
                // Face the player
                Vector3 lookDir = (playerTransform.position - transform.position).normalized;
                Quaternion lookRot = Quaternion.LookRotation(lookDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * turnSpeed);

                // Fire attack if “attackSignal” from actionBuffer is > 0.5
                if (attackSignal > 0.5f && distToPlayer <= attackRange + 0.1f)
                {
                    // Do your damage to player
                    RL_Player player = playerTransform.GetComponent<RL_Player>();
                    if (player != null)
                    {
                        player.DamagePlayer(attackDamage);
                        AddReward(rewardConfig.AttackPlayerReward);
                        if (player.CurrentHealth <= 0f)
                        {
                            successfulKills++;
                            AddReward(rewardConfig.KillPlayerReward);
                            
                            // End episode if killed at least one player
                            if (successfulKills >= 1)
                            {
                                EndEpisode();
                                return;
                            }
                        }
                    }
                }

                // If player moves out of range, go back to chasing
                if (distToPlayer > attackRange + 0.1f)
                {
                    isAttacking = false;
                    isChasing = true;
                }
                return;
            }
        }

        // 2.d) If you’ve seen the player but currently can’t see them (lost line-of-sight), decide fallback
        if (hasEverSeenPlayer && !playerVisible)
        {
            // Example: ⇒ go back to patrolling or stand idle for a moment
            isChasing = false;
            isDetecting = false;
            isAttacking = false;
            isPatrolling = true;
        }

        // 2.e) Default “Patrolling” behavior
        if (!hasEverSeenPlayer)
        {
            isPatrolling = true;
            isChasing = false;
            isDetecting = false;
            isAttacking = false;
        }

        // 3) **Patrol Step**: incentivize movement if agent is not doing anything else
        if (isPatrolling && patrolPoints.Length > 0)
        {
            NormalEnemyActions.DoPatrol(navAgent, patrolPoints, ref currentPatrolIndex, ref patrolLoopsCompleted, animator);
            AddReward(rewardConfig.PatrolStepReward * Time.deltaTime);
            
            // End episode if completed 2 full patrol loops
            if (patrolLoopsCompleted >= 2)
            {
                AddReward(rewardConfig.PatrolCompleteReward);
                EndEpisode();
                return;
            }
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        
        // Optional: define human input for debugging (e.g. arrow keys to move, space to attack)
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions.Clear();
        continuousActions[0] = Input.GetAxis("Horizontal");
        continuousActions[1] = Input.GetAxis("Vertical");
        continuousActions[2] = Input.GetKey(KeyCode.Q) ? -1f : (Input.GetKey(KeyCode.E) ? 1f : 0f);
        continuousActions[3] = Input.GetKey(KeyCode.Space) ? 1f : 0f;
    }

    public void OnEpisodeEnd()
    {
        // Reset to default state
        currentHealth = maxHealth;
        isPatrolling = true;
        isDetecting = false;
        isChasing = false;
        isAttacking = false;
        isFleeing = false;
        hasEverSeenPlayer = false;
        prevDistanceToPlayer = Mathf.Infinity;
        currentPatrolIndex = 0;
        
        // Reset NavMeshAgent
        navAgent.Warp(transform.position);
        navAgent.velocity = Vector3.zero;
        navAgent.isStopped = true;
    }

    // ───── PUBLIC “Damage” API ────────────────
    public void TakeDamage(float amount)
    {
        currentHealth = Mathf.Max(currentHealth - amount, 0f);
        if (currentHealth <= 0f)
        {
            // Play death animation, spawn loot, etc.
            if (animator != null)
            {
                Debug.Log("Setting death animation");
                animator.SetTrigger("isDead");
            }
            AddReward(rewardConfig.DiedByPlayerPunishment);
            
            // Find spawner and respawn player
            RL_TrainingEnemySpawner spawner = FindObjectOfType<RL_TrainingEnemySpawner>();
            if (spawner != null && playerTransform != null)
            {
                spawner.RespawnPlayer(playerTransform.gameObject);
            }
            
            EndEpisode();
        }
        else
        {
            // Play “hit” feedback
            if (animator != null)
            {
                animator.SetTrigger("getHit");
            }
            AddReward(rewardConfig.HitByPlayerPunishment);
        }
    }

    public float CurrentHealth => currentHealth;
}
