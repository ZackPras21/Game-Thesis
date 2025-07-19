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
    [SerializeField] private NormalEnemyRewards rewardConfig;
    [SerializeField] private NormalEnemyStates stateConfig; // Unused, consider removal if not implemented
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
    public static bool TrainingActive = true; // Consider if this should be static or managed by a training manager
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

    private const float HEALTH_NORMALIZATION_FACTOR = 100f; // Used for observation normalization
    private const float ATTACK_COOLDOWN_FALLBACK = 0.5f; // Fallback if attack animation length is not found

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
        // Ensure RL_EnemyController reference exists
        rl_EnemyController ??= GetComponent<RL_EnemyController>();
        if (rl_EnemyController == null)
        {
            Debug.LogError("NormalEnemyAgent: RL_EnemyController component is missing!", gameObject);
            enabled = false;
            return;
        }

        // Ensure NavMeshAgent reference exists
        navAgent ??= GetComponent<NavMeshAgent>();
        if (navAgent == null)
        {
            Debug.LogError("NormalEnemyAgent: NavMeshAgent component is missing!", gameObject);
            enabled = false;
            return;
        }

        // Force initialize RL_EnemyController if it hasn't been already
        if (!rl_EnemyController.IsInitialized)
        {
            rl_EnemyController.ForceInitialize();
        }

        // Check enemyData after initialization
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
    #endregion

    #region Initialization & Reset Helpers
    private void InitializeComponents()
    {
        // NavMeshAgent and RL_EnemyController are already ensured by RequireComponent and checks in Initialize()
        var raySensor = GetComponent<RayPerceptionSensorComponent3D>();
        ConfigureNavMeshAgent();
        playerDetection = new PlayerDetection(raySensor, obstacleMask);
    }

    private void InitializeSystems()
    {
        patrolSystem = new PatrolSystem(FindPatrolPoints());
        agentMovement = new AgentMovement(navAgent, transform, rl_EnemyController.moveSpeed, rl_EnemyController.rotationSpeed, rl_EnemyController.attackRange);
        debugDisplay = new DebugDisplay();
        rewardConfig = new NormalEnemyRewards();
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
        // This logic might need to be more robust for a real training environment
        // e.g., getting points from a dedicated manager or a specific parent object.
        var allPoints = Physics.OverlapSphere(transform.position, 50f, LayerMask.GetMask("PatrolPoint")) // Assuming a "PatrolPoint" layer
            .Where(c => c.CompareTag("Patrol Point"))
            .Select(c => c.transform)
            .ToList();

        // Filter out points too close to other enemies (if applicable)
        var nearbyEnemies = Physics.OverlapSphere(transform.position, 10f, LayerMask.GetMask("Enemy"))
            .Where(c => c.CompareTag("Enemy") && c.gameObject != gameObject)
            .Select(c => c.transform.position);

        var validPoints = allPoints
            .Where(p => !nearbyEnemies.Any(e => Vector3.Distance(p.position, e) < 2f))
            .ToList();

        if (validPoints.Count == 0) validPoints = allPoints; // Fallback if no valid points after filtering

        return validPoints.OrderBy(x => Random.value).ToArray();
    }

    private void ResetForNewEpisode()
    {
        ResetAgentState();

        // Reinitialize RL_EnemyController's health and state
        rl_EnemyController.enemyHP = rl_EnemyController.enemyData.enemyHealth;
        rl_EnemyController.healthState.SetDead(false);
        rl_EnemyController.InitializeHealthBar(); // Update health bar display

        currentState = "Idle";
        currentAction = "Idle";
        lastAttackTime = Time.fixedTime - dynamicAttackCooldown; // Allow immediate attack

        // Reset patrol loop counter
        patrolSystem?.Reset();

        gameObject.SetActive(true); // Ensure agent is active
        GetComponent<Collider>().enabled = true; // Re-enable collider
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
        Vector3 targetPosition = patrolPoints.Length > 0
            ? patrolPoints[Random.Range(0, patrolPoints.Length)].position
            : initialPosition; // Fallback to initial position

        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.Warp(targetPosition);
        }
        else
        {
            transform.position = targetPosition; // Direct teleport if NavMeshAgent is not ready
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
        // Get attack animation length if available for dynamic cooldown
        if (animator != null && attackAnimation == null)
        {
            // This assumes a specific animation state named "Attack"
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
        if (currentAction == "Chasing")
        {
            rewardConfig.AddChaseStepReward(this, Time.deltaTime);
            ProcessChaseRewards(deltaTime);
        }
        else if (currentAction.StartsWith("Patrol"))
        {
            rewardConfig.AddPatrolStepReward(this, Time.deltaTime);
            if (navAgent.velocity.magnitude < 0.1f)
                rewardConfig.AddNoMovementPunishment(this, Time.deltaTime);
        }
        else if (currentAction == "Idle")
        {
            rewardConfig.AddIdlePunishment(this, Time.deltaTime);
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
            currentAction = "Detecting";
        }
        else
        {
            UpdatePatrolBehavior();
        }

        patrolSystem.UpdatePatrol(transform.position, rewardConfig, this);
    }

    private void UpdatePatrolBehavior()
    {
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

    #region Episode Management
    private void CheckEpisodeEnd()
    {
        if (StepCount >= MaxStep)
        {
            Debug.Log($"{gameObject.name} Max steps reached. Ending episode.");
            EndEpisode();
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
