using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine.AI;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent), typeof(RayPerceptionSensorComponent3D), typeof(RL_EnemyController))]
public class NormalEnemyAgent : Agent
{
    [Header("References")]
    public RL_EnemyController rl_EnemyController;
    public HealthBar healthBar;
    public NormalEnemyRewards rewardConfig;
    public NormalEnemyStates stateConfig;
    public Animator animator;
    public NavMeshAgent navAgent;
    public LayerMask obstacleMask;

    [Header("Debug")]
    public bool showDebugInfo = true;
    public Vector2 debugTextOffset = new Vector2(10, 10);
    public Color debugTextColor = Color.white;
    public int debugFontSize = 14;

    public static bool TrainingActive = true;

    private PlayerDetection playerDetection;
    private PatrolSystem patrolSystem;
    private AgentMovement agentMovement;
    private DebugDisplay debugDisplay;
    private RewardSystem rewardSystem;
    private AnimationClip attackAnimation;
    private float dynamicAttackCooldown = 0.5f;

    private const float HEALTH_NORMALIZATION_FACTOR = 100f;
    private const float ATTACK_COOLDOWN = 0.5f;
    
    private int currentStepCount;
    private Vector3 initialPosition;
    private string currentState = "Idle";
    private string currentAction = "Idle";
    private float previousDistanceToPlayer = float.MaxValue;
    private float lastAttackTime;

    public float CurrentHealth => rl_EnemyController.enemyHP;
    public float MaxHealth => rl_EnemyController.enemyData.enemyHealth;
    public bool IsDead => rl_EnemyController.healthState.IsDead;
    private bool isInitialized = false;


    public override void Initialize()
    {
         // Ensure RL_EnemyController reference exists
        if (rl_EnemyController == null)
        {
            rl_EnemyController = GetComponent<RL_EnemyController>();
        }
        
        // Double-check after GetComponent
        if (rl_EnemyController == null)
        {
            Debug.LogError("NormalEnemyAgent: RL_EnemyController component is missing!", gameObject);
            enabled = false;
            return;
        }
        
        // Check if RL_EnemyController is initialized
        if (!rl_EnemyController.IsInitialized)
        {
            rl_EnemyController.ForceInitialize();
        }

        // Check enemyData after initialization
        if (rl_EnemyController.enemyData == null)
        {
            Debug.LogError("NormalEnemyAgent: enemyData is still null after initialization!", gameObject);
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
        ResetForNewEpisode();
        WarpToRandomPatrolPoint();
        ResetTrainingArena();
        Debug.Log($"{gameObject.name} episode began - Health: {CurrentHealth}");
        if (rl_EnemyController != null) rl_EnemyController.ShowHealthBar();

        // Reset animator to a living state
        if (animator != null)
        {
            animator.SetBool("isDead", false);
            animator.SetBool("isAttacking", false);
            animator.SetBool("isWalking", false);
            animator.SetBool("isIdle", true);
            animator.ResetTrigger("attack");
            animator.ResetTrigger("getHit");
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (!isInitialized || rl_EnemyController == null) return;
        float healthFraction = CurrentHealth / MaxHealth;
        sensor.AddObservation(healthFraction);
        
        float normalizedDistance = playerDetection.IsPlayerAvailable() 
            ? playerDetection.GetDistanceToPlayer(transform.position) / HEALTH_NORMALIZATION_FACTOR 
            : 0f;
        sensor.AddObservation(normalizedDistance);
        
        Vector3 localVelocity = transform.InverseTransformDirection(navAgent.velocity);
        sensor.AddObservation(localVelocity.x / rl_EnemyController.moveSpeed);
        sensor.AddObservation(localVelocity.z / rl_EnemyController.moveSpeed);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!isInitialized || rl_EnemyController == null) return;
        // Prevent action processing when agent is dead or disabled
        if (IsDead || !isActiveAndEnabled) return;
        
        UpdateStepCount();
        ProcessActions(actions);
        UpdateBehaviorAndRewards();
        CheckEpisodeEnd();
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        var discreteActions = actionsOut.DiscreteActions;
        
        // UP (W key)
        continuousActions[0] = Input.GetKey(KeyCode.W) ? 1f : 0f;
        // DOWN (S key)
        continuousActions[1] = Input.GetKey(KeyCode.S) ? 1f : 0f;
        // RIGHT (D key)
        continuousActions[2] = Input.GetKey(KeyCode.D) ? 1f : 0f;
        // LEFT (A key)
        continuousActions[3] = Input.GetKey(KeyCode.A) ? 1f : 0f;
        // ROTATE (Q/E keys)
        continuousActions[4] = GetRotationInputHeuristic();

        // Attack (Space)
        discreteActions[0] = Input.GetKey(KeyCode.Space) ? 
            (int)EnemyHighLevelAction.Attack : 
            (int)EnemyHighLevelAction.NoAttack;
    }

    private float GetRotationInputHeuristic()
    {
        if (Input.GetKey(KeyCode.Q)) return -1f;
        if (Input.GetKey(KeyCode.E)) return 1f;
        return 0f;
    }

    private enum EnemyHighLevelAction
    {
        NoAttack = 0,
        Attack = 1,
        // Add other actions if needed
    }

    void OnGUI()
    {
        if (showDebugInfo)
            debugDisplay.DisplayDebugInfo(gameObject.name, currentState, currentAction, debugTextOffset, debugTextColor, debugFontSize, patrolSystem.PatrolLoopsCompleted);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (IsObstacleCollision(collision))
            rewardSystem.AddObstaclePunishment();
    }

    public void TakeDamage(float damage)
    {
        if (IsDead) return;
        
        rl_EnemyController.TakeDamage(Mathf.RoundToInt(damage));
    }

    public void TriggerHitAnimation()
    {
        if (HasAnimatorParameter("getHit"))
            animator.SetTrigger("getHit");
    } 

    public void SetPatrolPoints(Transform[] points) => patrolSystem.SetPatrolPoints(points);

    private void InitializeComponents()
    {
        navAgent = GetComponent<NavMeshAgent>();
        var raySensor = GetComponent<RayPerceptionSensorComponent3D>();
        ConfigureNavMeshAgent();
        playerDetection = new PlayerDetection(raySensor, obstacleMask);
    }

    private void InitializeSystems()
    {
        patrolSystem = new PatrolSystem(FindPatrolPoints());
        agentMovement = new AgentMovement(navAgent, transform, rl_EnemyController.moveSpeed, rl_EnemyController.rotationSpeed, rl_EnemyController.attackRange);
        debugDisplay = new DebugDisplay();
        rewardSystem = new RewardSystem(this, rewardConfig);
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
        var allPoints = Physics.OverlapSphere(transform.position, 20f)
            .Where(c => c.CompareTag("Patrol Point"))
            .Select(c => c.transform)
            .ToList();
        
        var nearbyEnemies = Physics.OverlapSphere(transform.position, 10f)
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
        if (!isInitialized) return;
        
        ResetAgentState();        

        // Check if RL_EnemyController needs reinitialization
        if (rl_EnemyController == null || rl_EnemyController.enemyData == null)
        {
            if (rl_EnemyController == null)
            {
                rl_EnemyController = GetComponent<RL_EnemyController>();
            }
            
            if (rl_EnemyController != null && !rl_EnemyController.IsInitialized)
            {
                rl_EnemyController.ForceInitialize();
            }
            
            if (rl_EnemyController == null || rl_EnemyController.enemyData == null)
            {
                Debug.LogError("rl_EnemyController or enemyData is null in ResetForNewEpisode - Skipping episode", gameObject);
                return;
            }
        }

        rl_EnemyController.enemyHP = rl_EnemyController.enemyData.enemyHealth;
        rl_EnemyController.healthState.SetDead(false);
        rl_EnemyController.InitializeHealthBar();

        currentStepCount = 0;
        currentState = "Idle";
        currentAction = "Idle";
        lastAttackTime = Time.fixedTime - dynamicAttackCooldown;
        
        // Reset patrol loop counter at start of new episode
        if (patrolSystem != null)
            patrolSystem.Reset();
        
        gameObject.SetActive(true);
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
            : initialPosition;
        navAgent.Warp(targetPosition);
    }

    private void ResetTrainingArena()
    {
        FindFirstObjectByType<RL_TrainingTargetSpawner>()?.ResetArena();
    }

    private void UpdateStepCount()
    {
        currentStepCount++;
        debugDisplay.IncrementSteps();
    }

    private void ProcessActions(ActionBuffers actions)
    {
        ProcessMovementActions(actions);
        ProcessAttackAction(actions);
    }

    private void ProcessMovementActions(ActionBuffers actions)
    {
        // Combine UP/DOWN and LEFT/RIGHT into movement vectors
        float vertical = actions.ContinuousActions[0] - actions.ContinuousActions[1]; // UP - DOWN
        float horizontal = actions.ContinuousActions[2] - actions.ContinuousActions[3]; // RIGHT - LEFT
        float rotation = actions.ContinuousActions[4]; // ROTATE
        
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
        bool canAttack = Time.fixedTime - lastAttackTime >= dynamicAttackCooldown;
        
        if (playerInRange)
            agentMovement.FaceTarget(playerDetection.GetPlayerPosition());
        
        if (playerInRange && shouldAttack && canAttack)
            ExecuteAttack();
        else if (playerInRange)
            currentAction = "Chasing";
        
        UpdateAnimationStates(playerInRange, canAttack);
        AdjustMovementSpeed(playerInRange);
    }

    private void ExecuteAttack()
    {
        // Get attack animation length if available
        if (animator != null && attackAnimation == null)
        {
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName("Attack"))
            {
                attackAnimation = animator.GetCurrentAnimatorClipInfo(0)[0].clip;
                dynamicAttackCooldown = attackAnimation.length;
            }
        }

        lastAttackTime = Time.fixedTime;
        rewardSystem.AddAttackReward();
        
        if (currentAction == "Chasing")
            rewardSystem.AddChasePlayerReward();
        
        // Trigger attack via the controller
        if (rl_EnemyController != null)
        {
            rl_EnemyController.AgentAttack();
        }
        
        currentState = "Attacking";
        currentAction = "Attacking";
    }

    private bool DamagePlayer()
    {
        var playerTransform = playerDetection.GetPlayerTransform();
        if (playerTransform == null) return false;
        
        var player = GetPlayerComponent(playerTransform);
        if (player == null) return false;
        
        bool playerDied = player.DamagePlayer(rl_EnemyController.attackDamage);
        
        if (playerDied)
        {
            rewardSystem.AddKillPlayerReward();
            ResetTrainingArena();
            EndEpisode();
        }
        
        return true;
    }

    private RL_Player GetPlayerComponent(Transform playerTransform)
    {
        return playerTransform.GetComponent<RL_Player>() ??
               playerTransform.GetComponentInParent<RL_Player>() ??
               playerTransform.GetComponentInChildren<RL_Player>();
    }

    private void UpdateAnimationStates(bool playerInRange, bool canAttack)
    {
        if (animator == null || IsDead || rl_EnemyController == null) return;

        bool isMoving = navAgent.velocity.magnitude > 0.1f &&
                      rl_EnemyController != null &&
                      !rl_EnemyController.combatState.IsAttacking;
        bool shouldAttack = playerInRange && canAttack;

        animator.SetBool("isWalking", isMoving);
        animator.SetBool("isAttacking", shouldAttack);
        animator.SetBool("isIdle", !isMoving && !shouldAttack);

        // Sync animation speed with movement
        if (animator.GetBool("isWalking"))
        {
            animator.speed = Mathf.Clamp(navAgent.velocity.magnitude / rl_EnemyController.moveSpeed, 0.5f, 1.5f);
        }
        else
        {
            animator.speed = 1f;
        }

        // Force attack animation completion before state change
        if (shouldAttack && !animator.GetCurrentAnimatorStateInfo(0).IsName("Attack"))
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
            rewardSystem.AddChaseStepReward(deltaTime);
            ProcessChaseRewards(deltaTime);
        }
        else if (currentAction.StartsWith("Patrol"))
        {
            rewardSystem.AddPatrolStepReward(deltaTime);
            if (navAgent.velocity.magnitude < 0.1f)
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
        if (playerDetection.IsPlayerAvailable())
        {
            float currentDistance = playerDetection.GetDistanceToPlayer(transform.position);
            if (currentDistance < previousDistanceToPlayer)
                rewardSystem.AddApproachPlayerReward(deltaTime);
            previousDistanceToPlayer = currentDistance;
        }
    }

    private void ProcessPlayerVisibilityRewards(float deltaTime)
    {
        if (playerDetection.IsPlayerVisible && !currentAction.Contains("Chasing"))
            rewardSystem.AddDoesntChasePlayerPunishment(deltaTime);
    }

    private void ProcessDistanceRewards(float deltaTime)
    {
        if (playerDetection.IsPlayerAvailable() &&
            playerDetection.GetDistanceToPlayer(transform.position) > 10f)
            rewardSystem.AddStayFarFromPlayerPunishment(deltaTime);
    }

    private void UpdateDetectionAndBehavior()
    {
        playerDetection.UpdatePlayerDetection(transform.position);
        
        if (playerDetection.IsPlayerVisible)
        {
            rewardSystem.AddDetectionReward();
            currentState = "Detecting";
            currentAction = "Detecting";
        }
        else
        {
            UpdatePatrolBehavior();
        }
        
        patrolSystem.UpdatePatrol(transform.position, rewardSystem);
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

    private void CheckEpisodeEnd()
    {
        if (currentStepCount >= MaxStep)
        {
            ResetTrainingArena();
            EndEpisode();
        }
    }

    private void TriggerAttackAnimation()
    {
        if (!HasAnimatorParameter("attack")) return;
        
        if (HasAnimatorParameter("isWalking"))
            animator.SetBool("isWalking", false);
            
        animator.SetTrigger("attack");
        
        if (HasAnimatorParameter("isAttacking"))
            animator.SetBool("isAttacking", true);
            
        StartCoroutine(ResetAttackState());
    }
    
    private IEnumerator ResetAttackState()
    {
        if (attackAnimation != null)
        {
            yield return new WaitForSeconds(attackAnimation.length);
        }
        else
        {
            Debug.LogWarning("Using fallback attack cooldown");
            yield return new WaitForSeconds(0.5f);
        }
        
        if (HasAnimatorParameter("isAttacking"))
            animator.SetBool("isAttacking", false);
    }

    private float GetRotationInput()
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

    private bool HasAnimatorParameter(string parameterName)
    {
        if (animator == null) return false;
        return animator.parameters.Any(param => param.name == parameterName);
    }

    public void HandleEnemyDeath()
    {
        rewardSystem.AddDeathPunishment();
        currentState = "Dead";
        currentAction = "Dead";
        ResetTrainingArena();
        EndEpisode();
    }

    private void HandleDamage()
    {
        TriggerHitAnimation();
        rewardSystem.AddDamagePunishment();
    }

    private void TriggerDeathAnimation()
    {
        if (HasAnimatorParameter("isDead"))
            animator.SetBool("isDead", true);
    }
}


public class AgentHealth
{
    private readonly float maxHealth;
    private float currentHealth;

    public AgentHealth(float maxHealth)
    {
        this.maxHealth = maxHealth;
        ResetHealth();
    }

    public void ResetHealth() => currentHealth = maxHealth;
    public void TakeDamage(float damage) => currentHealth = Mathf.Max(currentHealth - damage, 0f);

    public float CurrentHealth => currentHealth;
    public float HealthFraction => currentHealth / maxHealth;
    public bool IsDead => currentHealth <= 0f;
}

public class PlayerDetection
{
    private readonly RayPerceptionSensorComponent3D raySensor;
    private readonly LayerMask obstacleMask;
    private Transform playerTransform;
    private bool isPlayerVisible;

    public PlayerDetection(RayPerceptionSensorComponent3D raySensor, LayerMask obstacleMask)
    {
        this.raySensor = raySensor;
        this.obstacleMask = obstacleMask;
        FindPlayerTransform();
    }

    public void Reset()
    {
        isPlayerVisible = false;
        FindPlayerTransform();
    }

    public void UpdatePlayerDetection(Vector3 agentPosition)
    {
        isPlayerVisible = false;
        
        if (!IsPlayerAvailable() || !playerTransform.gameObject.activeInHierarchy)
        {
            FindPlayerTransform();
            if (!IsPlayerAvailable())
            {
                return;
            }
        }

        try
        {
            float distanceToPlayer = GetDistanceToPlayer(agentPosition);
            
            if (distanceToPlayer <= raySensor.RayLength)
            {
                CheckRayPerceptionVisibility();
                if (isPlayerVisible)
                    VerifyLineOfSight(agentPosition, distanceToPlayer);
            }
        }
        catch (MissingReferenceException)
        {
            // Player object was destroyed, initiate recovery
            playerTransform = null;
            FindPlayerTransform();
        }
    }

    private float lastCheckTime;
    private float retryInterval = 5f;
    private int maxRetries = 3;
    private int currentRetries;

    private void FindPlayerTransform()
    {
        if (Time.time - lastCheckTime < retryInterval && currentRetries >= maxRetries)
            return;

        var methods = new System.Func<Transform>[]
        {
            () => GameObject.FindGameObjectWithTag("Player")?.transform,
            () => Object.FindFirstObjectByType<RL_Player>()?.transform,
            () => GameObject.Find("Player")?.transform,
            () => Object.FindObjectsByType<RL_Player>(FindObjectsSortMode.None).FirstOrDefault()?.transform
        };

        currentRetries = 0;
        foreach (var method in methods)
        {
            playerTransform = method();
            if (playerTransform != null)
            {
                currentRetries = 0;
                lastCheckTime = Time.time;
                return;
            }
            currentRetries++;
        }
    }

    private void CheckRayPerceptionVisibility()
    {
        var rayOutputs = RayPerceptionSensor.Perceive(raySensor.GetRayPerceptionInput(), false);
        isPlayerVisible = rayOutputs.RayOutputs.Any(ray => 
            ray.HasHit && ray.HitGameObject.CompareTag("Player"));
    }

    private void VerifyLineOfSight(Vector3 agentPosition, float distanceToPlayer)
    {
        Vector3 directionToPlayer = (playerTransform.position - agentPosition).normalized;
        
        if (Physics.Raycast(agentPosition, directionToPlayer, out RaycastHit hit, distanceToPlayer, obstacleMask))
        {
            if (!hit.collider.CompareTag("Player"))
                isPlayerVisible = false;
        }
    }

    public bool IsPlayerAvailable() => playerTransform != null;
    public bool IsPlayerVisible => isPlayerVisible;
    public Vector3 GetPlayerPosition() => playerTransform?.position ?? Vector3.zero;
    public Transform GetPlayerTransform() => playerTransform;
    public float GetDistanceToPlayer(Vector3 agentPosition) => 
        IsPlayerAvailable() ? Vector3.Distance(agentPosition, playerTransform.position) : float.MaxValue;
}

public class PatrolSystem
{
    private Transform[] patrolPoints;
    private int currentPatrolIndex;
    private int patrolLoopsCompleted;

    private const float PATROL_WAYPOINT_TOLERANCE = 0.5f;
    private const float NEAR_POINT_DISTANCE = 3f;

    public PatrolSystem(Transform[] patrolPoints)
    {
        this.patrolPoints = patrolPoints;
        Reset();
    }

    public void Reset()
    {
        currentPatrolIndex = 0;
        patrolLoopsCompleted = 0;
    }

    public void UpdatePatrol(Vector3 agentPosition, RewardSystem rewardSystem)
    {
        if (patrolPoints.Length == 0) return;

        Vector3 targetWaypoint = patrolPoints[currentPatrolIndex].position;
        
        if (Vector3.Distance(agentPosition, targetWaypoint) < PATROL_WAYPOINT_TOLERANCE)
        {
            AdvanceToNextWaypoint();
            rewardSystem.AddPatrolReward();
        }
    }

    public string GetNearestPointName(Vector3 agentPosition)
    {
        if (patrolPoints.Length == 0) return "None";
        
        int nearestIndex = GetNearestPointIndex(agentPosition);
        float minDistance = Vector3.Distance(agentPosition, patrolPoints[nearestIndex].position);
        
        if (minDistance < NEAR_POINT_DISTANCE)
        {
            string pointName = patrolPoints[nearestIndex].gameObject.name;
            return string.IsNullOrEmpty(pointName) ? $"Point {nearestIndex + 1}" : pointName;
        }
        return "None";
    }

    private int GetNearestPointIndex(Vector3 agentPosition)
    {
        float minDistance = float.MaxValue;
        int nearestIndex = 0;
        
        for (int i = 0; i < patrolPoints.Length; i++)
        {
            float distance = Vector3.Distance(agentPosition, patrolPoints[i].position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestIndex = i;
            }
        }
        return nearestIndex;
    }

    private void AdvanceToNextWaypoint()
    {
        currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
        if (currentPatrolIndex == 0)
            patrolLoopsCompleted++;
    }

    public void SetPatrolPoints(Transform[] points)
    {
        patrolPoints = points;
        Reset();
    }

    public Transform[] GetPatrolPoints() => patrolPoints;
    public int PatrolLoopsCompleted => patrolLoopsCompleted;
}

public class AgentMovement
{
    private readonly NavMeshAgent navAgent;
    private readonly Transform agentTransform;
    private readonly float moveSpeed;
    private readonly float turnSpeed;
    private readonly float attackRange;

    public AgentMovement(NavMeshAgent navAgent, Transform agentTransform, float moveSpeed, float turnSpeed, float attackRange)
    {
        this.navAgent = navAgent;
        this.agentTransform = agentTransform;
        this.moveSpeed = moveSpeed;
        this.turnSpeed = turnSpeed;
        this.attackRange = attackRange;
    }

    public void Reset()
    {
        if (navAgent != null)
        {
            navAgent.velocity = Vector3.zero;
            navAgent.isStopped = false;
        }
    }

    public void ProcessMovement(Vector3 movement, float rotation)
    {
        if (navAgent != null && navAgent.enabled)
            navAgent.Move(movement * moveSpeed * Time.deltaTime);
            
        agentTransform.Rotate(0, rotation * turnSpeed * Time.deltaTime, 0);
    }

    public bool IsPlayerInAttackRange(Vector3 playerPosition) =>
        Vector3.Distance(agentTransform.position, playerPosition) <= attackRange;
    
    public void FaceTarget(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - agentTransform.position).normalized;
        direction.y = 0;
        
        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            agentTransform.rotation = Quaternion.Slerp(
                agentTransform.rotation,
                lookRotation,
                Time.deltaTime * turnSpeed / 100f
            );
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

public class RewardSystem
{
    private readonly Agent agent;
    private readonly NormalEnemyRewards rewardConfig;

    public RewardSystem(Agent agent, NormalEnemyRewards rewardConfig)
    {
        this.agent = agent;
        this.rewardConfig = rewardConfig;
    }

    public void AddDetectionReward() => agent.AddReward(rewardConfig.DetectPlayerReward * Time.deltaTime);
    public void AddIdlePunishment(float deltaTime) => agent.AddReward(rewardConfig.IdlePunishment * deltaTime);
    public void AddPatrolReward() => agent.AddReward(rewardConfig.PatrolCompleteReward);
    public void AddAttackReward() => agent.AddReward(rewardConfig.AttackPlayerReward);
    public void AddKillPlayerReward() => agent.AddReward(rewardConfig.KillPlayerReward);
    public void AddObstaclePunishment() => agent.AddReward(rewardConfig.ObstaclePunishment);
    public void AddDeathPunishment() => agent.AddReward(rewardConfig.DiedByPlayerPunishment);
    public void AddDamagePunishment() => agent.AddReward(rewardConfig.HitByPlayerPunishment);
    public void AddNoMovementPunishment(float deltaTime) => agent.AddReward(rewardConfig.NoMovementPunishment * deltaTime);
    public void AddApproachPlayerReward(float deltaTime) => agent.AddReward(rewardConfig.ApproachPlayerReward * deltaTime);
    public void AddStayFarFromPlayerPunishment(float deltaTime) => agent.AddReward(rewardConfig.StayFarFromPlayerPunishment * deltaTime);
    public void AddDoesntChasePlayerPunishment(float deltaTime) => agent.AddReward(rewardConfig.DoesntChasePlayerPunishment * deltaTime);
    public void AddAttackMissedPunishment() => agent.AddReward(rewardConfig.AttackMissedPunishment);
    public void AddChasePlayerReward() => agent.AddReward(rewardConfig.ChasePlayerReward);
    public void AddChaseStepReward(float deltaTime) => agent.AddReward(rewardConfig.ChaseStepReward * deltaTime);
    public void AddPatrolStepReward(float deltaTime) => agent.AddReward(rewardConfig.PatrolStepReward * deltaTime);
    public void AddAttackIncentive(float deltaTime) => agent.AddReward(rewardConfig.AttackIncentive * deltaTime);
}
