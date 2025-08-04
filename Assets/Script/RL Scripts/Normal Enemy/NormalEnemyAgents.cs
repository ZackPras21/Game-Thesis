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
    [SerializeField] private bool useRLMovement = true;
    [SerializeField] private float rotationSmoothness = 8f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private Vector2 debugTextOffset = new Vector2(10, 10);
    [SerializeField] private Color debugTextColor = Color.white;
    [SerializeField] private int debugFontSize = 14;
    #endregion

    #region Public Properties
    public static bool TrainingActive = true;
    public float CurrentHealth => rl_EnemyController.enemyHP;
    public float MaxHealth => rl_EnemyController.enemyData.enemyHealth;
    public bool IsDead => rl_EnemyController.healthState.IsDead;
    #endregion

    #region Private Variables
    private PatrolSystem patrolSystem;
    private DebugDisplay debugDisplay;
    private RayPerceptionSensorComponent3D rayPerceptionSensor;
    private Rigidbody agentRigidbody;
    
    private AnimationClip attackAnimation;
    private float dynamicAttackCooldown = 0.5f;
    private const float HEALTH_NORMALIZATION_FACTOR = 100f;
    private const float ATTACK_COOLDOWN_FALLBACK = 0.5f;
    private const float STUCK_THRESHOLD = 0.1f;
    private const float STUCK_TIME_LIMIT = 1.5f;
    
    private string currentState = "Idle";
    private string currentAction = "Idle";
    private float previousDistanceToPlayer = float.MaxValue;
    private float lastAttackTime;
    private bool isInitialized = false;
    
    // Movement tracking
    private Vector3 lastFramePosition;
    private float stuckTimer = 0f;
    
    // Behavior state tracking
    private enum AgentBehaviorState { Idle, Patrolling, Chasing, Attacking, Fleeing, KnockedBack }
    private AgentBehaviorState currentBehaviorState = AgentBehaviorState.Idle;
    private AgentBehaviorState previousBehaviorState = AgentBehaviorState.Idle;
    
    // Reward timing control
    private float lastRewardTime = 0f;
    private const float REWARD_INTERVAL = 0.1f; // Only apply certain rewards every 0.1 seconds
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
        agentRigidbody ??= GetComponent<Rigidbody>();
        
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
        rl_EnemyController.movementSystem.ResetMovement();
        
        lastFramePosition = transform.position;
        stuckTimer = 0f;
        lastRewardTime = 0f;
        currentBehaviorState = AgentBehaviorState.Idle;
        previousBehaviorState = AgentBehaviorState.Idle;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (!isInitialized || rl_EnemyController == null) return;
        
        // Health observation
        sensor.AddObservation(CurrentHealth / MaxHealth);
        
        // Player detection and distance
        bool playerAvailable = rl_EnemyController.playerDetection.IsPlayerAvailable();
        sensor.AddObservation(playerAvailable ? 1f : 0f);
        sensor.AddObservation(playerAvailable 
            ? rl_EnemyController.playerDetection.GetDistanceToPlayer(transform.position) / HEALTH_NORMALIZATION_FACTOR : 0f);
        
        // Velocity observations
        Vector3 localVelocity = transform.InverseTransformDirection(agentRigidbody != null ? agentRigidbody.linearVelocity : navAgent.velocity);
        sensor.AddObservation(localVelocity.x / rl_EnemyController.moveSpeed);
        sensor.AddObservation(localVelocity.z / rl_EnemyController.moveSpeed);
        sensor.AddObservation(localVelocity.magnitude / rl_EnemyController.moveSpeed);
        
        // State observations
        sensor.AddObservation(IsAgentKnockedBack() ? 1f : 0f);
        sensor.AddObservation(IsAgentFleeing() ? 1f : 0f);
        sensor.AddObservation(ShouldAgentFlee() ? 1f : 0f);
        
        // Obstacle detection observations
        var obstacleInfo = rl_EnemyController.playerDetection.GetObstacleInfo();
        sensor.AddObservation(obstacleInfo.hasObstacleAhead ? 1f : 0f);
        sensor.AddObservation(obstacleInfo.hasObstacleLeft ? 1f : 0f);
        sensor.AddObservation(obstacleInfo.hasObstacleRight ? 1f : 0f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!isInitialized || rl_EnemyController == null || IsDead || !isActiveAndEnabled) return;

        debugDisplay.IncrementSteps();
        
        UpdateBehaviorState();
        CheckIfStuck();

        if (!IsAgentKnockedBack() && !IsAgentFleeing())
        {
            ProcessActions(actions);
        }
        else
        {
            HandleReactiveStates();
        }

        ProcessRewards();
        CheckEpisodeEnd();
        
        lastFramePosition = transform.position;
        previousBehaviorState = currentBehaviorState;
        debugDisplay.UpdateCumulativeReward(GetCumulativeReward());
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        var discreteActions = actionsOut.DiscreteActions;

        continuousActions[0] = Input.GetKey(KeyCode.W) ? 1f : 0f;
        continuousActions[1] = Input.GetKey(KeyCode.S) ? 1f : 0f;
        continuousActions[2] = Input.GetKey(KeyCode.D) ? 1f : 0f;
        continuousActions[3] = Input.GetKey(KeyCode.A) ? 1f : 0f;
        continuousActions[4] = GetRotationInputHeuristic();

        discreteActions[0] = Input.GetKey(KeyCode.Space) ?
            (int)EnemyHighLevelAction.Attack :
            (int)EnemyHighLevelAction.Idle;
    }

    private void CheckEpisodeEnd()
    {
        if (StepCount >= MaxStep)
        {
            EndEpisode();
        }
    }
    #endregion

    #region Initialization & Reset Helpers
    private void InitializeComponents()
    {
        navAgent.speed = rl_EnemyController.moveSpeed;
        navAgent.angularSpeed = rl_EnemyController.rotationSpeed;
        navAgent.acceleration = rl_EnemyController.moveSpeed;
        navAgent.stoppingDistance = 0.3f;
        navAgent.autoBraking = true;
        navAgent.updateRotation = false;
        navAgent.updateUpAxis = false;
        navAgent.isStopped = false;
    }

    private void InitializeSystems()
    {
        Transform[] patrolPoints = FindPatrolPoints();
        patrolSystem = new PatrolSystem(patrolPoints);
        debugDisplay = new DebugDisplay();

        if (patrolPoints.Length > 0)
        {
            patrolSystem.SetPatrolPoints(patrolPoints);
        }
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
                    return arenaPatrolPoints;
                }
            }
        }

        return FindPatrolPointsByProximityAndArena();
    }

    private Transform[] FindPatrolPointsByProximityAndArena()
    {
        GameObject[] allPatrolPoints = GameObject.FindGameObjectsWithTag("Patrol Point");
        if (allPatrolPoints.Length == 0) return new Transform[0];

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
            return arenaGroups[closestArena].OrderBy(p => p.name).ToArray();
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
        currentBehaviorState = AgentBehaviorState.Idle;
        previousBehaviorState = AgentBehaviorState.Idle;
        lastAttackTime = Time.fixedTime - dynamicAttackCooldown;

        patrolSystem?.Reset();
        rl_EnemyController.movementSystem?.ResetMovement();

        gameObject.SetActive(true);
        GetComponent<Collider>().enabled = true;
        stuckTimer = 0f;
    }

    private void ResetAgentState()
    {
        rl_EnemyController.playerDetection?.Reset();
        patrolSystem?.Reset();
        debugDisplay?.Reset();
        rl_EnemyController.movementSystem?.ResetMovement();
        
        if (agentRigidbody != null)
        {
            agentRigidbody.linearVelocity = Vector3.zero;
            agentRigidbody.angularVelocity = Vector3.zero;
        }
    }

    private void ResetTrainingArena()
    {
        FindFirstObjectByType<RL_TrainingTargetSpawner>()?.ResetArena();
    }

    private void RespawnAtRandomLocation()
    {
        var patrolPoints = patrolSystem.GetPatrolPoints();
        if (patrolPoints.Length == 0) return;

        int randomIndex = Random.Range(0, patrolPoints.Length);
        Vector3 respawnPosition = patrolPoints[randomIndex].position;

        Vector2 randomOffset = Random.insideUnitCircle * 0.5f;
        respawnPosition += new Vector3(randomOffset.x, 0, randomOffset.y);

        if (UnityEngine.AI.NavMesh.SamplePosition(respawnPosition, out UnityEngine.AI.NavMeshHit hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
        {
            respawnPosition = hit.position;
        }

        Vector3 originalScale = transform.localScale;
        rl_EnemyController.movementSystem.WarpToPosition(respawnPosition);
        transform.localScale = originalScale;

        patrolSystem.ResetToSpecificPoint(randomIndex);
    }
    #endregion

    #region Behavior State Management
    private void UpdateBehaviorState()
    {
        if (IsAgentKnockedBack())
        {
            currentBehaviorState = AgentBehaviorState.KnockedBack;
        }
        else if (IsAgentFleeing())
        {
            currentBehaviorState = AgentBehaviorState.Fleeing;
        }
        else if (rl_EnemyController.playerDetection.IsPlayerVisible)
        {
            float distanceToPlayer = rl_EnemyController.playerDetection.GetDistanceToPlayer(transform.position);
            if (distanceToPlayer <= rl_EnemyController.attackRange)
            {
                currentBehaviorState = AgentBehaviorState.Attacking;
            }
            else
            {
                currentBehaviorState = AgentBehaviorState.Chasing;
            }
        }
        else if (patrolSystem.HasValidPatrolPoints())
        {
            currentBehaviorState = AgentBehaviorState.Patrolling;
        }
        else
        {
            currentBehaviorState = AgentBehaviorState.Idle;
        }
    }

    private bool IsAgentKnockedBack() => rl_EnemyController.IsKnockedBack();
    private bool IsAgentFleeing() => rl_EnemyController.IsFleeing();
    private bool ShouldAgentFlee() => rl_EnemyController.IsHealthLow() && rl_EnemyController.playerDetection.IsPlayerAvailable();
    #endregion

    #region Action Processing
    private void ProcessActions(ActionBuffers actions)
    {
        switch (currentBehaviorState)
        {
            case AgentBehaviorState.Chasing:
                ProcessChaseMovement(actions);
                break;
            case AgentBehaviorState.Attacking:
                ProcessAttackBehavior(actions);
                break;
            case AgentBehaviorState.Patrolling:
                ProcessPatrolMovement(actions);
                break;
            default:
                ProcessIdleMovement(actions);
                break;
        }
    }

    private void ProcessChaseMovement(ActionBuffers actions)
    {
        currentState = "Chasing Player";
        currentAction = "Chasing";

        Vector3 playerPosition = rl_EnemyController.playerDetection.GetPlayerPosition();
        Vector3 directionToPlayer = (playerPosition - transform.position).normalized;
        
        float vertical = actions.ContinuousActions[0] - actions.ContinuousActions[1];
        float horizontal = actions.ContinuousActions[2] - actions.ContinuousActions[3];
        float rotation = actions.ContinuousActions[4];

        Vector3 rawMovement = new Vector3(horizontal, 0, vertical);
        Vector3 blendedMovement = Vector3.Lerp(rawMovement, directionToPlayer, 0.6f);
        Vector3 adjustedMovement = ApplyObstacleAvoidance(blendedMovement);

        rl_EnemyController.movementSystem.ProcessRLMovement(adjustedMovement, rotation, playerPosition);
        RotateTowardsTarget(playerPosition);
        ProcessAttackAction(actions);
    }

    private void ProcessAttackBehavior(ActionBuffers actions)
    {
        currentState = "In Attack Range";
        currentAction = "Preparing Attack";
        
        rl_EnemyController.movementSystem.StopMovement();
        
        Vector3 playerPosition = rl_EnemyController.playerDetection.GetPlayerPosition();
        RotateTowardsTarget(playerPosition);
        ProcessAttackAction(actions);
    }

    private void ProcessPatrolMovement(ActionBuffers actions)
    {
        currentState = "Patrolling";
        
        float vertical = actions.ContinuousActions[0] - actions.ContinuousActions[1];
        float horizontal = actions.ContinuousActions[2] - actions.ContinuousActions[3];
        float rotation = actions.ContinuousActions[4];

        Vector3 rawMovement = new Vector3(horizontal, 0, vertical);
        Vector3 adjustedMovement = ApplyObstacleAvoidance(rawMovement);

        if (patrolSystem.IsIdlingAtSpawn())
        {
            rl_EnemyController.movementSystem.StopMovement();
            currentAction = "Idling";
            currentState = $"Idling at {patrolSystem.GetCurrentPatrolPointName()} ({patrolSystem.GetIdleTimeRemaining():F1}s remaining)";
            
            patrolSystem.UpdateIdleTimer();
            return;
        }

        Vector3 currentTarget = patrolSystem.GetCurrentPatrolTarget();
        float distanceToTarget = Vector3.Distance(transform.position, currentTarget);

        if (adjustedMovement.magnitude > 0.3f && useRLMovement)
        {
            Vector3 directionToTarget = (currentTarget - transform.position).normalized;
            Vector3 blendedMovement = Vector3.Lerp(adjustedMovement, directionToTarget, 0.4f);
            rl_EnemyController.movementSystem.ProcessRLMovement(blendedMovement, rotation, currentTarget);
            currentAction = "Manual Patrol";
        }
        else
        {
            rl_EnemyController.movementSystem.ProcessNavMeshMovement(currentTarget);
            currentAction = "Auto Patrol";
        }

        if (distanceToTarget < 2f)
        {
            bool completedLoop = patrolSystem.AdvanceToNextWaypoint();
            if (completedLoop)
            {
                if (patrolSystem.PatrolLoopsCompleted >= 2)
                {
                    EndEpisode();
                    return;
                }
            }
        }

        currentState = $"Moving to {patrolSystem.GetCurrentPatrolPointName()} (dist: {distanceToTarget:F1}m)";
    }

    private void ProcessIdleMovement(ActionBuffers actions)
    {
        currentState = "Idle";
        currentAction = "Idle";
        
        float vertical = actions.ContinuousActions[0] - actions.ContinuousActions[1];
        float horizontal = actions.ContinuousActions[2] - actions.ContinuousActions[3];
        float rotation = actions.ContinuousActions[4];

        Vector3 rawMovement = new Vector3(horizontal, 0, vertical);
        
        if (rawMovement.magnitude > 0.1f)
        {
            Vector3 adjustedMovement = ApplyObstacleAvoidance(rawMovement);
            rl_EnemyController.movementSystem.ProcessRLMovement(adjustedMovement, rotation);
            currentAction = "Manual Movement";
        }
        else
        {
            rl_EnemyController.movementSystem.StopMovement();
        }
    }

    private Vector3 ApplyObstacleAvoidance(Vector3 desiredMovement)
    {
        if (desiredMovement.magnitude < 0.1f) return desiredMovement;

        var obstacleInfo = rl_EnemyController.playerDetection.GetObstacleInfo();
        Vector3 avoidanceDirection = Vector3.zero;
        float avoidanceStrength = 0f;

        if (obstacleInfo.hasObstacleAhead)
        {
            avoidanceDirection += -transform.forward * 1.2f;
            avoidanceStrength = 0.8f;
        }

        if (obstacleInfo.hasObstacleLeft && !obstacleInfo.hasObstacleRight)
        {
            avoidanceDirection += transform.right * 0.8f;
            avoidanceStrength = Mathf.Max(avoidanceStrength, 0.6f);
        }
        else if (obstacleInfo.hasObstacleRight && !obstacleInfo.hasObstacleLeft)
        {
            avoidanceDirection += -transform.right * 0.8f;
            avoidanceStrength = Mathf.Max(avoidanceStrength, 0.6f);
        }
        else if (obstacleInfo.hasObstacleLeft && obstacleInfo.hasObstacleRight)
        {
            avoidanceDirection += -transform.forward * 1.0f;
            avoidanceStrength = 0.9f;
        }

        if (avoidanceDirection.magnitude > 0.1f)
        {
            avoidanceDirection.Normalize();
            Vector3 blendedMovement = Vector3.Lerp(desiredMovement, avoidanceDirection, avoidanceStrength);
            return blendedMovement.normalized * desiredMovement.magnitude;
        }

        return desiredMovement;
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
        rl_EnemyController.AgentAttack();
        currentState = "Attacking";
        currentAction = "Attacking";
    }

    private void UpdateAnimationStates(bool playerInRange, bool canAttack)
    {
        if (animator == null || IsDead || rl_EnemyController == null) return;

        bool isMoving = rl_EnemyController.movementSystem.IsMoving();
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

        float currentSpeed = rl_EnemyController.movementSystem.GetCurrentSpeed();
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
            rl_EnemyController.movementSystem.HandleKnockback();
        }
        else if (IsAgentFleeing())
        {
            currentState = "Fleeing";
            currentAction = "Fleeing";
            
            if (rl_EnemyController.playerDetection.IsPlayerAvailable())
            {
                Vector3 fleeDirection = (transform.position - rl_EnemyController.playerDetection.GetPlayerPosition()).normalized;
                Vector3 fleeTarget = transform.position + fleeDirection;
                rl_EnemyController.movementSystem.ProcessFleeMovement(fleeTarget);
            }
        }
    }

    private void CheckIfStuck()
    {
        float distanceMoved = Vector3.Distance(transform.position, lastFramePosition);
        
        if (distanceMoved < STUCK_THRESHOLD && rl_EnemyController.movementSystem.IsMoving())
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer > STUCK_TIME_LIMIT)
            {
                rl_EnemyController.movementSystem.ApplyAntiStuckBehavior();
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
        }
    }

    private void RotateTowardsTarget(Vector3 targetPosition)
    {
        Vector3 directionToTarget = (targetPosition - transform.position);
        directionToTarget.y = 0;
        
        if (directionToTarget.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSmoothness * Time.fixedDeltaTime);
        }
    }
    #endregion

    #region Reward Processing - FIXED REWARD SYSTEM
    private void ProcessRewards()
    {
        bool shouldApplyTimedRewards = Time.fixedTime - lastRewardTime >= REWARD_INTERVAL;
        
        // Update player detection first
        rl_EnemyController.playerDetection.UpdatePlayerDetection(transform.position);
        
        // Process state-based rewards
        switch (currentBehaviorState)
        {
            case AgentBehaviorState.Chasing:
                ProcessChaseRewards(shouldApplyTimedRewards);
                break;
            case AgentBehaviorState.Attacking:
                // Attack rewards are handled in ExecuteAttack() - no continuous rewards needed
                break;
            case AgentBehaviorState.Patrolling:
                ProcessPatrolRewards(shouldApplyTimedRewards);
                break;
            case AgentBehaviorState.Fleeing:
                ProcessFleeRewards(shouldApplyTimedRewards);
                break;
            case AgentBehaviorState.Idle:
                ProcessIdleRewards(shouldApplyTimedRewards);
                break;
        }

        // Apply obstacle punishment only when actually hitting obstacles (reduced frequency)
        var obstacleInfo = rl_EnemyController.playerDetection.GetObstacleInfo();
        if ((obstacleInfo.hasObstacleAhead || obstacleInfo.hasObstacleLeft || obstacleInfo.hasObstacleRight) && shouldApplyTimedRewards)
        {
            rewardConfig.AddObstaclePunishment(this);
        }

        // State transition rewards (one-time only)
        if (previousBehaviorState != currentBehaviorState)
        {
            ProcessStateTransitionRewards();
        }

        // Update patrol system for patrolling behavior
        if (currentBehaviorState == AgentBehaviorState.Patrolling)
        {
            patrolSystem.UpdatePatrol(transform.position, navAgent, rewardConfig, this, shouldApplyTimedRewards ? Time.deltaTime : 0f);
        }

        if (shouldApplyTimedRewards)
        {
            lastRewardTime = Time.fixedTime;
        }
    }

    private void ProcessChaseRewards(bool shouldApply)
    {
        if (!shouldApply) return;

        // Small continuous reward for chasing
        rewardConfig.AddChasePlayerReward(this);
        
        // Distance-based reward (only if getting closer)
        if (rl_EnemyController.playerDetection.IsPlayerAvailable())
        {
            float currentDistance = rl_EnemyController.playerDetection.GetDistanceToPlayer(transform.position);
            if (currentDistance < previousDistanceToPlayer)
            {
                rewardConfig.AddChaseStepReward(this, Time.deltaTime);
            }
            previousDistanceToPlayer = currentDistance;
        }
    }

    private void ProcessPatrolRewards(bool shouldApply)
    {
        if (!shouldApply) return;

        // Only reward movement during patrol
        if (rl_EnemyController.movementSystem.IsMoving())
        {
            rewardConfig.AddPatrolStepReward(this, Time.deltaTime);
        }
    }

    private void ProcessFleeRewards(bool shouldApply)
    {
        if (!shouldApply) return;

        if (ShouldAgentFlee())
        {
            rewardConfig.AddFleeReward(this, Time.deltaTime);
        }
        else
        {
            // Small punishment for unnecessary fleeing
            rewardConfig.AddFleeingPunishment(this, Time.deltaTime);
        }
    }

    private void ProcessIdleRewards(bool shouldApply)
    {
        if (!shouldApply) return;

        // Only punish idle if not supposed to be idling at patrol point
        if (!patrolSystem.IsIdlingAtSpawn() && !rl_EnemyController.movementSystem.IsMoving())
        {
            rewardConfig.AddIdlePunishment(this, Time.deltaTime);
        }
    }

    private void ProcessStateTransitionRewards()
    {
        // One-time rewards for good state transitions
        if (previousBehaviorState == AgentBehaviorState.Patrolling && currentBehaviorState == AgentBehaviorState.Chasing)
        {
            rewardConfig.AddDetectionReward(this, 0f); 
        }
        else if (previousBehaviorState == AgentBehaviorState.Chasing && currentBehaviorState == AgentBehaviorState.Attacking)
        {
            rewardConfig.AddApproachPlayerReward(this, 0f); 
        }
    }

    // Event-based reward methods (called from external systems)
    public void HandleEnemyDeath()
    {
        rewardConfig.AddDeathPunishment(this);
        currentState = "Dead";
        currentAction = "Dead";
        currentBehaviorState = AgentBehaviorState.Idle;
        rl_EnemyController.movementSystem.StopMovement();
        EndEpisode();
    }

    public void HandleDamage()
    {
        rewardConfig.AddDamagePunishment(this);
        currentState = "Taking Damage";
        currentAction = "Reacting";
    }

    public void HandleKillPlayer()
    {
        rewardConfig.AddKillPlayerReward(this);
    }

    public void HandleAttackHit()
    {
        rewardConfig.AddAttackReward(this);
    }

    public void HandlePatrolLoopComplete()
    {
        rewardConfig.AddPatrolReward(this);
    }
    #endregion

    #region Utility Methods
    private float GetRotationInputHeuristic()
    {
        if (Input.GetKey(KeyCode.Q)) return -1f;
        if (Input.GetKey(KeyCode.E)) return 1f;
        return 0f;
    }

    private bool IsObstacleCollision(Collision collision) =>
        ((1 << collision.gameObject.layer) & obstacleMask) != 0;

    private bool IsPlayerInAttackRange() =>
        rl_EnemyController.playerDetection.IsPlayerAvailable() &&
        rl_EnemyController.movementSystem.IsPlayerInAttackRange(rl_EnemyController.playerDetection.GetPlayerPosition());

    public void SetPatrolPoints(Transform[] points) => patrolSystem?.SetPatrolPoints(points);
    #endregion

    #region Debug and Events
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
    
    #region Helper Classes
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
            
            GUI.Label(new Rect(offset.x, offset.y, 300, 180), debugText, labelStyle);
        }
    }
    #endregion
}
