using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine.AI;
using System.Linq;
using static NormalEnemyActions;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody), typeof(RayPerceptionSensorComponent3D), typeof(RL_EnemyController))]
public class NormalEnemyAgent : Agent
{
    #region Serialized Fields
    [Header("References")]
    [SerializeField] private RL_EnemyController rl_EnemyController;
    [SerializeField] private NormalEnemyRewards rewardConfig;
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody agentRigidbody;

    [Header("Movement Configuration")]
    [SerializeField] public float moveSpeed = 3.5f;
    [SerializeField] public float rotationSpeed = 120f;

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
    private PlayerDetection playerDetection;
    private PatrolSystem patrolSystem;
    private RLMovementController movementController;
    private DebugDisplay debugDisplay;
    
    private const float DISTANCE_NORMALIZATION_FACTOR = 50f;
    private const float VELOCITY_NORMALIZATION_FACTOR = 10f;
    private const float STUCK_THRESHOLD = 0.1f;
    private const float STUCK_TIME_LIMIT = 2f;
    
    private Vector3 initialPosition;
    private string currentState = "Idle";
    private string currentAction = "Idle";
    private float previousDistanceToPlayer = float.MaxValue;
    private float lastAttackTime;
    private bool isInitialized = false;
    private bool hasObstaclePunishmentThisFrame = false;
    private Vector3 lastPosition;
    private float stuckTimer = 0f;
    #endregion

    #region Agent Lifecycle
    public override void Initialize()
    {
        rl_EnemyController ??= GetComponent<RL_EnemyController>();
        agentRigidbody ??= GetComponent<Rigidbody>();
        
        if (rl_EnemyController == null || agentRigidbody == null)
        {
            Debug.LogError("NormalEnemyAgent: Missing required components!", gameObject);
            enabled = false;
            return;
        }

        if (!rl_EnemyController.IsInitialized)
        {
            rl_EnemyController.ForceInitialize();
        }

        InitializeComponents();
        InitializeSystems();
        ConfigureRigidbody();
        
        initialPosition = transform.position;
        lastPosition = transform.position;
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
        agentRigidbody.linearVelocity = Vector3.zero;
        agentRigidbody.angularVelocity = Vector3.zero;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (!isInitialized || rl_EnemyController == null) return;
        
        // Health observation (1)
        sensor.AddObservation(CurrentHealth / MaxHealth);
        
        // Player-related observations (4)
        bool playerAvailable = playerDetection.IsPlayerAvailable();
        sensor.AddObservation(playerAvailable ? 1f : 0f);
        
        if (playerAvailable)
        {
            Vector3 playerPos = playerDetection.GetPlayerPosition();
            Vector3 directionToPlayer = (playerPos - transform.position).normalized;
            float distanceToPlayer = playerDetection.GetDistanceToPlayer(transform.position);
            
            Vector3 localPlayerDirection = transform.InverseTransformDirection(directionToPlayer);
            sensor.AddObservation(localPlayerDirection.x);
            sensor.AddObservation(localPlayerDirection.z);
            sensor.AddObservation(distanceToPlayer / DISTANCE_NORMALIZATION_FACTOR);
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(1f);
        }
        
        // Agent velocity (local space) (2)
        Vector3 localVelocity = transform.InverseTransformDirection(agentRigidbody.linearVelocity);
        sensor.AddObservation(localVelocity.x / VELOCITY_NORMALIZATION_FACTOR);
        sensor.AddObservation(localVelocity.z / VELOCITY_NORMALIZATION_FACTOR);
        
        // State observations (3)
        sensor.AddObservation(IsAgentKnockedBack() ? 1f : 0f);
        sensor.AddObservation(IsAgentFleeing() ? 1f : 0f);
        sensor.AddObservation(ShouldAgentFlee() ? 1f : 0f);
        
        // Patrol-related observations (3)
        if (patrolSystem.HasValidPatrolPoints())
        {
            Vector3 patrolTarget = patrolSystem.GetCurrentPatrolTarget();
            Vector3 directionToPatrol = (patrolTarget - transform.position).normalized;
            Vector3 localPatrolDirection = transform.InverseTransformDirection(directionToPatrol);
            
            sensor.AddObservation(localPatrolDirection.x);
            sensor.AddObservation(localPatrolDirection.z);
            sensor.AddObservation(Vector3.Distance(transform.position, patrolTarget) / DISTANCE_NORMALIZATION_FACTOR);
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }
        
        // Total: 13 observations
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!isInitialized || rl_EnemyController == null || IsDead || !isActiveAndEnabled) return;

        debugDisplay.IncrementSteps();
        ProcessActions(actions);
        UpdateBehaviorAndRewards();
        CheckStuckState();
        CheckEpisodeEnd();
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        var discreteActions = actionsOut.DiscreteActions;

        continuousActions[0] = Input.GetAxis("Vertical");
        continuousActions[1] = Input.GetAxis("Horizontal");
        continuousActions[2] = GetRotationInputHeuristic();
        discreteActions[0] = Input.GetKey(KeyCode.Space) ? 1 : 0;
    }

    private void CheckEpisodeEnd()
    {
        if (StepCount >= MaxStep)
        {
            Debug.Log($"{gameObject.name} Max steps reached. Ending episode.");
            EndEpisode();
        }
        
        if (patrolSystem.PatrolLoopsCompleted >= 3)
        {
            rewardConfig.AddPatrolReward(this);
            Debug.Log($"{gameObject.name} completed 3 patrol loops. Ending episode.");
            EndEpisode();
        }
    }
    #endregion

    #region Initialization & Reset Helpers
    private void InitializeComponents()
    {
        var raySensor = GetComponent<RayPerceptionSensorComponent3D>();
        playerDetection = new PlayerDetection(raySensor, LayerMask.GetMask("Wall", "Environment", "Gate", "Box", "Enemy"));
    }

    private void InitializeSystems()
    {
        Transform[] patrolPoints = FindPatrolPoints();
        patrolSystem = new PatrolSystem(patrolPoints);
        movementController = new RLMovementController(agentRigidbody, transform, moveSpeed, rotationSpeed);
        debugDisplay = new DebugDisplay();
        
        if (patrolPoints.Length > 0)
        {
            Debug.Log($"{gameObject.name} initialized with {patrolPoints.Length} patrol points");
        }
        else
        {
            Debug.LogWarning($"{gameObject.name} has no patrol points assigned!");
        }
    }

    private void ConfigureRigidbody()
    {
        if (agentRigidbody == null) return;

        agentRigidbody.mass = 1f;
        agentRigidbody.linearDamping = 1f; // Reduced from 2f
        agentRigidbody.angularDamping = 3f; // Reduced from 5f
        agentRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        agentRigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
        agentRigidbody.constraints = RigidbodyConstraints.FreezeRotationX | 
                                    RigidbodyConstraints.FreezeRotationZ | 
                                    RigidbodyConstraints.FreezePositionY;
    }


    private Transform[] FindPatrolPoints()
    {
        var spawner = FindFirstObjectByType<RL_TrainingEnemySpawner>();
        if (spawner != null)
        {
            Transform parentTransform = transform.parent;
            if (parentTransform != null)
            {
                Transform[] arenaPatrolPoints = spawner.GetArenaPatrolPoints(parentTransform);
                if (arenaPatrolPoints != null && arenaPatrolPoints.Length > 0)
                {
                    System.Array.Sort(arenaPatrolPoints, (x, y) => string.Compare(x.name, y.name));
                    return arenaPatrolPoints;
                }
            }
        }

        var nearbyPoints = Physics.OverlapSphere(transform.position, 30f, LayerMask.GetMask("Ground"))
            .Where(c => c.CompareTag("Patrol Point"))
            .Select(c => c.transform)
            .OrderBy(p => p.name) 
            .Take(4)
            .ToArray();

        return nearbyPoints.Length > 0 ? nearbyPoints : new Transform[0];
    }

    private void ResetForNewEpisode()
    {
        ResetAgentState();
        rl_EnemyController.enemyHP = rl_EnemyController.enemyData.enemyHealth;
        rl_EnemyController.healthState.ResetHealthState();
        rl_EnemyController.InitializeHealthBar();
        rl_EnemyController.fleeState?.Reset();

        currentState = "Idle";
        currentAction = "Idle";
        lastAttackTime = Time.fixedTime - 2f;
        stuckTimer = 0f;
        lastPosition = transform.position;

        patrolSystem?.Reset();
        movementController?.Reset();
        gameObject.SetActive(true);
        GetComponent<Collider>().enabled = true;
    }

    private void ResetAgentState()
    {
        playerDetection?.Reset();
        patrolSystem?.Reset();
        debugDisplay?.Reset();
        movementController?.Reset();
        
        if (agentRigidbody != null)
        {
            agentRigidbody.linearVelocity = Vector3.zero;
            agentRigidbody.angularVelocity = Vector3.zero;
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
        
        Vector2 randomOffset = Random.insideUnitCircle * 2f;
        respawnPosition += new Vector3(randomOffset.x, 0, randomOffset.y);
        
        transform.position = respawnPosition;
        agentRigidbody.linearVelocity = Vector3.zero;
        agentRigidbody.angularVelocity = Vector3.zero;
        
        patrolSystem.ResetToSpecificPoint(randomIndex);
        lastPosition = respawnPosition;
        
        Debug.Log($"{gameObject.name} respawned at {patrolPoints[randomIndex].name}");
    }

    private void ResetTrainingArena()
    {
        FindFirstObjectByType<RL_TrainingTargetSpawner>()?.ResetArena();
    }
    #endregion

    #region Action Processing
    private void ProcessActions(ActionBuffers actions)
    {
        if (!isInitialized || IsDead || !isActiveAndEnabled) return;

        float forward = actions.ContinuousActions[0];
        float right = actions.ContinuousActions[1];
        float rotation = actions.ContinuousActions[2];
        bool shouldAttack = actions.DiscreteActions[0] == 1;

        // Scale down movement when near obstacles
        if (IsNearObstacle())
        {
            forward *= 0.5f;
            right *= 0.5f;
        }

        // Handle different behavioral states with collision awareness
        if (IsAgentKnockedBack())
        {
            HandleKnockbackState();
        }
        else if (IsAgentFleeing())
        {
            HandleFleeingState();
        }
        else if (playerDetection.IsPlayerVisible)
        {
            HandleChaseState(forward, right, rotation, shouldAttack);
        }
        else
        {
            HandlePatrolState(forward, right, rotation);
        }

        UpdateMovementAnimation();
        debugDisplay.IncrementSteps();
        UpdateBehaviorAndRewards();
        CheckStuckState();
        CheckEpisodeEnd();
    }

    private bool IsNearObstacle()
    {
        // Check for nearby obstacles using multiple raycasts
        Vector3[] directions = {
            transform.forward,
            transform.forward + transform.right,
            transform.forward - transform.right,
            transform.right,
            -transform.right
        };

        foreach (Vector3 dir in directions)
        {
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, dir, out _, 1.0f, 
                LayerMask.GetMask("Wall", "Obstacle", "Enemy")))
            {
                return true;
            }
        }
        return false;
    }

    private void HandleKnockbackState()
    {
        currentState = "Knocked Back";
        currentAction = "Recovering";
        // Let physics handle knockback - no additional movement
    }

    private void HandleFleeingState()
    {
        currentState = "Fleeing";
        currentAction = "Fleeing";
        
        if (playerDetection.IsPlayerAvailable())
        {
            Vector3 fleeDirection = (transform.position - playerDetection.GetPlayerPosition()).normalized;
            movementController.ApplyFleeMovement(fleeDirection);
        }
    }

    private void HandleChaseState(float forward, float right, float rotation, bool shouldAttack)
    {
        currentState = "Chasing";
        currentAction = "Chasing";

        // Face the player
        Vector3 playerPos = playerDetection.GetPlayerPosition();
        movementController.FaceTarget(playerPos);

        // Check if player is in attack range
        bool playerInRange = IsPlayerInAttackRange();
        
        if (playerInRange)
        {
            movementController.FaceTarget(playerPos);
            ProcessAttackAction(shouldAttack);
        }
        else
        {
            // Move towards player using RL actions
            Vector3 directionToPlayer = (playerPos - transform.position).normalized;
            Vector3 localDirection = transform.InverseTransformDirection(directionToPlayer);
            
            // Blend RL actions with chase behavior
            float chaseForward = Mathf.Max(forward, localDirection.z * 0.8f);
            float chaseRight = right + localDirection.x * 0.3f;
            float rotationInput = playerInRange || rl_EnemyController.combatState.IsAttacking ? 0f : rotation;
            
            movementController.ProcessMovement(chaseForward, chaseRight, rotationInput);
            rewardConfig.AddApproachPlayerReward(this, Time.deltaTime);
        }
    }

    private void HandlePatrolState(float forward, float right, float rotation)
    {
        currentState = "Patrolling";
        currentAction = "Patrolling";

        if (patrolSystem.HasValidPatrolPoints())
        {
            Vector3 currentTarget = patrolSystem.GetCurrentPatrolTarget();
            float distanceToTarget = Vector3.Distance(transform.position, currentTarget);
            
            // Check if reached waypoint
            if (distanceToTarget < 2f)
            {
                bool completedLoop = patrolSystem.AdvanceToNextWaypoint();
                if (completedLoop)
                {
                    rewardConfig.AddPatrolReward(this);
                    Debug.Log($"{gameObject.name} completed patrol loop {patrolSystem.PatrolLoopsCompleted}");
                }
                
                currentAction = patrolSystem.IsIdlingAtSpawn() ? "Idling" : "Patrolling";
            }
            else
            {
                // Move towards patrol target using RL actions
                Vector3 directionToTarget = (currentTarget - transform.position).normalized;
                Vector3 localDirection = transform.InverseTransformDirection(directionToTarget);
                
                // Blend RL actions with patrol behavior
                float patrolForward = Mathf.Max(forward, localDirection.z * 0.6f);
                float patrolRight = right + localDirection.x * 0.4f;
                
                movementController.ProcessMovement(patrolForward, patrolRight, rotation);
                rewardConfig.AddPatrolStepReward(this, Time.deltaTime);
            }

            // Handle idling at spawn
            if (patrolSystem.IsIdlingAtSpawn())
            {
                bool idleComplete = patrolSystem.UpdateIdleTimer();
                if (idleComplete)
                {
                    currentAction = "Patrolling";
                }
            }
        }
        else
        {
            // No patrol points - apply basic RL movement
            movementController.ProcessMovement(forward, right, rotation);
            currentAction = "Wandering";
        }
    }

    private void ProcessAttackAction(bool shouldAttack)
    {
        bool canAttack = Time.fixedTime - lastAttackTime >= 2f;
        bool shouldAttackAnim = canAttack && rl_EnemyController.combatState.IsAttacking;

        if (shouldAttack && canAttack)
        {
            ExecuteAttack();
        }
        else if (shouldAttackAnim && !animator.GetCurrentAnimatorStateInfo(0).IsName("Attack"))
        {
            animator.Play("Attack", 0, 0f);
        }
    }

    private void ExecuteAttack()
    {
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

    private void UpdateMovementAnimation()
    {
        if (animator == null || IsDead) return;

        bool isMoving = agentRigidbody.linearVelocity.sqrMagnitude > 0.01f;
        bool isAttacking = rl_EnemyController.combatState.IsAttacking;

        animator.SetBool("isWalking", isMoving && !isAttacking);
        animator.SetBool("isAttacking", isAttacking);
        animator.SetBool("isIdle", !isMoving && !isAttacking);
    }

    private void CheckStuckState()
    {
        float distanceMoved = Vector3.Distance(transform.position, lastPosition);
        
        if (distanceMoved < STUCK_THRESHOLD)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer >= STUCK_TIME_LIMIT)
            {
                AddReward(-0.1f);
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
        }
        
        lastPosition = transform.position;
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
                rewardConfig.AddFleeReward(this, deltaTime);
            else
                rewardConfig.AddFleeingPunishment(this, deltaTime);
            return;
        }

        if (IsNearObstacle())
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
        }
        else if (currentAction == "Idling")
        {
            if (!patrolSystem.IsIdlingAtSpawn() && agentRigidbody.linearVelocity.magnitude < 0.1f)
            {
                rewardConfig.AddIdlePunishment(this, deltaTime);
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
        }
    }

    public void HandleEnemyDeath()
    {
        rewardConfig.AddDeathPunishment(this);
        currentState = "Dead";
        currentAction = "Dead";
        
        agentRigidbody.linearVelocity = Vector3.zero;
        agentRigidbody.angularVelocity = Vector3.zero;
        
        Debug.Log($"{gameObject.name} died. Ending episode.");
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
    #endregion

    #region Utility & Debug
    private float GetRotationInputHeuristic()
    {
        if (Input.GetKey(KeyCode.Q)) return -1f;
        if (Input.GetKey(KeyCode.E)) return 1f;
        return 0f;
    }

    private bool IsPlayerInAttackRange() =>
        playerDetection.IsPlayerAvailable() &&
        Vector3.Distance(transform.position, playerDetection.GetPlayerPosition()) <= rl_EnemyController.attackRange;

    private bool IsAgentKnockedBack() => rl_EnemyController.IsKnockedBack();
    private bool IsAgentFleeing() => rl_EnemyController.IsFleeing();
    private bool ShouldAgentFlee() => rl_EnemyController.IsHealthLow() && playerDetection.IsPlayerAvailable();

    public void SetPatrolPoints(Transform[] points) => patrolSystem?.SetPatrolPoints(points);

    void OnGUI()
    {
        if (showDebugInfo)
            debugDisplay.DisplayDebugInfo(gameObject.name, currentState, currentAction, debugTextOffset, debugTextColor, debugFontSize, patrolSystem.PatrolLoopsCompleted);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (((1 << collision.gameObject.layer) & LayerMask.GetMask("Wall", "Obstacle")) != 0)
        {
            rewardConfig.AddObstaclePunishment(this);
        }
    }
    #endregion
}

#region Helper Classes
public class RLMovementController
{
    private readonly Rigidbody agentRigidbody;
    private readonly Transform agentTransform;
    private readonly float moveSpeed;
    private readonly float rotationSpeed;
    private readonly float maxVelocity;

    public RLMovementController(Rigidbody rigidbody, Transform transform, float moveSpeed, float rotationSpeed)
    {
        this.agentRigidbody = rigidbody;
        this.agentTransform = transform;
        this.moveSpeed = moveSpeed;
        this.rotationSpeed = rotationSpeed;
        this.maxVelocity = moveSpeed * 2f;
    }

    public void Reset()
    {
        if (agentRigidbody != null)
        {
            agentRigidbody.linearVelocity = Vector3.zero;
            agentRigidbody.angularVelocity = Vector3.zero;
        }
    }

    public void ProcessMovement(float forward, float right, float rotation)
    {

        Vector3 movement = agentTransform.forward * forward + agentTransform.right * right;
        movement = Vector3.ClampMagnitude(movement, 1f);
        
        Vector3 force = movement * moveSpeed;
        agentRigidbody.AddForce(force, ForceMode.Force);
        
        // Limit velocity
        if (agentRigidbody.linearVelocity.magnitude > maxVelocity)
        {
            agentRigidbody.linearVelocity = agentRigidbody.linearVelocity.normalized * maxVelocity;
        }
        
        // Apply rotation
        if (Mathf.Abs(rotation) > 0.1f)
        {
            float torque = rotation * rotationSpeed;
            agentRigidbody.AddTorque(0, torque, 0, ForceMode.Force);
        }
    }

    public void FaceTarget(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - agentTransform.position);
        direction.y = 0;
        
        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            agentTransform.rotation = Quaternion.Slerp(
                agentTransform.rotation,
                targetRotation,
                rotationSpeed * Time.fixedDeltaTime * 0.1f
            );
        }
    }

    public void ApplyFleeMovement(Vector3 fleeDirection)
    {
        Vector3 force = fleeDirection * moveSpeed * 1.5f;
        agentRigidbody.AddForce(force, ForceMode.Force);
        
        if (agentRigidbody.linearVelocity.magnitude > maxVelocity * 1.2f)
        {
            agentRigidbody.linearVelocity = agentRigidbody.linearVelocity.normalized * maxVelocity * 1.2f;
        }
    }
}

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