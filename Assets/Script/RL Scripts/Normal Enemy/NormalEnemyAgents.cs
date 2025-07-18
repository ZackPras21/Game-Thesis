using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine.AI;
using System.Linq;
using static NormalEnemyActions; 

[RequireComponent(typeof(NavMeshAgent), typeof(RayPerceptionSensorComponent3D), typeof(RL_EnemyController))]
public class NormalEnemyAgent : Agent
{
    #region Serialized Fields
    [Header("References")]
    [SerializeField] private RL_EnemyController rl_EnemyController;
    [SerializeField] private NormalEnemyRewards enemyRewards;
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent navAgent;
    [SerializeField] private LayerMask obstacleMask;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private Vector2 debugTextOffset = new Vector2(10, 10);
    [SerializeField] private Color debugTextColor = Color.white;
    [SerializeField] private int debugFontSize = 14;
    #endregion

    #region Private Variables
    private PlayerDetection playerDetection;
    private PatrolSystem patrolSystem;
    private AgentMovement agentMovement;
    private NormalEnemyStates enemyStates; 
    private DebugDisplay debugDisplay;
    private NormalEnemyRewards.RewardSystem rewardSystem;
    private const float HEALTH_NORMALIZATION_FACTOR = 100f;
    private const float ATTACK_COOLDOWN_FALLBACK = 0.3f;
    private bool wasPlayerVisibleLastFrame = false;
    private bool hasPlayerBeenKilled = false;
    private Vector3 lastPlayerPosition = Vector3.zero;
    private float attackIncentiveTimer = 0f;
    private const float ATTACK_INCENTIVE_THRESHOLD = 2f;
    private int currentStepCount;
    private Vector3 initialPosition;
    private string currentState = "Idle";
    private string currentAction = "Idle";
    private float previousDistanceToPlayer = float.MaxValue;
    private bool isInitialized = false;
    #endregion

    #region Public Properties & Variables
    public float CurrentHealth => rl_EnemyController?.enemyHP ?? 0f;
    public float MaxHealth => rl_EnemyController?.enemyData?.enemyHealth ?? 100f;
    public bool IsDead => enemyStates?.HealthState?.IsDead ?? false;
    #endregion

    #region Agent Lifecycle
    public override void Initialize()
    {
        if (!rl_EnemyController.IsInitialized)
        {
            rl_EnemyController.ForceInitialize();
        }
        InitializeEnemyStates(); 
        InitializeComponents();
        InitializeSystems();
        initialPosition = transform.position;
        ResetAgentState();
        rl_EnemyController.InitializeHealthBar();
        isInitialized = true;

        if (rl_EnemyController != null)
        {
            rl_EnemyController.IsMLControlled = true;
        }
    }

    public override void OnEpisodeBegin()
    {
        if (!isInitialized)
        {
            Initialize();
            if (!isInitialized) return;
        }
        
        // Stop any ongoing coroutines
        StopAllCoroutines();
        
        ResetForNewEpisode();
        WarpToRandomPatrolPoint();
        ResetTrainingArena();
        
        Debug.Log($"{gameObject.name} episode began - Health: {CurrentHealth}");
        
        if (rl_EnemyController != null)
        {
            rl_EnemyController.ShowHealthBar();
            // Ensure the controller's combat state is properly reset
            rl_EnemyController.enemyStates.CombatState.ResetCombatState();
            // Reset ML control flag
            rl_EnemyController.IsMLControlled = true;
        }
        
        // Reset animator to a living state
        if (animator != null)
        {
            animator.SetBool("isDead", false);
            animator.SetBool("isAttacking", false);
            animator.SetBool("isWalking", false);
            animator.SetBool("isIdle", true);
            animator.ResetTrigger("getHit");
            animator.ResetTrigger("attack");
        }
        
        // Re-enable collider and NavMeshAgent
        var collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = true;
        }
        
        if (navAgent != null)
        {
            navAgent.enabled = true;
            navAgent.isStopped = false;
        }
        
        // Force player detection refresh
        if (playerDetection != null)
        {
            playerDetection.Reset();
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (!isInitialized || rl_EnemyController == null) return;

        sensor.AddObservation(CurrentHealth / MaxHealth);
        sensor.AddObservation(playerDetection != null && playerDetection.IsPlayerAvailable()
            ? playerDetection.GetDistanceToPlayer(transform.position) / HEALTH_NORMALIZATION_FACTOR
            : 0f);

        if (navAgent != null)
        {
            Vector3 localVelocity = transform.InverseTransformDirection(navAgent.velocity);
            sensor.AddObservation(localVelocity.x / rl_EnemyController.moveSpeed);
            sensor.AddObservation(localVelocity.z / rl_EnemyController.moveSpeed);
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!isInitialized || rl_EnemyController == null || IsDead || !isActiveAndEnabled) return;

        UpdateStepCount();
        ProcessActions(actions);
        UpdateBehaviorAndRewards();
        CheckEpisodeEnd();
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
    #endregion

    #region Initialization & Reset Helpers
    private void InitializeEnemyStates()
    {
        enemyStates = rl_EnemyController.enemyStates;
    }

    private void InitializeComponents()
    {
        var raySensor = GetComponent<RayPerceptionSensorComponent3D>();
        ConfigureNavMeshAgent();
        playerDetection = new PlayerDetection(raySensor, obstacleMask);
    }

    private void InitializeSystems()
    {
        patrolSystem = new PatrolSystem(FindPatrolPoints());
        agentMovement = new AgentMovement(navAgent, transform, rl_EnemyController.moveSpeed, rl_EnemyController.rotationSpeed, rl_EnemyController.attackRange);
        debugDisplay = new DebugDisplay();
        rewardSystem = new NormalEnemyRewards.RewardSystem(this, enemyRewards);
    }

    private void ConfigureNavMeshAgent()
    {
        if (navAgent != null && rl_EnemyController != null)
        {
            navAgent.speed = rl_EnemyController.moveSpeed;
            navAgent.angularSpeed = rl_EnemyController.rotationSpeed;
            navAgent.stoppingDistance = 0.1f;
            navAgent.isStopped = false;
        }
    }

    private Transform[] FindPatrolPoints()
    {
        var allPoints = Physics.OverlapSphere(transform.position, 50f, LayerMask.GetMask("PatrolPoint"))
            .Where(c => c.CompareTag("Patrol Point"))
            .Select(c => c.transform)
            .ToList();

        var nearbyEnemies = Physics.OverlapSphere(transform.position, 10f, LayerMask.GetMask("Enemy"))
            .Where(c => c.CompareTag("Enemy") && c.gameObject != gameObject)
            .Select(c => c.transform.position);

        var validPoints = allPoints
            .Where(p => !nearbyEnemies.Any(e => Vector3.Distance(p.position, e) < 2f))
            .ToList();

        if (validPoints.Count == 0) validPoints = allPoints;

        return validPoints.OrderBy(x => Random.value).ToArray();
    }

    private void ResetForNewEpisode()
    {
        ResetAgentState();
        if (rl_EnemyController != null && rl_EnemyController.enemyData != null)
        {
            rl_EnemyController.enemyHP = rl_EnemyController.enemyData.enemyHealth;
            rl_EnemyController.InitializeHealthBar();
        }
        if (enemyStates?.HealthState != null)
        {
            enemyStates.HealthState.SetDead(false);
        }
        // Reset combat state properly in the controller's state object
        if (rl_EnemyController != null && rl_EnemyController.enemyStates?.CombatState != null)
        {
            rl_EnemyController.enemyStates.CombatState.ResetCombatState();
        }
        // Reset reward tracking variables
        currentStepCount = 0;   
        currentState = "Idle";
        currentAction = "Idle";
        wasPlayerVisibleLastFrame = false;
        hasPlayerBeenKilled = false;
        attackIncentiveTimer = 0f;
        lastPlayerPosition = Vector3.zero;
        previousDistanceToPlayer = float.MaxValue; // Reset this for new episode
        patrolSystem?.Reset();
        gameObject.SetActive(true);
        var collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = true;
        }
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

    private void WarpToRandomPatrolPoint()
    {
        if (patrolSystem == null) return;

        var patrolPoints = patrolSystem.GetPatrolPoints();
        Vector3 targetPosition = patrolPoints.Length > 0
            ? patrolPoints[Random.Range(0, patrolPoints.Length)].position
            : initialPosition;

        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.Warp(targetPosition);
        }
        else
        {
            transform.position = targetPosition;
        }
    }

    private void ResetTrainingArena()
    {
        FindFirstObjectByType<RL_TrainingTargetSpawner>()?.ResetArena();
    }

    private void CheckEpisodeEnd()
    {
        if (currentStepCount >= MaxStep)
        {
            Debug.Log($"{gameObject.name} Max steps reached. Ending episode.");
            EndEpisode();
        }
    }
    
    #endregion

    #region Action Processing
    private void UpdateStepCount() => currentStepCount++;

    private void ProcessActions(ActionBuffers actions)
    {
        ProcessMovementActions(actions);
        ProcessAttackAction(actions);
    }

    private void ProcessMovementActions(ActionBuffers actions)
    {
        if (agentMovement == null) return;

        float vertical = actions.ContinuousActions[0] - actions.ContinuousActions[1];
        float horizontal = actions.ContinuousActions[2] - actions.ContinuousActions[3];
        float rotation = actions.ContinuousActions[4];

        Vector3 movement = new Vector3(horizontal, 0, vertical);

        if (IsPlayerInAttackRange())
        {
            agentMovement.FaceTarget(playerDetection.GetPlayerPosition());
            movement *= 0.3f;
        }

        agentMovement.ProcessMovement(movement, rotation);
    }

    private void ProcessAttackAction(ActionBuffers actions)
    {
        bool shouldAttack = actions.DiscreteActions[0] == (int)EnemyHighLevelAction.Attack;
        bool playerInRange = IsPlayerInAttackRange();
        bool canAttack = rl_EnemyController?.enemyStates?.CombatState?.CanAttack ?? false;
        bool isAttacking = rl_EnemyController?.enemyStates?.CombatState?.IsAttacking ?? false;

        if (playerInRange && shouldAttack && canAttack && !isAttacking)
        {
            ExecuteAttack();
            currentState = "Attacking";
            currentAction = "Attacking";
        }
        else if (playerInRange && !shouldAttack)
        {
            currentAction = "Chasing";
            currentState = "Chasing";
        }
        else if (!playerInRange)
        {
            currentAction = "Patrolling";
            currentState = "Patrolling";
        }
    }

    private void ExecuteAttack()
    {
        // Validate attack conditions before executing
        if (rl_EnemyController?.enemyStates?.CombatState?.CanAttack != true) return;
        if (rl_EnemyController.enemyStates.CombatState.IsAttacking) return;
        
        attackIncentiveTimer = 0f;
        
        // Store player position for hit detection
        if (playerDetection != null && playerDetection.IsPlayerAvailable())
        {
            lastPlayerPosition = playerDetection.GetPlayerPosition();
        }
        
        currentAction = "Attacking";
        currentState = "Attacking";
        
        // Call the ML attack method
        rl_EnemyController.MLAgentAttack();
    }

    private void ProcessAttackIncentive(float deltaTime)
    {
        if (IsPlayerInAttackRange())
        {
            attackIncentiveTimer += deltaTime;
            
            if (attackIncentiveTimer >= ATTACK_INCENTIVE_THRESHOLD)
            {
                rewardSystem?.AddAttackIncentive(deltaTime);
            }
        }
        else
        {
            attackIncentiveTimer = 0f;
        }
    }

    public void OnAttackHit()
    {
        rewardSystem?.AddAttackReward();
    }

    public void OnAttackMissed()
    {
        rewardSystem?.AddAttackMissedPunishment();
    }

    private void AdjustMovementSpeed(bool playerInRange)
    {
        if (navAgent != null && rl_EnemyController != null)
        {
            navAgent.speed = playerInRange
                ? rl_EnemyController.moveSpeed * 0.2f
                : rl_EnemyController.moveSpeed;
        }
    }

    public void HandleEnemyDeath()
    {
        rewardSystem?.AddDeathPunishment();
        currentState = "Dead";
        currentAction = "Dead";
        Debug.Log($"{gameObject.name} died. Ending episode.");
        EndEpisode();
    }

    public void HandleDamage()
    {
        rewardSystem?.AddDamagePunishment();
        currentState = "Attacking";
        currentAction = "Attacking";
    }

    public void HandlePlayerKilled()
    {
        if (!hasPlayerBeenKilled)
        {
            hasPlayerBeenKilled = true;
            rewardSystem?.AddKillPlayerReward();
            currentState = "Victory";
            currentAction = "Victory";
            Debug.Log($"{gameObject.name} killed the player! Ending episode with victory.");
            EndEpisode();
        }
    }
    #endregion

    #region Reward & Behavior Updates
    private void UpdateBehaviorAndRewards()
    {
        UpdateDetectionAndBehavior();
        ProcessRewards(Time.deltaTime);
        debugDisplay?.UpdateCumulativeReward(GetCumulativeReward());
    }

    private void ProcessRewards(float deltaTime)
    {
        if (rewardSystem == null) return;

        if (currentAction == "Chasing")
        {
            rewardSystem.AddChaseStepReward(deltaTime);
            ProcessChaseRewards(deltaTime);
            ProcessAttackIncentive(deltaTime);
        }
        else if (currentAction.StartsWith("Patrol"))
        {
            rewardSystem.AddPatrolStepReward(deltaTime);
            if (navAgent != null && navAgent.velocity.magnitude < 0.1f)
                rewardSystem.AddNoMovementPunishment(deltaTime);
        }
        else if (currentAction == "Idle")
        {
            rewardSystem.AddIdlePunishment(deltaTime);
        }

        ProcessPlayerVisibilityRewards(deltaTime);
        ProcessDistanceRewards(deltaTime);
    }


    private void ProcessChaseRewards(float deltaTime)
    {
        if (playerDetection != null && playerDetection.IsPlayerAvailable())
        {
            float currentDistance = playerDetection.GetDistanceToPlayer(transform.position);
            
            if (currentDistance < previousDistanceToPlayer - 1f)
            {
                rewardSystem?.AddApproachPlayerReward(deltaTime);
            }
            
            previousDistanceToPlayer = currentDistance;
        }
    }

    private void ProcessPlayerVisibilityRewards(float deltaTime)
    {
        bool isPlayerCurrentlyVisible = playerDetection != null && playerDetection.IsPlayerVisible;
        if (isPlayerCurrentlyVisible && !currentAction.Contains("Detecting") && !currentAction.Contains("Chasing"))
        {
            rewardSystem?.AddDoesntChasePlayerPunishment(deltaTime);
        }
        
        wasPlayerVisibleLastFrame = isPlayerCurrentlyVisible;
    }

    private void ProcessDistanceRewards(float deltaTime)
    {
        if (playerDetection != null && playerDetection.IsPlayerAvailable() &&
            playerDetection.GetDistanceToPlayer(transform.position) > 10f)
            rewardSystem?.AddStayFarFromPlayerPunishment(deltaTime);
    }

    private void UpdateDetectionAndBehavior()
    {
        if (playerDetection == null) return;

        playerDetection.UpdatePlayerDetection(transform.position);

        if (playerDetection.IsPlayerVisible)
        {
            rewardSystem?.AddDetectionReward(Time.deltaTime);
            
            // Set proper states for chasing
            if (IsPlayerInAttackRange())
            {
                currentState = "In Attack Range";
                currentAction = "Preparing Attack";
            }
            else
            {
                currentState = "Detecting";
                currentAction = "Chasing";
            }
            
            // Update enemy states for proper behavior
            enemyStates.IsDetecting = true;
            enemyStates.IsChasing = !IsPlayerInAttackRange();
        }
        else
        {
            enemyStates.IsDetecting = false;
            enemyStates.IsChasing = false;
            UpdatePatrolBehavior();
        }

        // Check for patrol completion
        if (patrolSystem != null)
        {
            patrolSystem.UpdatePatrol(transform.position, rewardSystem);
        }
    }

    private void HandlePatrolPointReached()
    {
        rewardSystem?.AddPatrolReward();
        Debug.Log($"{gameObject.name} completed patrol point!");
    }

    private void UpdatePatrolBehavior()
    {
        if (patrolSystem == null || navAgent == null) return;

        string nearestPoint = patrolSystem.GetNearestPointName(transform.position);
        bool isMoving = navAgent.velocity.magnitude > 0.1f;

        currentAction = isMoving ? "Patroling" : "Idle";

        if (isMoving)
        {
            currentState = navAgent.hasPath && navAgent.remainingDistance > navAgent.stoppingDistance
                ? "Pathfinding to " + nearestPoint
                : "Patroling near " + nearestPoint;
        }
        else
        {
            currentState = "Position in Arena: " + nearestPoint;
        }
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
        playerDetection != null && playerDetection.IsPlayerAvailable() &&
        agentMovement != null && agentMovement.IsPlayerInAttackRange(playerDetection.GetPlayerPosition());

    public void SetPatrolPoints(Transform[] points) => patrolSystem?.SetPatrolPoints(points);

    void OnGUI()
    {
        if (showDebugInfo && debugDisplay != null && patrolSystem != null)
            debugDisplay.DisplayDebugInfo(gameObject.name, currentState, currentAction, debugTextOffset, debugTextColor, debugFontSize, patrolSystem.PatrolLoopsCompleted);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (IsObstacleCollision(collision))
            rewardSystem?.AddObstaclePunishment();
    }
    #endregion
}