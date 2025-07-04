using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine.AI;
using System.Linq;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent), typeof(RayPerceptionSensorComponent3D))]
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

    private AgentHealth agentHealth;
    private PlayerDetection playerDetection;
    private PatrolSystem patrolSystem;
    private AgentMovement agentMovement;
    private DebugDisplay debugDisplay;
    private RewardSystem rewardSystem;

    private const float HEALTH_NORMALIZATION_FACTOR = 100f;
    private const float ATTACK_COOLDOWN = 0.5f;
    
    private int currentStepCount;
    private Vector3 initialPosition;
    private bool isDead;
    private string currentState = "Idle";
    private string currentAction = "Idle";
    private float previousDistanceToPlayer = float.MaxValue;
    private float lastAttackTime;

    public float CurrentHealth => agentHealth.CurrentHealth;
    public bool IsDead => isDead;

    public override void Initialize()
    {
        InitializeComponents();
        InitializeSystems();
        initialPosition = transform.position;
        ResetAgentState();
        
        if (healthBar != null)
            healthBar.SetMaxHealth((int)rl_EnemyController.enemyHP);
    }

    public override void OnEpisodeBegin()
    {
        ResetForNewEpisode();
        WarpToRandomPatrolPoint();
        ResetTrainingArena();
        Debug.Log($"{gameObject.name} episode began - Health: {agentHealth.CurrentHealth}");
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(agentHealth.HealthFraction);
        
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
        if (isDead) return;
        
        UpdateStepCount();
        ProcessActions(actions);
        UpdateBehaviorAndRewards();
        CheckEpisodeEnd();
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions.Clear();
        
        continuousActions[0] = Input.GetAxis("Horizontal");
        continuousActions[1] = Input.GetAxis("Vertical");
        continuousActions[2] = GetRotationInput();
        continuousActions[3] = Input.GetKey(KeyCode.Space) ? 1f : 0f;
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
        if (isDead) return;
        
        agentHealth.TakeDamage(damage);
        
        if (healthBar != null)
            healthBar.SetHealth((int)agentHealth.CurrentHealth);
            
        if (agentHealth.IsDead)
            HandleDeath();
        else
            HandleDamage();
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
        agentHealth = new AgentHealth(rl_EnemyController.enemyHP);
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
        ResetAgentState();
        agentHealth.ResetHealth();
        currentStepCount = 0;
        isDead = false;
        currentState = "Idle";
        currentAction = "Idle";
        
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
        Vector3 movement = new Vector3(actions.ContinuousActions[0], 0, actions.ContinuousActions[1]);
        float rotation = actions.ContinuousActions[2];
        
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
        bool canAttack = Time.time - lastAttackTime >= ATTACK_COOLDOWN;
        
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
        lastAttackTime = Time.time;
        rewardSystem.AddAttackReward();
        
        if (currentAction == "Chasing")
            rewardSystem.AddChasePlayerReward();
        
        TriggerAttackAnimation();
        currentState = "Attacking";
        currentAction = "Attacking";
        
        bool hitPlayer = DamagePlayer();
        if (!hitPlayer)
            rewardSystem.AddAttackMissedPunishment();
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
        if (animator == null) return;
        
        if (HasAnimatorParameter("isAttacking"))
            animator.SetBool("isAttacking", playerInRange && canAttack);
            
        if (HasAnimatorParameter("isWalking"))
            animator.SetBool("isWalking", !playerInRange && navAgent.velocity.magnitude > 0.1f);
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
        ProcessRewards();
        debugDisplay.UpdateCumulativeReward(GetCumulativeReward());
    }

    private void ProcessRewards()
    {
        if (currentAction == "Chasing")
        {
            rewardSystem.AddChaseStepReward();
            ProcessChaseRewards();
        }
        else if (currentAction.StartsWith("Patrol"))
        {
            rewardSystem.AddPatrolStepReward();
            if (navAgent.velocity.magnitude < 0.1f)
                rewardSystem.AddNoMovementPunishment();
        }
        else if (currentAction == "Idle")
        {
            rewardSystem.AddIdlePunishment();
        }
        
        ProcessPlayerVisibilityRewards();
        ProcessDistanceRewards();
    }

    private void ProcessChaseRewards()
    {
        if (playerDetection.IsPlayerAvailable())
        {
            float currentDistance = playerDetection.GetDistanceToPlayer(transform.position);
            if (currentDistance < previousDistanceToPlayer)
                rewardSystem.AddApproachPlayerReward();
            previousDistanceToPlayer = currentDistance;
        }
    }

    private void ProcessPlayerVisibilityRewards()
    {
        if (playerDetection.IsPlayerVisible && !currentAction.Contains("Chasing"))
            rewardSystem.AddDoesntChasePlayerPunishment();
    }

    private void ProcessDistanceRewards()
    {
        if (playerDetection.IsPlayerAvailable() && 
            playerDetection.GetDistanceToPlayer(transform.position) > 10f)
            rewardSystem.AddStayFarFromPlayerPunishment();
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
        yield return new WaitForSeconds(0.5f);
        
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

    private void HandleDeath()
    {
        isDead = true;
        TriggerDeathAnimation();
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

// Helper Classes
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
        
        if (!IsPlayerAvailable()) 
        {
            FindPlayerTransform();
            return;
        }

        float distanceToPlayer = GetDistanceToPlayer(agentPosition);
        
        if (distanceToPlayer <= raySensor.RayLength)
        {
            CheckRayPerceptionVisibility();
            if (isPlayerVisible)
                VerifyLineOfSight(agentPosition, distanceToPlayer);
        }
    }

    private void FindPlayerTransform()
    {
        var methods = new System.Func<Transform>[]
        {
            () => GameObject.FindGameObjectWithTag("Player")?.transform,
            () => Object.FindFirstObjectByType<RL_Player>()?.transform,
            () => GameObject.Find("Player")?.transform,
            () => Object.FindObjectsByType<RL_Player>(FindObjectsSortMode.None).FirstOrDefault()?.transform
        };

        foreach (var method in methods)
        {
            playerTransform = method();
            if (playerTransform != null) return;
        }
        
        Debug.LogWarning("Player not found!");
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
    public void AddIdlePunishment() => agent.AddReward(rewardConfig.IdlePunishment * Time.deltaTime);
    public void AddPatrolReward() => agent.AddReward(rewardConfig.PatrolCompleteReward);
    public void AddAttackReward() => agent.AddReward(rewardConfig.AttackPlayerReward);
    public void AddKillPlayerReward() => agent.AddReward(rewardConfig.KillPlayerReward);
    public void AddObstaclePunishment() => agent.AddReward(rewardConfig.ObstaclePunishment);
    public void AddDeathPunishment() => agent.AddReward(rewardConfig.DiedByPlayerPunishment);
    public void AddDamagePunishment() => agent.AddReward(rewardConfig.HitByPlayerPunishment);
    public void AddNoMovementPunishment() => agent.AddReward(rewardConfig.NoMovementPunishment);
    public void AddApproachPlayerReward() => agent.AddReward(rewardConfig.ApproachPlayerReward);
    public void AddStayFarFromPlayerPunishment() => agent.AddReward(rewardConfig.StayFarFromPlayerPunishment);
    public void AddDoesntChasePlayerPunishment() => agent.AddReward(rewardConfig.DoesntChasePlayerPunishment);
    public void AddAttackMissedPunishment() => agent.AddReward(rewardConfig.AttackMissedPunishment);
    public void AddChasePlayerReward() => agent.AddReward(rewardConfig.ChasePlayerReward);
    public void AddChaseStepReward() => agent.AddReward(rewardConfig.ChaseStepReward);
    public void AddPatrolStepReward() => agent.AddReward(rewardConfig.PatrolStepReward);
}