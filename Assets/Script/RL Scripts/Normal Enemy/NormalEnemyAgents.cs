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
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float rotationSpeed = 120f;
    [SerializeField] private float maxVelocity = 10f;

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
    private RLMovementController movementController;
    private DebugDisplay debugDisplay;
    
    private const float DISTANCE_NORMALIZATION_FACTOR = 50f;
    private const float VELOCITY_NORMALIZATION_FACTOR = 10f;
    
    private Vector3 initialPosition;
    private string currentState = "Idle";
    private string currentAction = "Idle";
    private float previousDistanceToPlayer = float.MaxValue;
    private float lastAttackTime;
    private bool isInitialized = false;
    private bool hasObstaclePunishmentThisFrame = false;
    
    // RL Movement variables
    private Vector3 lastPosition;
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

        // Reset animation state
        if (animator != null)
        {
            animator.SetBool("isDead", false);
            animator.SetBool("isAttacking", false);
            animator.SetBool("isWalking", false);
            animator.SetBool("isIdle", true);
            animator.ResetTrigger("getHit");
        }

        GetComponent<Collider>().enabled = true;
        
        // Reset rigidbody
        agentRigidbody.linearVelocity = Vector3.zero;
        agentRigidbody.angularVelocity = Vector3.zero;
        agentRigidbody.isKinematic = false;
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
            
            // Direction to player (local space)
            Vector3 localPlayerDirection = transform.InverseTransformDirection(directionToPlayer);
            sensor.AddObservation(localPlayerDirection.x);
            sensor.AddObservation(localPlayerDirection.z);
            sensor.AddObservation(distanceToPlayer / DISTANCE_NORMALIZATION_FACTOR);
        }
        else
        {
            sensor.AddObservation(0f); // No player direction X
            sensor.AddObservation(0f); // No player direction Z  
            sensor.AddObservation(1f);  // Max distance when no player
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
        
        // Only process movement actions if not in knockback or flee state
        if (!IsAgentKnockedBack() && !IsAgentFleeing())
        {
            ProcessActions(actions);
        }
        else
        {
            HandleReactiveStates();
        }
        
        UpdateBehaviorAndRewards();
        CheckStuckState();
        CheckEpisodeEnd();
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        var discreteActions = actionsOut.DiscreteActions;

        // Movement: Forward/Backward
        continuousActions[0] = Input.GetAxis("Vertical");
        // Movement: Left/Right  
        continuousActions[1] = Input.GetAxis("Horizontal");
        // Rotation
        continuousActions[2] = GetRotationInputHeuristic();

        // Attack action
        discreteActions[0] = Input.GetKey(KeyCode.Space) ? 1 : 0;
    }

    private void CheckEpisodeEnd()
    {
        if (StepCount >= MaxStep)
        {
            Debug.Log($"{gameObject.name} Max steps reached. Ending episode.");
            EndEpisode();
        }
        
        // End episode if agent completes enough patrol loops
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
        playerDetection = new PlayerDetection(raySensor, LayerMask.GetMask("Wall", "Obstacle"));
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
        agentRigidbody.mass = 1f;
        agentRigidbody.linearDamping = 3f; // Add drag to prevent sliding
        agentRigidbody.angularDamping = 5f;
        agentRigidbody.freezeRotation = false;
        agentRigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionY;
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
        // Extract movement inputs
        float forward = actions.ContinuousActions[0];
        float right = actions.ContinuousActions[1]; 
        float rotation = actions.ContinuousActions[2];
        bool shouldAttack = actions.DiscreteActions[0] == 1;

        // Apply movement through RL controller
        movementController.ProcessMovement(forward, right, rotation);
        
        // Handle attack
        ProcessAttackAction(shouldAttack);
        
        // Update animation based on movement
        UpdateMovementAnimation();
    }

    private void ProcessAttackAction(bool shouldAttack)
    {
        bool playerInRange = IsPlayerInAttackRange();
        bool canAttack = Time.fixedTime - lastAttackTime >= 2f; // Attack cooldown
        bool shouldAttackAnim = playerInRange && canAttack && rl_EnemyController.combatState.IsAttacking;

        if (playerInRange)
        {
            movementController.FaceTarget(playerDetection.GetPlayerPosition());

            if (shouldAttack && canAttack)
            {
                ExecuteAttack();
            }
            if (shouldAttackAnim && !animator.GetCurrentAnimatorStateInfo(0).IsName("Attack"))
            {
                animator.Play("Attack", 0, 0f);
            }
            else
            {
                currentAction = "Chasing";
                rewardConfig.AddApproachPlayerReward(this, Time.deltaTime);
            }
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

        if (IsAgentKnockedBack() || IsAgentFleeing())
        {
            animator.SetBool("isWalking", isMoving);
            animator.SetBool("isAttacking", false);
            animator.SetBool("isIdle", !isMoving);
        }
    }

    private void HandleReactiveStates()
    {
        if (IsAgentKnockedBack())
        {
            currentState = "Knocked Back";
            currentAction = "Recovering";
            // Let physics handle knockback
        }
        else if (IsAgentFleeing())
        {
            currentState = "Fleeing";
            currentAction = "Fleeing";
            // Apply flee movement
            if (playerDetection.IsPlayerAvailable())
            {
                Vector3 fleeDirection = (transform.position - playerDetection.GetPlayerPosition()).normalized;
                movementController.ApplyFleeMovement(fleeDirection);
            }
        }
    }

    private void CheckStuckState()
    {
        float distanceMoved = Vector3.Distance(transform.position, lastPosition);
        
        if (distanceMoved < STUCK_THRESHOLD)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer >= STUCK_TIME_LIMIT)
            {
                // Punish for being stuck
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

        // Check for obstacles using Ray Perception
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
        else if (currentAction == "Patroling")
        {
            rewardConfig.AddPatrolStepReward(this, deltaTime);
            
            // Reward for movement during patrol
            if (agentRigidbody.linearVelocity.magnitude > 0.1f)
            {
                AddReward(0.002f * deltaTime);
            }
            
            // Bonus for staying away from walls
            if (!IsNearObstacle())
            {
                AddReward(0.001f * deltaTime);
            }
        }
        else if (currentAction == "Idling")
        {
            if (!patrolSystem.IsIdlingAtSpawn() && agentRigidbody.linearVelocity.magnitude < 0.1f)
            {
                rewardConfig.AddIdlePunishment(this, deltaTime * 0.5f);
            }
        }

        ProcessPlayerVisibilityRewards(deltaTime);
        ProcessDistanceRewards(deltaTime);
    }

    private bool IsNearObstacle()
    {
        // Use Ray Perception results to detect obstacles
        var raySensor = GetComponent<RayPerceptionSensorComponent3D>();
        var rayOutputs = RayPerceptionSensor.Perceive(raySensor.GetRayPerceptionInput(), false);
        
        foreach (var rayOutput in rayOutputs.RayOutputs)
        {
            if (rayOutput.HasHit && rayOutput.HitFraction < 0.3f) // Close obstacle
            {
                if (rayOutput.HitGameObject != null && 
                    (rayOutput.HitGameObject.layer == LayerMask.NameToLayer("Wall") ||
                     rayOutput.HitGameObject.layer == LayerMask.NameToLayer("Obstacle")))
                {
                    return true;
                }
            }
        }
        return false;
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
            
            bool idleComplete = patrolSystem.UpdateIdleTimer();
            if (idleComplete)
            {
                currentAction = "Patroling";
            }
            return;
        }

        Vector3 currentTarget = patrolSystem.GetCurrentPatrolTarget();
        float distanceToTarget = Vector3.Distance(transform.position, currentTarget);
        
        // Check if we've reached the waypoint
        if (distanceToTarget < 2f)
        {
            bool completedLoop = patrolSystem.AdvanceToNextWaypoint();
            
            if (completedLoop)
            {
                rewardConfig.AddPatrolReward(this);
                Debug.Log($"{gameObject.name} completed patrol loop {patrolSystem.PatrolLoopsCompleted}");
            }
            
            currentAction = patrolSystem.IsIdlingAtSpawn() ? "Idling" : "Patroling";
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
        // Apply movement forces
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
        
        // Limit flee velocity
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