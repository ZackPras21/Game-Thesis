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
        WarpToRandomPatrolPoint();
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
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (!isInitialized || rl_EnemyController == null) return;

        sensor.AddObservation(CurrentHealth / MaxHealth); // Normalized health
        sensor.AddObservation(playerDetection.IsPlayerAvailable()
            ? playerDetection.GetDistanceToPlayer(transform.position) / HEALTH_NORMALIZATION_FACTOR: 0f); 

        Vector3 localVelocity = transform.InverseTransformDirection(navAgent.velocity);
        sensor.AddObservation(localVelocity.x / rl_EnemyController.moveSpeed);
        sensor.AddObservation(localVelocity.z / rl_EnemyController.moveSpeed);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!isInitialized || rl_EnemyController == null || IsDead || !isActiveAndEnabled) return;

        debugDisplay.IncrementSteps();
        ProcessActions(actions);
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
        // Try to get patrol points from the spawner for this specific agent
        var spawner = FindFirstObjectByType<RL_TrainingEnemySpawner>();
        if (spawner != null)
        {
            Transform parentTransform = transform.parent;
            if (parentTransform != null)
            {
                Transform[] arenaPatrolPoints = spawner.GetArenaPatrolPoints(parentTransform);
                if (arenaPatrolPoints != null && arenaPatrolPoints.Length > 0)
                {
                    // Sort patrol points by name to ensure consistent order (A->B->C->D)
                    System.Array.Sort(arenaPatrolPoints, (x, y) => string.Compare(x.name, y.name));
                    return arenaPatrolPoints;
                }
            }
        }

        // Fallback: Find nearby patrol points and sort them
        var nearbyPoints = Physics.OverlapSphere(transform.position, 30f, LayerMask.GetMask("Ground"))
            .Where(c => c.CompareTag("Patrol Point"))
            .Select(c => c.transform)
            .OrderBy(p => p.name) 
            .Take(4)
            .ToArray();

        return nearbyPoints.Length > 0 ? nearbyPoints : new Transform[0];
    }
    
    private Transform[] GetPatrolPointsFromArena(Transform arenaParent)
    {
        // Look for patrol points as children of the arena parent
        List<Transform> patrolPoints = new List<Transform>();
        
        foreach (Transform child in arenaParent)
        {
            if (child.CompareTag("Patrol Point") || child.name.Contains("Patrol Point"))
            {
                patrolPoints.Add(child);
            }
        }

        return patrolPoints.OrderBy(p => p.name).ToArray();
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

    private void WarpToRandomPatrolPoint()
    {
        var patrolPoints = patrolSystem.GetPatrolPoints();
        if (patrolPoints.Length == 0)
        {
            Debug.LogWarning($"{gameObject.name} has no patrol points for warping!");
            return;
        }

        // Always start from first patrol point (A) for consistency
        Vector3 startPosition = patrolPoints[0].position;
        
        if (navAgent != null && navAgent.enabled)
        {
            navAgent.enabled = false; // Disable temporarily for warping
            transform.position = startPosition;
            navAgent.enabled = true;
            
            if (navAgent.isOnNavMesh)
            {
                navAgent.Warp(startPosition);
            }
        }
        else
        {
            transform.position = startPosition;
        }
        
        // Reset patrol system to start from first point
        patrolSystem.ResetToFirstPoint();
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
        float vertical = actions.ContinuousActions[0] - actions.ContinuousActions[1]; // UP - DOWN
        float horizontal = actions.ContinuousActions[2] - actions.ContinuousActions[3]; // RIGHT - LEFT
        float rotation = actions.ContinuousActions[4]; // ROTATE

        Vector3 movement = new Vector3(horizontal, 0, vertical);

        if (IsPlayerInAttackRange())
        {
            agentMovement.FaceTarget(playerDetection.GetPlayerPosition());
            movement *= 0.3f; // Slow down when in attack range
        }

        agentMovement.ProcessMovement(movement, rotation);
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

        bool isMoving = navAgent.velocity.magnitude > 0.1f && !rl_EnemyController.combatState.IsAttacking;
        bool shouldAttackAnim = playerInRange && canAttack; // Use a separate bool for animation trigger

        animator.SetBool("isWalking", isMoving);
        animator.SetBool("isAttacking", shouldAttackAnim);
        animator.SetBool("isIdle", !isMoving && !shouldAttackAnim);
        currentState = "Attacking";
        currentAction = "Attacking";

        // Sync animation speed with movement
        if (animator.GetBool("isWalking"))
        {
            animator.speed = Mathf.Clamp(navAgent.velocity.magnitude / rl_EnemyController.moveSpeed, 0.5f, 1.5f);
        }
        else
        {
            animator.speed = 1f;
        }

        // Force attack animation to play if shouldAttackAnim is true and not already playing
        if (shouldAttackAnim && !animator.GetCurrentAnimatorStateInfo(0).IsName("Attack"))
        {
            animator.Play("Attack", 0, 0f);
        }
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
        // Check if agent is too close to walls/obstacles
        if (IsNearObstacle())
        {
            rewardConfig.AddObstaclePunishment(this);
        }

        if (currentAction == "Chasing")
        {
            rewardConfig.AddChaseStepReward(this, deltaTime);
            ProcessChaseRewards(deltaTime);
        }
        else if (currentAction == "Patroling")
        {
            rewardConfig.AddPatrolStepReward(this, deltaTime);
            
            // Additional reward for maintaining good distance from walls during patrol
            if (!IsNearObstacle())
            {
                AddReward(0.001f); // Small bonus for staying away from walls
            }
        }
        else if (currentAction == "Idling")
        {
            // Don't punish idling during patrol breaks
            if (!patrolSystem.IsIdlingAtSpawn())
            {
                rewardConfig.AddIdlePunishment(this, deltaTime);
            }
        }

        ProcessPlayerVisibilityRewards(deltaTime);
        ProcessDistanceRewards(deltaTime);
    }

    private bool IsNearObstacle()
    {
        return Physics.CheckSphere(transform.position, 1.2f, obstacleMask);
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
            currentAction = "Detecting";
        }
        else
        {
            UpdatePatrolBehavior();
        }

        //patrolSystem.patrol(transform.position, rewardConfig, this); fix this or implement it 
    }

    private void UpdatePatrolBehavior()
    {
        if (!patrolSystem.HasValidPatrolPoints())
        {
            currentAction = "Idle";
            currentState = "No Patrol Points";
            rewardConfig.AddIdlePunishment(this, Time.deltaTime);
            return;
        }

        // Handle idling state
        if (patrolSystem.IsIdlingAtSpawn())
        {
            currentAction = "Idling";
            currentState = $"Idling at {patrolSystem.GetCurrentPatrolPointName()} ({patrolSystem.GetIdleTimeRemaining():F1}s remaining)";
            
            // Stop movement during idle
            if (navAgent != null && navAgent.enabled)
            {
                navAgent.isStopped = true;
            }
            
            // Update idle timer and check if idle period is complete
            bool idleComplete = patrolSystem.UpdateIdleTimer();
            if (idleComplete)
            {
                currentAction = "Patroling";
                if (navAgent != null && navAgent.enabled)
                {
                    navAgent.isStopped = false;
                }
            }
            return;
        }

        // Get current patrol target and move towards it
        Vector3 currentTarget = patrolSystem.GetCurrentPatrolTarget();
        float distanceToTarget = Vector3.Distance(transform.position, currentTarget);
        
        // Check if we've reached the waypoint
        if (distanceToTarget < 2f) // Patrol waypoint tolerance
        {
            bool completedLoop = patrolSystem.AdvanceToNextWaypoint();
            
            if (completedLoop)
            {
                rewardConfig.AddPatrolReward(this);
                Debug.Log($"{gameObject.name} completed patrol loop {patrolSystem.PatrolLoopsCompleted}");
                
                // Check if we've completed required loops
                if (patrolSystem.PatrolLoopsCompleted >= 2)
                {
                    rewardConfig.AddPatrolReward(this);
                    Debug.Log($"{gameObject.name} completed 2 patrol loops. Ending episode.");
                    EndEpisode();
                    return;
                }
            }
            
            currentAction = patrolSystem.IsIdlingAtSpawn() ? "Idling" : "Patroling";
            currentState = patrolSystem.IsIdlingAtSpawn() ? 
                $"Starting idle at {patrolSystem.GetCurrentPatrolPointName()}" : 
                $"Reached {patrolSystem.GetCurrentPatrolPointName()}, moving to next";
        }
        else
        {
            // Move towards current patrol target
            agentMovement.MoveToTarget(currentTarget);
            currentAction = "Patroling";
            currentState = $"Moving to {patrolSystem.GetCurrentPatrolPointName()} (dist: {distanceToTarget:F1}m)";
            rewardConfig.AddPatrolStepReward(this, Time.deltaTime);
            
            // Add punishment if agent is not moving towards target
            if (navAgent != null && navAgent.velocity.magnitude < 0.1f)
            {
                rewardConfig.AddNoMovementPunishment(this, Time.deltaTime);
            }
        }
    }
    
    public void HandleEnemyDeath()
    {
        rewardConfig.AddDeathPunishment(this);
        currentState = "Dead";
        currentAction = "Dead";
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

    void OnCollisionEnter(Collision collision)
    {
        if (IsObstacleCollision(collision))
            rewardConfig.AddObstaclePunishment(this);
    }
    #endregion
}

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

    public void DisplayDebugInfo(string agentName, string currentState, string currentAction, Vector2 offset, Color textColor, int fontSize, int patrolLoops)
    {
        var labelStyle = new GUIStyle
        {
            fontSize = fontSize,
            normal = { textColor = textColor }
        };

        string debugText = $"{agentName}:\nState: {currentState}\nAction: {currentAction}\nSteps: {episodeSteps}\nCumulative Reward: {cumulativeReward:F3}\nPatrol Loops: {patrolLoops}";
        GUI.Label(new Rect(offset.x, offset.y, 300, 150), debugText, labelStyle);
    }
}
#endregion
