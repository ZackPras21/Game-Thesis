using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine.AI;
using System.Linq;
using static NormalEnemyActions;
using System.Collections.Generic;

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

    [Header("Movement Control")]
    [SerializeField] private bool useRLMovement = true; // Toggle between RL and NavMesh control
    [SerializeField] private float obstacleAvoidanceWeight = 0.7f;
    [SerializeField] private float rotationSmoothness = 5f;

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
    private EnhancedPlayerDetection playerDetection;
    private PatrolSystem patrolSystem;
    private UnifiedMovementSystem movementSystem;
    private DebugDisplay debugDisplay;
    private RayPerceptionSensorComponent3D rayPerceptionSensor;
    
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
    
    // Movement state tracking
    private Vector3 lastFramePosition;
    private float stuckTimer = 0f;
    private const float STUCK_THRESHOLD = 0.1f;
    private const float STUCK_TIME_LIMIT = 2f;
    
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
        rayPerceptionSensor ??= GetComponent<RayPerceptionSensorComponent3D>();
        
        if (navAgent == null || rayPerceptionSensor == null)
        {
            Debug.LogError("NormalEnemyAgent: Required components missing!", gameObject);
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
        lastFramePosition = transform.position;
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
        movementSystem.ResetMovement();
        lastFramePosition = transform.position;
        stuckTimer = 0f;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (!isInitialized || rl_EnemyController == null) return;
        
        // Enhanced observations (10 total)
        sensor.AddObservation(CurrentHealth / MaxHealth);
        sensor.AddObservation(playerDetection.IsPlayerAvailable() 
            ? playerDetection.GetDistanceToPlayer(transform.position) / HEALTH_NORMALIZATION_FACTOR : 0f);
        
        // Velocity observations
        Vector3 localVelocity = transform.InverseTransformDirection(navAgent.velocity);
        sensor.AddObservation(localVelocity.x / rl_EnemyController.moveSpeed);
        sensor.AddObservation(localVelocity.z / rl_EnemyController.moveSpeed);
        
        // State observations
        sensor.AddObservation(IsAgentKnockedBack() ? 1f : 0f);
        sensor.AddObservation(IsAgentFleeing() ? 1f : 0f);
        sensor.AddObservation(ShouldAgentFlee() ? 1f : 0f);
        
        // Enhanced obstacle detection observations
        var obstacleInfo = playerDetection.GetObstacleInfo();
        sensor.AddObservation(obstacleInfo.hasObstacleAhead ? 1f : 0f);
        sensor.AddObservation(obstacleInfo.hasObstacleLeft ? 1f : 0f);
        sensor.AddObservation(obstacleInfo.hasObstacleRight ? 1f : 0f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!isInitialized || rl_EnemyController == null || IsDead || !isActiveAndEnabled) return;

        debugDisplay.IncrementSteps();
        CheckIfStuck();

        // Process actions based on current state
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
        
        lastFramePosition = transform.position;
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
        ConfigureNavMeshAgent();
        playerDetection = new EnhancedPlayerDetection(rayPerceptionSensor, obstacleMask);
    }

    private void InitializeSystems()
    {
        Transform[] patrolPoints = FindPatrolPoints();
        patrolSystem = new PatrolSystem(patrolPoints);
        movementSystem = new UnifiedMovementSystem(navAgent, transform, rl_EnemyController.moveSpeed, 
            rl_EnemyController.rotationSpeed, rl_EnemyController.attackRange, playerDetection);
        debugDisplay = new DebugDisplay();

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
        navAgent.angularSpeed = rl_EnemyController.rotationSpeed * 100f; // Convert to degrees/second
        navAgent.acceleration = rl_EnemyController.moveSpeed * 4f;
        navAgent.stoppingDistance = 0.5f;
        navAgent.autoBraking = true;
        navAgent.updateRotation = false; // Let our system handle rotation
        navAgent.updateUpAxis = false;
        navAgent.isStopped = false;
    }

    private Transform[] FindPatrolPoints()
    {
        var spawner = FindFirstObjectByType<RL_TrainingEnemySpawner>();
        if (spawner != null)
        {
            Transform currentParent = transform.parent;
            while (currentParent != null)
            {
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
        GameObject[] allPatrolPoints = GameObject.FindGameObjectsWithTag("Patrol Point");

        if (allPatrolPoints.Length == 0)
        {
            Debug.LogWarning($"{gameObject.name} no patrol points found in scene with 'Patrol Point' tag");
            return new Transform[0];
        }

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
        movementSystem?.ResetMovement();

        gameObject.SetActive(true);
        GetComponent<Collider>().enabled = true;
        stuckTimer = 0f;
    }

    private void ResetAgentState()
    {
        playerDetection?.Reset();
        patrolSystem?.Reset();
        debugDisplay?.Reset();
        movementSystem?.ResetMovement();
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

        Vector3 rawMovement = new Vector3(horizontal, 0, vertical);
        
        // Apply obstacle avoidance using RayPerception data
        Vector3 adjustedMovement = ApplyIntelligentObstacleAvoidance(rawMovement);

        bool isPlayerVisible = playerDetection.IsPlayerVisible;
        bool isPatrolling = !isPlayerVisible && patrolSystem.HasValidPatrolPoints();

        if (isPlayerVisible)
        {
            currentState = "Chasing";
            currentAction = "Chasing";
            
            if (useRLMovement)
            {
                movementSystem.ProcessRLMovement(adjustedMovement, rotation, playerDetection.GetPlayerPosition());
            }
            else
            {
                movementSystem.ProcessNavMeshMovement(playerDetection.GetPlayerPosition());
                if (adjustedMovement.magnitude > 0.1f) // RL can still influence NavMesh movement
                {
                    movementSystem.ApplyMovementInfluence(adjustedMovement);
                }
            }
        }
        else if (isPatrolling)
        {
            if (adjustedMovement.magnitude > 0.1f && useRLMovement)
            {
                // RL takes control during patrol with manual input
                movementSystem.ProcessRLMovement(adjustedMovement, rotation);
                currentAction = "Manual Patrol";
            }
            else
            {
                // Let patrol system handle movement
                UpdatePatrolMovement();
            }
        }
        else
        {
            // Idle state - RL control only
            if (adjustedMovement.magnitude > 0.1f)
            {
                movementSystem.ProcessRLMovement(adjustedMovement, rotation);
                currentAction = "Manual Movement";
            }
            else
            {
                movementSystem.StopMovement();
                currentAction = "Idle";
            }
        }
    }

    private Vector3 ApplyIntelligentObstacleAvoidance(Vector3 desiredMovement)
    {
        if (desiredMovement.magnitude < 0.1f) return desiredMovement;

        var obstacleInfo = playerDetection.GetObstacleInfo();
        Vector3 avoidanceDirection = Vector3.zero;

        // Calculate avoidance based on detected obstacles
        if (obstacleInfo.hasObstacleAhead)
        {
            // Strong avoidance for front obstacles
            avoidanceDirection += -transform.forward * 1.0f;
        }

        if (obstacleInfo.hasObstacleLeft)
        {
            // Avoid left, prefer right
            avoidanceDirection += transform.right * 0.7f;
        }

        if (obstacleInfo.hasObstacleRight)
        {
            // Avoid right, prefer left
            avoidanceDirection += -transform.right * 0.7f;
        }

        // If we detected obstacles, blend avoidance with desired movement
        if (avoidanceDirection.magnitude > 0.1f)
        {
            avoidanceDirection.Normalize();
            Vector3 blendedMovement = Vector3.Lerp(desiredMovement, avoidanceDirection, obstacleAvoidanceWeight);
            return blendedMovement.normalized * desiredMovement.magnitude;
        }

        return desiredMovement;
    }

    private void UpdatePatrolMovement()
    {
        if (!patrolSystem.HasValidPatrolPoints()) return;

        if (patrolSystem.IsIdlingAtSpawn())
        {
            movementSystem.StopMovement();
            currentAction = "Idling";
            currentState = $"Idling at {patrolSystem.GetCurrentPatrolPointName()} ({patrolSystem.GetIdleTimeRemaining():F1}s remaining)";
            
            bool idleComplete = patrolSystem.UpdateIdleTimer();
            if (idleComplete)
            {
                currentAction = "Patrolling";
            }
            return;
        }

        Vector3 currentTarget = patrolSystem.GetCurrentPatrolTarget();
        float distanceToTarget = Vector3.Distance(transform.position, currentTarget);

        if (useRLMovement)
        {
            // RL-controlled patrol movement
            Vector3 directionToTarget = (currentTarget - transform.position).normalized;
            movementSystem.ProcessRLMovement(directionToTarget, 0f, currentTarget);
        }
        else
        {
            // NavMesh-controlled patrol movement
            movementSystem.ProcessNavMeshMovement(currentTarget);
        }

        // Check if we've reached the waypoint
        if (distanceToTarget < 2f)
        {
            bool completedLoop = patrolSystem.AdvanceToNextWaypoint();
            if (completedLoop)
            {
                rewardConfig.AddPatrolReward(this);
                Debug.Log($"{gameObject.name} completed patrol loop {patrolSystem.PatrolLoopsCompleted}");

                if (patrolSystem.PatrolLoopsCompleted >= 2)
                {
                    rewardConfig.AddPatrolReward(this);
                    Debug.Log($"{gameObject.name} completed 2 patrol loops. Ending episode.");
                    EndEpisode();
                    return;
                }
            }
        }

        currentAction = "Patrolling";
        currentState = $"Moving to {patrolSystem.GetCurrentPatrolPointName()} (dist: {distanceToTarget:F1}m)";
    }

    private void ProcessAttackAction(ActionBuffers actions)
    {
        bool shouldAttack = actions.DiscreteActions[0] == (int)EnemyHighLevelAction.Attack;
        bool playerInRange = IsPlayerInAttackRange();
        bool canAttack = Time.fixedTime - lastAttackTime >= dynamicAttackCooldown;

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

        bool isMoving = movementSystem.IsMoving();
        bool shouldAttackAnim = playerInRange && canAttack && rl_EnemyController.combatState.IsAttacking;

        animator.SetBool("isWalking", isMoving && !IsAgentKnockedBack() && !shouldAttackAnim);
        animator.SetBool("isAttacking", shouldAttackAnim);
        animator.SetBool("isIdle", !isMoving && !shouldAttackAnim && !IsAgentKnockedBack());

        if (IsAgentKnockedBack())
        {
            animator.SetBool("isWalking", false);
            animator.SetBool("isAttacking", false);
            animator.SetBool("isIdle", true);
            return;
        }

        if (IsAgentFleeing())
        {
            animator.SetBool("isWalking", true);
            animator.SetBool("isAttacking", false);
            animator.SetBool("isIdle", false);
            animator.speed = 1.5f;
            return;
        }

        // Adjust animation speed based on movement speed
        float currentSpeed = movementSystem.GetCurrentSpeed();
        if (currentSpeed > 0.1f)
        {
            float speedRatio = currentSpeed / rl_EnemyController.moveSpeed;
            animator.speed = Mathf.Clamp(speedRatio, 0.8f, 1.2f);
        }
        else
        {
            animator.speed = 1f;
        }

        if (shouldAttackAnim && !animator.GetCurrentAnimatorStateInfo(0).IsName("Attack"))
        {
            animator.Play("Attack", 0, 0f);
        }
    }

    private void HandleReactiveStates()
    {
        if (IsAgentKnockedBack())
        {
            currentState = "Knocked Back";
            currentAction = "Recovering";
            movementSystem.HandleKnockback();
        }
        else if (IsAgentFleeing())
        {
            currentState = "Fleeing";
            currentAction = "Fleeing";
            
            if (playerDetection.IsPlayerAvailable())
            {
                Vector3 fleeDirection = (transform.position - playerDetection.GetPlayerPosition()).normalized;
                Vector3 fleeTarget = transform.position + fleeDirection * 10f;
                movementSystem.ProcessFleeMovement(fleeTarget);
            }
        }
    }

    private void CheckIfStuck()
    {
        float distanceMoved = Vector3.Distance(transform.position, lastFramePosition);
        
        if (distanceMoved < STUCK_THRESHOLD && movementSystem.IsMoving())
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer > STUCK_TIME_LIMIT)
            {
                // Apply anti-stuck behavior
                rewardConfig.AddObstaclePunishment(this);
                movementSystem.ApplyAntiStuckBehavior();
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
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

        Vector2 randomOffset = Random.insideUnitCircle * 0.5f;
        respawnPosition += new Vector3(randomOffset.x, 0, randomOffset.y);

        if (UnityEngine.AI.NavMesh.SamplePosition(respawnPosition, out UnityEngine.AI.NavMeshHit hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
        {
            respawnPosition = hit.position;
        }

        Vector3 originalScale = transform.localScale;
        movementSystem.WarpToPosition(respawnPosition);
        transform.localScale = originalScale;

        patrolSystem.ResetToSpecificPoint(randomIndex);
        Debug.Log($"{gameObject.name} respawned at {patrolPoints[randomIndex].name}");
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

        var obstacleInfo = playerDetection.GetObstacleInfo();
        if (obstacleInfo.hasObstacleAhead || obstacleInfo.hasObstacleLeft || obstacleInfo.hasObstacleRight)
        {
            if (!hasObstaclePunishmentThisFrame)
            {
                rewardConfig.AddObstaclePunishment(this);
                hasObstaclePunishmentThisFrame = true;
            }
        }
        else
        {
            hasObstaclePunishmentThisFrame = false;
        }

        if (currentAction == "Chasing")
        {
            rewardConfig.AddChaseStepReward(this, deltaTime);
            ProcessChaseRewards(deltaTime);
        }
        else if (currentAction == "Patrolling")
        {
            rewardConfig.AddPatrolStepReward(this, deltaTime);
            
            if (movementSystem.IsMoving())
            {
                AddReward(0.002f * deltaTime);
            }

            if (!obstacleInfo.hasObstacleAhead)
            {
                AddReward(0.001f * deltaTime);
            }
        }
        else if (currentAction == "Idle" || currentAction == "Idling")
        {
            if (!patrolSystem.IsIdlingAtSpawn() && !movementSystem.IsMoving())
            {
                rewardConfig.AddIdlePunishment(this, deltaTime * 0.5f);
            }
        }

        ProcessPlayerVisibilityRewards(deltaTime);
        ProcessDistanceRewards(deltaTime);
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
            rewardConfig.AddIdlePunishment(this, Time.deltaTime * 0.2f);
            return;
        }

        if (patrolSystem.IsIdlingAtSpawn())
        {
            currentAction = "Idling";
            currentState = $"Idling at {patrolSystem.GetCurrentPatrolPointName()} ({patrolSystem.GetIdleTimeRemaining():F1}s remaining)";
            return;
        }

        Vector3 currentTarget = patrolSystem.GetCurrentPatrolTarget();
        float distanceToTarget = Vector3.Distance(transform.position, currentTarget);

        if (distanceToTarget < 2f)
        {
            bool completedLoop = patrolSystem.AdvanceToNextWaypoint();
            if (completedLoop)
            {
                rewardConfig.AddPatrolReward(this);
                Debug.Log($"{gameObject.name} completed patrol loop {patrolSystem.PatrolLoopsCompleted}");

                if (patrolSystem.PatrolLoopsCompleted >= 2)
                {
                    rewardConfig.AddPatrolReward(this);
                    Debug.Log($"{gameObject.name} completed 2 patrol loops. Ending episode.");
                    EndEpisode();
                    return;
                }
            }

            currentAction = patrolSystem.IsIdlingAtSpawn() ? "Idling" : "Patrolling";
            currentState = patrolSystem.IsIdlingAtSpawn() ?
                $"Starting idle at {patrolSystem.GetCurrentPatrolPointName()}" :
                $"Reached {patrolSystem.GetCurrentPatrolPointName()}, moving to next";
        }
        else
        {
            currentAction = "Patrolling";
            currentState = $"Moving to {patrolSystem.GetCurrentPatrolPointName()} (dist: {distanceToTarget:F1}m)";

            if (!movementSystem.IsMoving())
            {
                rewardConfig.AddNoMovementPunishment(this, Time.deltaTime * 0.3f);
            }
        }
    }

    public void HandleEnemyDeath()
    {
        rewardConfig.AddDeathPunishment(this);
        currentState = "Dead";
        currentAction = "Dead";
        movementSystem.StopMovement();
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
        movementSystem.IsPlayerInAttackRange(playerDetection.GetPlayerPosition());

    public void SetPatrolPoints(Transform[] points) => patrolSystem?.SetPatrolPoints(points);

    void OnGUI()
    {
        if (showDebugInfo)
            debugDisplay.DisplayDebugInfo(gameObject.name, currentState, currentAction, debugTextOffset, debugTextColor, debugFontSize, patrolSystem.PatrolLoopsCompleted);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (IsObstacleCollision(collision))
            rewardConfig.AddObstaclePunishment(this);
    }
    #endregion
    
    #region Enhanced Helper Classes
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

    // Enhanced Player Detection using RayPerception3D for obstacle avoidance
    public class EnhancedPlayerDetection
    {
        private readonly RayPerceptionSensorComponent3D raySensor;
        private readonly LayerMask obstacleMask;
        private Transform playerTransform;
        private bool isPlayerVisible;
        private float lastPlayerDistance;
        private Vector3 lastPlayerPosition;
        private float lastPlayerCheckTime;
        private const float PLAYER_CHECK_INTERVAL = 0.5f;

        // Obstacle detection data
        private ObstacleInfo currentObstacleInfo;

        public struct ObstacleInfo
        {
            public bool hasObstacleAhead;
            public bool hasObstacleLeft;
            public bool hasObstacleRight;
            public float distanceAhead;
            public float distanceLeft;
            public float distanceRight;
        }

        public EnhancedPlayerDetection(RayPerceptionSensorComponent3D raySensor, LayerMask obstacleMask)
        {
            this.raySensor = raySensor;
            this.obstacleMask = obstacleMask;
            FindPlayerTransform();
            currentObstacleInfo = new ObstacleInfo();
        }

        public void Reset()
        {
            isPlayerVisible = false;
            lastPlayerDistance = float.MaxValue;
            lastPlayerPosition = Vector3.zero;
            currentObstacleInfo = new ObstacleInfo();
            FindPlayerTransform();
        }

        public void UpdatePlayerDetection(Vector3 agentPosition)
        {
            isPlayerVisible = false;
            UpdateObstacleDetection();

            if (!IsPlayerAvailable() || playerTransform == null || !playerTransform.gameObject.activeInHierarchy)
            {
                if (Time.time - lastPlayerCheckTime > PLAYER_CHECK_INTERVAL)
                {
                    FindPlayerTransform();
                    lastPlayerCheckTime = Time.time;
                }
                if (!IsPlayerAvailable()) return;
            }

            try
            {
                var rayOutputs = RayPerceptionSensor.Perceive(raySensor.GetRayPerceptionInput(), false);

                foreach (var rayOutput in rayOutputs.RayOutputs)
                {
                    if (rayOutput.HasHit && rayOutput.HitGameObject != null)
                    {
                        if (rayOutput.HitGameObject.CompareTag("Player"))
                        {
                            isPlayerVisible = true;
                            lastPlayerDistance = rayOutput.HitFraction * raySensor.RayLength;
                            
                            if (playerTransform != null)
                            {
                                lastPlayerPosition = playerTransform.position;
                            }
                            break;
                        }
                    }
                }

                if (isPlayerVisible && playerTransform != null)
                {
                    float actualDistance = Vector3.Distance(agentPosition, playerTransform.position);
                    if (actualDistance > raySensor.RayLength * 1.1f)
                    {
                        isPlayerVisible = false;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Player detection error: {e.Message}");
                playerTransform = null;
                isPlayerVisible = false;
            }
        }

        private void UpdateObstacleDetection()
        {
            var rayOutputs = RayPerceptionSensor.Perceive(raySensor.GetRayPerceptionInput(), false);
            
            // Reset obstacle info
            currentObstacleInfo = new ObstacleInfo
            {
                distanceAhead = float.MaxValue,
                distanceLeft = float.MaxValue,
                distanceRight = float.MaxValue
            };

            // Analyze ray outputs for obstacles
            int rayCount = rayOutputs.RayOutputs.Length;
            if (rayCount == 0) return;

            // Calculate ray indices for different directions
            int centerRayIndex = rayCount / 2;
            int leftRayIndex = centerRayIndex - rayCount / 4;
            int rightRayIndex = centerRayIndex + rayCount / 4;

            // Ensure indices are within bounds
            leftRayIndex = Mathf.Clamp(leftRayIndex, 0, rayCount - 1);
            rightRayIndex = Mathf.Clamp(rightRayIndex, 0, rayCount - 1);

            // Check for obstacles in each direction
            CheckObstacleInDirection(rayOutputs.RayOutputs, centerRayIndex, ref currentObstacleInfo.hasObstacleAhead, ref currentObstacleInfo.distanceAhead);
            CheckObstacleInDirection(rayOutputs.RayOutputs, leftRayIndex, ref currentObstacleInfo.hasObstacleLeft, ref currentObstacleInfo.distanceLeft);
            CheckObstacleInDirection(rayOutputs.RayOutputs, rightRayIndex, ref currentObstacleInfo.hasObstacleRight, ref currentObstacleInfo.distanceRight);
        }

        private void CheckObstacleInDirection(RayPerceptionOutput.RayOutput[] rayOutputs, int rayIndex, ref bool hasObstacle, ref float distance)
        {
            if (rayIndex >= 0 && rayIndex < rayOutputs.Length)
            {
                var rayOutput = rayOutputs[rayIndex];
                if (rayOutput.HasHit && rayOutput.HitGameObject != null)
                {
                    // Check if hit object is an obstacle (not player)
                    if (((1 << rayOutput.HitGameObject.layer) & obstacleMask) != 0)
                    {
                        hasObstacle = true;
                        distance = rayOutput.HitFraction * raySensor.RayLength;
                    }
                }
            }
        }

        private void FindPlayerTransform()
        {
            playerTransform = null;
            
            var rlPlayer = Object.FindFirstObjectByType<RL_Player>();
            if (rlPlayer != null && rlPlayer.gameObject.activeInHierarchy)
            {
                playerTransform = rlPlayer.transform;
                return;
            }

            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null && playerObj.activeInHierarchy)
            {
                playerTransform = playerObj.transform;
            }
        }

        public bool IsPlayerAvailable() 
        {
            return playerTransform != null && 
                playerTransform.gameObject != null && 
                playerTransform.gameObject.activeInHierarchy;
        }
        
        public bool IsPlayerVisible => isPlayerVisible && IsPlayerAvailable();
        
        public Vector3 GetPlayerPosition() 
        {
            if (IsPlayerAvailable())
            {
                try
                {
                    Vector3 currentPos = playerTransform.position;
                    lastPlayerPosition = currentPos;
                    return currentPos;
                }
                catch (System.Exception)
                {
                    playerTransform = null;
                }
            }
            
            return lastPlayerPosition != Vector3.zero ? lastPlayerPosition : Vector3.zero;
        }
        
        public Transform GetPlayerTransform() => IsPlayerAvailable() ? playerTransform : null;

        public float GetDistanceToPlayer(Vector3 agentPosition)
        {
            if (!IsPlayerAvailable()) return float.MaxValue;

            try
            {
                Vector3 playerPos = GetPlayerPosition();
                
                if (isPlayerVisible && lastPlayerDistance > 0)
                {
                    return lastPlayerDistance;
                }

                return Vector3.Distance(agentPosition, playerPos);
            }
            catch (System.Exception)
            {
                return float.MaxValue;
            }
        }

        public ObstacleInfo GetObstacleInfo() => currentObstacleInfo;
    }

    // Unified Movement System to eliminate conflicts
    public class UnifiedMovementSystem
    {
        private readonly NavMeshAgent navAgent;
        private readonly Transform agentTransform;
        private readonly float moveSpeed;
        private readonly float turnSpeed;
        private readonly float attackRange;
        private readonly EnhancedPlayerDetection playerDetection;

        private bool isRLControlled = false;
        private Vector3 lastMovementDirection = Vector3.zero;
        private float currentMovementSpeed = 0f;

        public UnifiedMovementSystem(NavMeshAgent navAgent, Transform agentTransform, float moveSpeed, 
            float turnSpeed, float attackRange, EnhancedPlayerDetection playerDetection)
        {
            this.navAgent = navAgent;
            this.agentTransform = agentTransform;
            this.moveSpeed = moveSpeed;
            this.turnSpeed = turnSpeed;
            this.attackRange = attackRange;
            this.playerDetection = playerDetection;
        }

        public void ResetMovement()
        {
            if (navAgent != null && navAgent.enabled)
            {
                navAgent.velocity = Vector3.zero;
                navAgent.isStopped = false;
                navAgent.ResetPath();
                navAgent.updateRotation = false; // Always let our system handle rotation
            }
            isRLControlled = false;
            lastMovementDirection = Vector3.zero;
            currentMovementSpeed = 0f;
        }

        public void ProcessRLMovement(Vector3 movement, float rotation, Vector3 targetPosition = default)
        {
            isRLControlled = true;
            
            // Stop NavMesh pathfinding when RL takes control
            if (navAgent != null && navAgent.enabled)
            {
                navAgent.ResetPath();
                navAgent.isStopped = true;
            }

            // Apply movement
            if (movement.magnitude > 0.1f)
            {
                Vector3 worldMovement = agentTransform.TransformDirection(movement).normalized;
                Vector3 targetPos = agentTransform.position + worldMovement * moveSpeed * Time.fixedDeltaTime;
                
                // Validate movement target
                if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out UnityEngine.AI.NavMeshHit hit, 1f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    agentTransform.position = Vector3.MoveTowards(agentTransform.position, hit.position, moveSpeed * Time.fixedDeltaTime);
                    currentMovementSpeed = moveSpeed;
                }
                
                lastMovementDirection = worldMovement;
            }
            else
            {
                currentMovementSpeed = 0f;
            }

            // Apply rotation
            if (targetPosition != default)
            {
                // Rotate towards target
                Vector3 direction = (targetPosition - agentTransform.position);
                direction.y = 0;
                
                if (direction.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    agentTransform.rotation = Quaternion.Slerp(
                        agentTransform.rotation,
                        targetRotation,
                        turnSpeed * Time.fixedDeltaTime
                    );
                }
            }
            else if (Mathf.Abs(rotation) > 0.1f)
            {
                // Manual rotation input
                agentTransform.Rotate(0, rotation * turnSpeed * Time.fixedDeltaTime, 0);
            }
        }

        public void ProcessNavMeshMovement(Vector3 targetPosition)
        {
            isRLControlled = false;
            
            if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
            {
                navAgent.isStopped = false;
                navAgent.speed = moveSpeed;
                navAgent.SetDestination(targetPosition);
                
                // Handle rotation manually even for NavMesh movement
                Vector3 direction = (targetPosition - agentTransform.position);
                direction.y = 0;
                
                if (direction.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    agentTransform.rotation = Quaternion.Slerp(
                        agentTransform.rotation,
                        targetRotation,
                        turnSpeed * Time.fixedDeltaTime
                    );
                }
                
                currentMovementSpeed = navAgent.velocity.magnitude;
            }
        }

        public void ProcessFleeMovement(Vector3 fleeTarget)
        {
            if (UnityEngine.AI.NavMesh.SamplePosition(fleeTarget, out UnityEngine.AI.NavMeshHit hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                if (navAgent != null && navAgent.enabled)
                {
                    navAgent.isStopped = false;
                    navAgent.SetDestination(hit.position);
                    navAgent.speed = moveSpeed * 1.5f; // Faster flee speed
                    
                    // Face flee direction
                    Vector3 fleeDirection = (hit.position - agentTransform.position);
                    fleeDirection.y = 0;
                    
                    if (fleeDirection.sqrMagnitude > 0.01f)
                    {
                        Quaternion targetRotation = Quaternion.LookRotation(fleeDirection);
                        agentTransform.rotation = Quaternion.Slerp(
                            agentTransform.rotation,
                            targetRotation,
                            turnSpeed * 2f * Time.fixedDeltaTime // Faster rotation during flee
                        );
                    }
                }
            }
            currentMovementSpeed = navAgent != null ? navAgent.velocity.magnitude : 0f;
        }

        public void HandleKnockback()
        {
            if (navAgent != null && navAgent.enabled)
            {
                navAgent.isStopped = true;
                navAgent.ResetPath();
            }
            currentMovementSpeed = 0f;
        }

        public void StopMovement()
        {
            isRLControlled = false;
            if (navAgent != null && navAgent.enabled)
            {
                navAgent.isStopped = true;
                navAgent.ResetPath();
                navAgent.velocity = Vector3.zero;
            }
            currentMovementSpeed = 0f;
        }

        public void ApplyMovementInfluence(Vector3 movement)
        {
            // Allow RL to influence NavMesh movement
            if (navAgent != null && navAgent.enabled && movement.magnitude > 0.1f)
            {
                Vector3 currentDestination = navAgent.destination;
                Vector3 influence = agentTransform.TransformDirection(movement) * 2f;
                Vector3 newDestination = currentDestination + influence;
                
                if (UnityEngine.AI.NavMesh.SamplePosition(newDestination, out UnityEngine.AI.NavMeshHit hit, 3f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    navAgent.SetDestination(hit.position);
                }
            }
        }

        public void ApplyAntiStuckBehavior()
        {
            // Apply random movement to get unstuck
            Vector3 randomDirection = Random.insideUnitSphere;
            randomDirection.y = 0;
            randomDirection.Normalize();
            
            Vector3 unstuckTarget = agentTransform.position + randomDirection * 3f;
            
            if (UnityEngine.AI.NavMesh.SamplePosition(unstuckTarget, out UnityEngine.AI.NavMeshHit hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                if (navAgent != null && navAgent.enabled)
                {
                    navAgent.isStopped = false;
                    navAgent.SetDestination(hit.position);
                }
            }
        }

        public void WarpToPosition(Vector3 position)
        {
            if (navAgent != null)
            {
                navAgent.enabled = false;
                agentTransform.position = position;
                navAgent.enabled = true;
                
                if (navAgent.isOnNavMesh)
                {
                    navAgent.Warp(position);
                }
            }
            else
            {
                agentTransform.position = position;
            }
        }

        public bool IsPlayerInAttackRange(Vector3 playerPosition) =>
            Vector3.SqrMagnitude(agentTransform.position - playerPosition) <= attackRange * attackRange;

        public bool IsMoving() => currentMovementSpeed > 0.1f;
        public float GetCurrentSpeed() => currentMovementSpeed;
    }
    #endregion
}