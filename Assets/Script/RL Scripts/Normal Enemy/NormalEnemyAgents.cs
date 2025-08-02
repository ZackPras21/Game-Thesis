using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine.AI;
using System.Linq;
using static NormalEnemyActions;
using System.Collections.Generic;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent), typeof(RayPerceptionSensorComponent3D), typeof(RL_EnemyController))]
public class NormalEnemyAgent : Agent
{
    #region Serialized Fields
    [Header("References")]
    [SerializeField] private RL_EnemyController rl_EnemyController;
    [SerializeField] private NormalEnemyRewards rewardConfig;
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent navAgent;
    [SerializeField] private LayerMask obstacleMask;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private Vector2 debugTextOffset = new Vector2(10, 10);
    [SerializeField] private Color debugTextColor = Color.white;
    [SerializeField] private int debugFontSize = 14;
    #endregion

    #region Public Properties & Variables
    public static bool TrainingActive = true;
    public float CurrentHealth => rl_EnemyController.enemyHP;
    public float MaxHealth => rl_EnemyController.enemyData.enemyHealth;
    public bool IsDead => rl_EnemyController.healthState.IsDead;
    #endregion

    #region Private Variables
    private PlayerDetection playerDetection;
    private PatrolSystem patrolSystem;
    private AgentMovement agentMovement;
    private DebugDisplay debugDisplay;
    private AnimationClip attackAnimation;
    private float dynamicAttackCooldown = 0.5f;
    private const float HEALTH_NORMALIZATION_FACTOR = 100f;
    private const float ATTACK_COOLDOWN_FALLBACK = 0.5f;
    private Vector3 initialPosition;
    private string currentState = "Idle";
    private string currentAction = "Idle";
    private float previousDistanceToPlayer = float.MaxValue;
    private float lastAttackTime;
    private bool isInitialized = false;
    private bool hasObstaclePunishmentThisFrame = false;
    private Vector3 previousPosition;
    private Vector3 lastMovementInput = Vector3.zero;
    private bool IsAgentKnockedBack() => rl_EnemyController.IsKnockedBack();
    private bool IsAgentFleeing() => rl_EnemyController.IsFleeing();
    private bool ShouldAgentFlee() => rl_EnemyController.IsHealthLow() && playerDetection.IsPlayerAvailable();
    #endregion

    #region Agent Lifecycle
    public override void Initialize()
    {
        rl_EnemyController ??= GetComponent<RL_EnemyController>();
        if (rl_EnemyController == null)
        {
            Debug.LogError("NormalEnemyAgent: RL_EnemyController component is missing!", gameObject);
            enabled = false;
            return;
        }

        navAgent ??= GetComponent<NavMeshAgent>();
        if (navAgent == null)
        {
            Debug.LogError("NormalEnemyAgent: NavMeshAgent component is missing!", gameObject);
            enabled = false;
            return;
        }

        if (!rl_EnemyController.IsInitialized)
        {
            rl_EnemyController.ForceInitialize();
        }

        if (rl_EnemyController.enemyData == null)
        {
            Debug.LogError("NormalEnemyAgent: enemyData is still null after RL_EnemyController initialization!", gameObject);
            enabled = false;
            return;
        }

        InitializeComponents();
        InitializeSystems();
        initialPosition = transform.position;
        previousPosition = transform.position;
        ResetAgentState();
        rl_EnemyController.InitializeHealthBar();
        isInitialized = true;
    }

    public override void OnEpisodeBegin()
    {
        if (!isInitialized)
        {
            Initialize();
            if (!isInitialized) return;
        }

        ResetForNewEpisode();
        RespawnAtRandomLocation();

        ResetTrainingArena();
        rl_EnemyController.ShowHealthBar();

        if (animator != null)
        {
            animator.SetBool("isDead", false);
            animator.SetBool("isAttacking", false);
            animator.SetBool("isWalking", false);
            animator.SetBool("isIdle", true);
            animator.ResetTrigger("getHit");
        }

        GetComponent<Collider>().enabled = true;
        if (navAgent != null)
        {
            navAgent.enabled = true;
            navAgent.isStopped = false;
            navAgent.speed = rl_EnemyController.moveSpeed;
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (!isInitialized || rl_EnemyController == null) return;
        // 7 Observation
        sensor.AddObservation(CurrentHealth / MaxHealth);
        sensor.AddObservation(playerDetection.IsPlayerAvailable()
            ? playerDetection.GetDistanceToPlayer(transform.position) / HEALTH_NORMALIZATION_FACTOR : 0f);
        Vector3 localVelocity = transform.InverseTransformDirection(navAgent.velocity);
        sensor.AddObservation(localVelocity.x / rl_EnemyController.moveSpeed);
        sensor.AddObservation(localVelocity.z / rl_EnemyController.moveSpeed);
        sensor.AddObservation(IsAgentKnockedBack() ? 1f : 0f);
        sensor.AddObservation(IsAgentFleeing() ? 1f : 0f);
        sensor.AddObservation(ShouldAgentFlee() ? 1f : 0f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!isInitialized || rl_EnemyController == null || IsDead || !isActiveAndEnabled) return;

        debugDisplay.IncrementSteps();
        previousPosition = transform.position;

        if (!IsAgentKnockedBack() && !IsAgentFleeing())
        {
            ProcessActions(actions);
        }
        else
        {
            HandleReactiveStates();
        }

        UpdateBehaviorAndRewards();
        CheckEpisodeEnd();
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        var discreteActions = actionsOut.DiscreteActions;

        continuousActions[0] = Input.GetKey(KeyCode.W) ? 1f : 0f; // UP
        continuousActions[1] = Input.GetKey(KeyCode.S) ? 1f : 0f; // DOWN
        continuousActions[2] = Input.GetKey(KeyCode.D) ? 1f : 0f; // RIGHT
        continuousActions[3] = Input.GetKey(KeyCode.A) ? 1f : 0f; // LEFT
        continuousActions[4] = GetRotationInputHeuristic(); // ROTATE

        discreteActions[0] = Input.GetKey(KeyCode.Space) ?
            (int)EnemyHighLevelAction.Attack :
            (int)EnemyHighLevelAction.Idle;
    }

    private void CheckEpisodeEnd()
    {
        if (StepCount >= MaxStep)
        {
            Debug.Log($"{gameObject.name} Max steps reached. Ending episode.");
            EndEpisode();
        }
    }
    #endregion

    #region Initialization & Reset Helpers
    private void InitializeComponents()
    {
        var raySensor = GetComponent<RayPerceptionSensorComponent3D>();
        ConfigureNavMeshAgent();
        playerDetection = new PlayerDetection(raySensor, obstacleMask);
    }

    private void InitializeSystems()
    {
        Transform[] patrolPoints = FindPatrolPoints();
        patrolSystem = new PatrolSystem(patrolPoints);
        agentMovement = new AgentMovement(navAgent, transform, rl_EnemyController.moveSpeed, rl_EnemyController.rotationSpeed, rl_EnemyController.attackRange);
        debugDisplay = new DebugDisplay();

        // Set patrol points on the agent for NavMesh movement
        if (patrolPoints.Length > 0)
        {
            patrolSystem.SetPatrolPoints(patrolPoints);
            Debug.Log($"{gameObject.name} initialized with {patrolPoints.Length} patrol points");
        }
        else
        {
            Debug.LogWarning($"{gameObject.name} has no patrol points assigned!");
        }
    }

    private void ConfigureNavMeshAgent()
    {
        navAgent.speed = rl_EnemyController.moveSpeed;
        navAgent.angularSpeed = rl_EnemyController.rotationSpeed;
        navAgent.stoppingDistance = 0.1f;
        navAgent.isStopped = false;
    }

    private Transform[] FindPatrolPoints()
    {
        // FIXED: First try to get from spawner using proper parent hierarchy
        var spawner = FindFirstObjectByType<RL_TrainingEnemySpawner>();
        if (spawner != null)
        {
            // Check if we have a proper parent in the hierarchy
            Transform currentParent = transform.parent;
            while (currentParent != null)
            {
                // Look for arena parent (usually named like "Arena_X" or similar)
                if (currentParent.name.Contains("Arena") || currentParent.name.Contains("Spawn"))
                {
                    Transform[] arenaPatrolPoints = spawner.GetArenaPatrolPoints(currentParent);
                    if (arenaPatrolPoints != null && arenaPatrolPoints.Length > 0)
                    {
                        Debug.Log($"{gameObject.name} found {arenaPatrolPoints.Length} patrol points from spawner via parent {currentParent.name}");
                        return arenaPatrolPoints;
                    }
                }
                currentParent = currentParent.parent;
            }

            // Try direct parent if no arena parent found
            if (transform.parent != null)
            {
                Transform[] arenaPatrolPoints = spawner.GetArenaPatrolPoints(transform.parent);
                if (arenaPatrolPoints != null && arenaPatrolPoints.Length > 0)
                {
                    Debug.Log($"{gameObject.name} found {arenaPatrolPoints.Length} patrol points from direct parent {transform.parent.name}");
                    return arenaPatrolPoints;
                }
            }
        }

        // FIXED: Fallback with better arena detection
        Transform[] fallbackPoints = FindPatrolPointsByProximityAndArena();
        if (fallbackPoints.Length > 0)
        {
            Debug.Log($"{gameObject.name} found {fallbackPoints.Length} patrol points via fallback method");
            return fallbackPoints;
        }

        Debug.LogError($"{gameObject.name} could not find any patrol points!");
        return new Transform[0];
    }

    private Transform[] FindPatrolPointsByProximityAndArena()
    {
        // Find all patrol points in the scene
        GameObject[] allPatrolPoints = GameObject.FindGameObjectsWithTag("Patrol Point");

        if (allPatrolPoints.Length == 0)
        {
            Debug.LogWarning($"{gameObject.name} no patrol points found in scene with 'Patrol Point' tag");
            return new Transform[0];
        }

        // Group patrol points by their parent (arena)
        Dictionary<Transform, List<Transform>> arenaGroups = new Dictionary<Transform, List<Transform>>();

        foreach (GameObject point in allPatrolPoints)
        {
            Transform arenaParent = point.transform.parent;
            if (arenaParent != null)
            {
                if (!arenaGroups.ContainsKey(arenaParent))
                {
                    arenaGroups[arenaParent] = new List<Transform>();
                }
                arenaGroups[arenaParent].Add(point.transform);
            }
        }

        // Find the closest arena group
        Transform closestArena = null;
        float closestDistance = float.MaxValue;

        foreach (var kvp in arenaGroups)
        {
            Vector3 arenaCenter = Vector3.zero;
            foreach (Transform point in kvp.Value)
            {
                arenaCenter += point.position;
            }
            arenaCenter /= kvp.Value.Count;

            float distance = Vector3.Distance(transform.position, arenaCenter);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestArena = kvp.Key;
            }
        }

        if (closestArena != null && arenaGroups[closestArena].Count > 0)
        {
            var sortedPoints = arenaGroups[closestArena].OrderBy(p => p.name).ToArray();
            Debug.Log($"{gameObject.name} assigned to arena {closestArena.name} with {sortedPoints.Length} patrol points");
            return sortedPoints;
        }

        return new Transform[0];
    }

    private void ResetForNewEpisode()
    {
        ResetAgentState();

        rl_EnemyController.enemyHP = rl_EnemyController.enemyData.enemyHealth;
        rl_EnemyController.healthState.ResetHealthState();
        rl_EnemyController.InitializeHealthBar();

        currentState = "Idle";
        currentAction = "Idle";
        lastAttackTime = Time.fixedTime - dynamicAttackCooldown;

        patrolSystem?.Reset();

        gameObject.SetActive(true);
        GetComponent<Collider>().enabled = true;
        if (navAgent != null)
        {
            navAgent.enabled = true;
            navAgent.isStopped = false;
        }
    }

    private void ResetAgentState()
    {
        playerDetection?.Reset();
        patrolSystem?.Reset();
        debugDisplay?.Reset();
        agentMovement?.Reset();

        if (navAgent != null)
        {
            navAgent.isStopped = false;
            navAgent.enabled = true;
        }
    }

    private void ResetTrainingArena()
    {
        FindFirstObjectByType<RL_TrainingTargetSpawner>()?.ResetArena();
    }
    #endregion

    #region Action Processing
    private void ProcessActions(ActionBuffers actions)
    {
        ProcessMovementActions(actions);
        ProcessAttackAction(actions);
    }

    private void ProcessMovementActions(ActionBuffers actions)
    {
        float vertical = actions.ContinuousActions[0] - actions.ContinuousActions[1];
        float horizontal = actions.ContinuousActions[2] - actions.ContinuousActions[3];
        float rotation = actions.ContinuousActions[4];

        Vector3 movement = new Vector3(horizontal, 0, vertical).normalized;
        lastMovementInput = movement; // Store for animation check
        
        bool isPlayerVisible = playerDetection.IsPlayerVisible;
        bool hasMovementInput = movement.magnitude > 0.1f;

        // Ensure NavMesh is disabled for RL control
        if (navAgent != null && navAgent.enabled)
        {
            navAgent.isStopped = true;
            navAgent.ResetPath();
            navAgent.velocity = Vector3.zero;
        }

        if (isPlayerVisible)
        {
            // Always face player when visible
            Vector3 playerDirection = (playerDetection.GetPlayerPosition() - transform.position).normalized;
            RotateTowardsDirection(playerDirection);
            
            if (hasMovementInput)
            {
                // Process both movement and additional rotation
                agentMovement.ProcessRLMovement(movement, rotation);
            }
            else
            {
                // Auto-chase: move towards player
                agentMovement.ProcessRLMovement(playerDirection, 0f);
            }
            
            currentAction = "Chasing";
        }
        else if (patrolSystem.HasValidPatrolPoints())
        {
            if (!patrolSystem.IsIdlingAtSpawn())
            {
                Vector3 patrolDirection = patrolSystem.GetDirectionToCurrentTarget(transform.position);
                
                if (hasMovementInput)
                {
                    // Blend RL input with patrol direction and apply rotation
                    Vector3 blendedMovement = (movement * 0.7f + patrolDirection * 0.3f).normalized;
                    agentMovement.ProcessRLMovement(blendedMovement, rotation);
                    
                    // Face movement direction
                    if (blendedMovement.magnitude > 0.1f)
                    {
                        RotateTowardsDirection(blendedMovement);
                    }
                }
                else
                {
                    // Pure patrol movement
                    agentMovement.ProcessRLMovement(patrolDirection, 0f);
                    RotateTowardsDirection(patrolDirection);
                }
                
                currentAction = "Patroling";
            }
            else
            {
                // Stop movement during idle but allow rotation
                agentMovement.ProcessRLMovement(Vector3.zero, rotation);
                currentAction = "Idling";
            }
        }
        else
        {
            // Free movement with rotation
            agentMovement.ProcessRLMovement(movement, rotation);
            
            // Rotate based on movement direction or rotation input
            if (hasMovementInput)
            {
                RotateTowardsDirection(movement);
            }
            else if (Mathf.Abs(rotation) > 0.1f)
            {
                // Apply rotation input directly
                transform.Rotate(0, rotation * rl_EnemyController.rotationSpeed * Time.deltaTime, 0);
            }
            
            currentAction = hasMovementInput ? "Moving" : "Idle";
        }
    }

    private void RotateTowardsDirection(Vector3 direction)
    {
        if (direction.magnitude < 0.1f) return;
        
        Vector3 lookDirection = direction;
        lookDirection.y = 0; // Keep rotation horizontal
        
        if (lookDirection.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 
                rl_EnemyController.rotationSpeed * Time.deltaTime);
        }
    }

    private void ProcessAttackAction(ActionBuffers actions)
    {
        bool shouldAttack = actions.DiscreteActions[0] == (int)EnemyHighLevelAction.Attack;
        bool playerInRange = IsPlayerInAttackRange();
        bool canAttack = Time.fixedTime - lastAttackTime >= dynamicAttackCooldown;

        if (playerInRange)
            agentMovement.FaceTarget(playerDetection.GetPlayerPosition());
        if (playerInRange && shouldAttack && canAttack)
        {
            ExecuteAttack();
        }
        else if (playerInRange)
        {
            currentAction = "Chasing";
            rewardConfig.AddApproachPlayerReward(this, Time.deltaTime);
        }

        UpdateAnimationStates(playerInRange, canAttack);
        AdjustMovementSpeed(playerInRange);
    }

    private void ExecuteAttack()
    {
        if (animator != null && attackAnimation == null)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName("Attack"))
            {
                attackAnimation = animator.GetCurrentAnimatorClipInfo(0)[0].clip;
                dynamicAttackCooldown = attackAnimation.length;
            }
            else
            {
                dynamicAttackCooldown = ATTACK_COOLDOWN_FALLBACK;
            }
        }

        lastAttackTime = Time.fixedTime;
        rewardConfig.AddAttackReward(this);

        if (currentAction == "Chasing")
        {
            rewardConfig.AddChasePlayerReward(this);
        }

        rl_EnemyController.AgentAttack();
        currentState = "Attacking";
        currentAction = "Attacking";
    }

    private void UpdateAnimationStates(bool playerInRange, bool canAttack)
    {
        if (animator == null || IsDead || rl_EnemyController == null) return;

        // Get current movement state BEFORE setting animations
        bool isActuallyMoving = IsAgentActuallyMoving();
        bool shouldAttackAnim = playerInRange && canAttack && rl_EnemyController.combatState.IsAttacking;

        // Priority-based animation state handling
        if (IsAgentKnockedBack())
        {
            SetAnimationState(isIdle: true, isWalking: false, isAttacking: false);
            currentState = "Knocked Back";
            currentAction = "Recovering";
            return;
        }

        if (IsAgentFleeing())
        {
            SetAnimationState(isIdle: false, isWalking: true, isAttacking: false);
            animator.speed = 1.5f; // Faster animation for fleeing
            currentState = "Fleeing";
            currentAction = "Fleeing";
            return;
        }

        if (shouldAttackAnim)
        {
            SetAnimationState(isIdle: false, isWalking: false, isAttacking: true);
            animator.speed = 1f;
            currentState = "Attacking";
            currentAction = "Attacking";
            return;
        }

        // Movement-based animation states
        if (isActuallyMoving)
        {
            SetAnimationState(isIdle: false, isWalking: true, isAttacking: false);
            
            // Adjust animation speed based on movement input rather than actual speed
            float speedRatio = lastMovementInput.magnitude > 0.1f ? 
                Mathf.Clamp(lastMovementInput.magnitude * 1.2f, 0.8f, 1.5f) : 1f;
            animator.speed = speedRatio;
            
            // Update state based on context
            if (playerInRange)
            {
                currentState = "Chasing";
                currentAction = "Chasing";
            }
            else
            {
                currentState = "Patrolling";
                currentAction = "Patroling";
            }
        }
        else
        {
            SetAnimationState(isIdle: true, isWalking: false, isAttacking: false);
            animator.speed = 1f;
            
            if (patrolSystem.IsIdlingAtSpawn())
            {
                currentState = $"Idling at {patrolSystem.GetCurrentPatrolPointName()}";
                currentAction = "Idling";
            }
            else
            {
                currentState = "Idle";
                currentAction = "Idle";
            }
        }
    }
    
    private void SetAnimationState(bool isIdle, bool isWalking, bool isAttacking)
    {
        animator.SetBool("isIdle", isIdle);
        animator.SetBool("isWalking", isWalking);
        animator.SetBool("isAttacking", isAttacking);
        
        // Ensure proper animation triggers
        if (isAttacking && !animator.GetCurrentAnimatorStateInfo(0).IsName("Attack"))
        {
            animator.Play("Attack", 0, 0f);
        }
    }

    private bool IsAgentActuallyMoving()
    {
        // Check movement input from RL actions
        bool hasMovementInput = false;
        if (lastMovementInput.magnitude > 0.1f) // Store movement input as class variable
        {
            hasMovementInput = true;
        }
        
        // Check position delta (most reliable for RL-controlled movement)
        Vector3 currentPos = transform.position;
        float positionDelta = Vector3.Distance(currentPos, previousPosition);
        bool hasPositionChanged = positionDelta > 0.02f * Time.deltaTime; // Adjust threshold based on deltaTime
        
        // Check rigidbody velocity (for physics-based movement)
        Rigidbody rb = GetComponent<Rigidbody>();
        bool hasPhysicsVelocity = rb != null && rb.linearVelocity.magnitude > 0.1f;
        
        // Movement is detected if we have input AND (position change OR physics velocity)
        return hasMovementInput && (hasPositionChanged || hasPhysicsVelocity);
    }

    private float CalculateSpeedRatio()
    {
        // Use movement input magnitude as primary indicator
        if (lastMovementInput.magnitude > 0.1f)
        {
            return Mathf.Clamp(lastMovementInput.magnitude, 0.5f, 1.2f);
        }
        
        // Fallback to velocity-based calculation
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null && rb.linearVelocity.magnitude > 0.01f)
        {
            return Mathf.Clamp(rb.linearVelocity.magnitude / rl_EnemyController.moveSpeed, 0.5f, 1.2f);
        }
        
        return 1f;
    }

    private void HandleReactiveStates()
    {
        if (IsAgentKnockedBack())
        {
            currentState = "Knocked Back";
            currentAction = "Recovering";

            if (navAgent != null && navAgent.enabled)
            {
                navAgent.isStopped = true;
            }
        }
        else if (IsAgentFleeing())
        {
            currentState = "Fleeing";
            currentAction = "Fleeing";

            if (navAgent != null && navAgent.enabled)
            {
                navAgent.isStopped = false;

                if (playerDetection.IsPlayerAvailable())
                {
                    Vector3 fleeDirection = (transform.position - playerDetection.GetPlayerPosition()).normalized;
                    Vector3 fleeTarget = transform.position + fleeDirection * 10f;

                    if (UnityEngine.AI.NavMesh.SamplePosition(fleeTarget, out UnityEngine.AI.NavMeshHit hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        navAgent.SetDestination(hit.position);
                        navAgent.speed = rl_EnemyController.moveSpeed * 1.5f;
                    }
                }
            }
        }
    }

    private void RespawnAtRandomLocation()
    {
        var patrolPoints = patrolSystem.GetPatrolPoints();
        if (patrolPoints.Length == 0)
        {
            Debug.LogWarning($"{gameObject.name} has no patrol points for respawning!");
            return;
        }

        int randomIndex = Random.Range(0, patrolPoints.Length);
        Vector3 respawnPosition = patrolPoints[randomIndex].position;

        // FIXED: Minimal random offset
        Vector2 randomOffset = Random.insideUnitCircle * 0.3f;
        respawnPosition += new Vector3(randomOffset.x, 0, randomOffset.y);

        // FIXED: Ensure valid NavMesh position
        if (UnityEngine.AI.NavMesh.SamplePosition(respawnPosition, out UnityEngine.AI.NavMeshHit hit, 1f, UnityEngine.AI.NavMesh.AllAreas))
        {
            respawnPosition = hit.position;
        }

        // FIXED: Clean agent repositioning
        Vector3 originalScale = transform.localScale;
        
        if (navAgent != null)
        {
            navAgent.enabled = false;
        }

        transform.position = respawnPosition;
        transform.localScale = originalScale;

        if (navAgent != null)
        {
            navAgent.enabled = true;
            if (navAgent.isOnNavMesh)
            {
                navAgent.Warp(respawnPosition);
            }
        }

        // FIXED: Reset patrol to the respawn point
        patrolSystem.ResetToSpecificPoint(randomIndex);
        
        Debug.Log($"{gameObject.name} respawned at {patrolPoints[randomIndex].name} (Point {randomIndex})");
    }

    private void AdjustMovementSpeed(bool playerInRange)
    {
        navAgent.speed = playerInRange
            ? rl_EnemyController.moveSpeed * 0.2f
            : rl_EnemyController.moveSpeed;
    }
    #endregion

    #region Reward & Behavior Updates
    private void UpdateBehaviorAndRewards()
    {
        UpdateDetectionAndBehavior();
        ProcessRewards(Time.deltaTime);
        debugDisplay.UpdateCumulativeReward(GetCumulativeReward());
    }

    private void ProcessRewards(float deltaTime)
    {
        if (IsAgentFleeing())
        {
            if (ShouldAgentFlee())
            {
                rewardConfig.AddFleeReward(this, deltaTime);
            }
            else
            {
                rewardConfig.AddFleeingPunishment(this, deltaTime);
            }
            return;
        }

        // FIXED: More aggressive obstacle punishment with proper detection
        if (IsNearObstacle() || IsStuckAgainstWall())
        {
            if (!hasObstaclePunishmentThisFrame)
            {
                rewardConfig.AddObstaclePunishment(this, deltaTime);
                // ADDITIONAL: Stack punishment for persistent wall hugging
                if (IsStuckAgainstWall())
                {
                    rewardConfig.AddObstaclePunishment(this, deltaTime); // Double punishment
                }
                hasObstaclePunishmentThisFrame = true;
            }
        }
        else
        {
            hasObstaclePunishmentThisFrame = false;
        }

        // Rest of the method remains the same...
        if (currentAction == "Chasing")
        {
            rewardConfig.AddChaseStepReward(this, deltaTime);
            ProcessChaseRewards(deltaTime);
        }
        else if (currentAction == "Patroling")
        {
            rewardConfig.AddPatrolStepReward(this, deltaTime);
        }
        else if (currentAction == "Idling")
        {
            if (!patrolSystem.IsIdlingAtSpawn() && navAgent.velocity.magnitude < 0.1f)
            {
                rewardConfig.AddIdlePunishment(this, deltaTime * 0.5f);
            }
        }

        ProcessPlayerVisibilityRewards(deltaTime);
        ProcessDistanceRewards(deltaTime);
    }


    private bool IsNearObstacle()
    {
        // Multi-directional obstacle detection
        Vector3[] directions = {
            transform.forward,
            transform.right,
            -transform.right,
            (transform.forward + transform.right).normalized,
            (transform.forward - transform.right).normalized
        };

        foreach (Vector3 dir in directions)
        {
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, dir, 1.5f, obstacleMask))
            {
                return true;
            }
        }

        return Physics.CheckSphere(transform.position, 1.0f, obstacleMask);
    }

    private bool IsStuckAgainstWall()
    {
        float positionDelta = Vector3.Distance(transform.position, previousPosition);
        bool hasMovementInput = navAgent != null && navAgent.velocity.magnitude > 0.1f;
        
        // Update previous position for next frame
        previousPosition = transform.position;
        
        // If trying to move but not actually moving much = stuck
        return hasMovementInput && positionDelta < 0.05f * Time.deltaTime;
    }

    private void ProcessChaseRewards(float deltaTime)
    {
        if (playerDetection.IsPlayerAvailable())
        {
            float currentDistance = playerDetection.GetDistanceToPlayer(transform.position);
            if (currentDistance < previousDistanceToPlayer)
                rewardConfig.AddApproachPlayerReward(this, Time.deltaTime);
            previousDistanceToPlayer = currentDistance;
        }
    }

    private void ProcessPlayerVisibilityRewards(float deltaTime)
    {
        if (playerDetection.IsPlayerVisible && !currentAction.Contains("Chasing"))
            rewardConfig.AddDoesntChasePlayerPunishment(this, Time.deltaTime);
    }

    private void ProcessDistanceRewards(float deltaTime)
    {
        if (playerDetection.IsPlayerAvailable() &&
            playerDetection.GetDistanceToPlayer(transform.position) > 5f)
            rewardConfig.AddStayFarFromPlayerPunishment(this, Time.deltaTime);
    }

    private void UpdateDetectionAndBehavior()
    {
        playerDetection.UpdatePlayerDetection(transform.position);

        if (playerDetection.IsPlayerVisible)
        {
            rewardConfig.AddDetectionReward(this, Time.deltaTime);
            currentState = "Detecting";
            currentAction = "Chasing";
        }
        else
        {
            UpdatePatrolBehavior();
        }

        if (!playerDetection.IsPlayerVisible)
        {
            patrolSystem.UpdatePatrol(transform.position, navAgent, rewardConfig, this, Time.deltaTime);
        }
    }

    private void UpdatePatrolBehavior()
    {
        if (!patrolSystem.HasValidPatrolPoints())
        {
            currentAction = "Idle";
            currentState = "No Patrol Points";
            return;
        }

        if (patrolSystem.IsIdlingAtSpawn())
        {
            currentAction = "Idling";
            currentState = $"Idling at {patrolSystem.GetCurrentPatrolPointName()} ({patrolSystem.GetIdleTimeRemaining():F1}s remaining)";
            
            bool idleComplete = patrolSystem.UpdateIdleTimer();
            if (idleComplete)
            {
                currentAction = "Patroling";
            }
            return;
        }

        // FIXED: Patrol movement logic
        Vector3 currentTarget = patrolSystem.GetCurrentPatrolTarget();
        float distanceToTarget = Vector3.Distance(transform.position, currentTarget);
        
        if (distanceToTarget < 2f)
        {
            bool completedLoop = patrolSystem.AdvanceToNextWaypoint();
            
            if (completedLoop)
            {
                rewardConfig.AddPatrolReward(this);
                Debug.Log($"{gameObject.name} completed full patrol loop {patrolSystem.PatrolLoopsCompleted}");
                
                // FIXED: End episode after completing required loops
                if (patrolSystem.PatrolLoopsCompleted >= 2)
                {
                    rewardConfig.AddPatrolReward(this);
                    Debug.Log($"{gameObject.name} completed 2 full patrol loops. Ending episode.");
                    EndEpisode();
                    return;
                }
            }
            
            currentState = patrolSystem.IsIdlingAtSpawn() ? 
                $"Starting idle at {patrolSystem.GetCurrentPatrolPointName()}" : 
                $"Reached {patrolSystem.GetCurrentPatrolPointName()}, moving to next";
        }
        else
        {
            currentAction = "Patroling";
            currentState = $"Moving to {patrolSystem.GetCurrentPatrolPointName()} (dist: {distanceToTarget:F1}m)";
        }
    }

    public void HandleEnemyDeath()
    {
        rewardConfig.AddDeathPunishment(this);
        currentState = "Dead";
        currentAction = "Dead";

        // FIX: Stop all movement when dead
        if (navAgent != null && navAgent.enabled)
        {
            navAgent.isStopped = true;
            navAgent.ResetPath();
        }

        Debug.Log($"{gameObject.name} died. Ending episode.");
        EndEpisode();
    }

    public void HandleDamage()
    {
        rewardConfig.AddDamagePunishment(this);
        currentState = "Attacking";
        currentAction = "Attacking";
    }

    public void HandleKillPlayer()
    {
        rewardConfig.AddKillPlayerReward(this);
    }
    #endregion  

    #region Utility & Debug
    private float GetRotationInputHeuristic()
    {
        if (Input.GetKey(KeyCode.Q)) return -1f;
        if (Input.GetKey(KeyCode.E)) return 1f;
        return 0f;
    }

    private bool IsObstacleCollision(Collision collision) =>
        ((1 << collision.gameObject.layer) & obstacleMask) != 0;

    private bool IsPlayerInAttackRange() =>
        playerDetection.IsPlayerAvailable() &&
        agentMovement.IsPlayerInAttackRange(playerDetection.GetPlayerPosition());

    public void SetPatrolPoints(Transform[] points) => patrolSystem?.SetPatrolPoints(points);

    void OnGUI()
    {
        if (showDebugInfo)
            debugDisplay.DisplayDebugInfo(gameObject.name, currentState, currentAction, debugTextOffset, debugTextColor, debugFontSize, patrolSystem.PatrolLoopsCompleted);
    }
    #endregion
    
    #region Helper Classes (Keep these in separate files or nested if preferred)
    public class DebugDisplay
    {
        private float cumulativeReward;
        private int episodeSteps;

        public void Reset()
        {
            cumulativeReward = 0f;
            episodeSteps = 0;
        }

        public void IncrementSteps() => episodeSteps++;
        public void UpdateCumulativeReward(float reward) => cumulativeReward = reward;

        public void DisplayDebugInfo(string agentName, string currentState, string currentAction, 
            Vector2 offset, Color textColor, int fontSize, int patrolLoops)
        {
            var labelStyle = new GUIStyle
            {
                fontSize = fontSize,
                normal = { textColor = textColor }
            };

            string debugText = $"{agentName}:\n" +
                            $"State: {currentState}\n" +
                            $"Action: {currentAction}\n" +
                            $"Steps: {episodeSteps}\n" +
                            $"Reward: {cumulativeReward:F3}\n" +
                            $"Patrol Loops: {patrolLoops}";
            
            GUI.Label(new Rect(offset.x, offset.y, 300, 150), debugText, labelStyle);
        }
    }
    #endregion
}

