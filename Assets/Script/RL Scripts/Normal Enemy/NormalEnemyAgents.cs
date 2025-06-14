using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine.AI;
using System.Linq;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(RayPerceptionSensorComponent3D))]
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

    [Header("Patrol Settings")]
    [Tooltip("Tag name for patrol points in the scene")]
    public string patrolPointTag = "PatrolPoint";

    [Header("Debug Visualization")]
    public bool showDebugInfo = true;
    public static bool TrainingActive = true;
    public Vector2 debugTextOffset = new Vector2(10, 10);
    public Color debugTextColor = Color.white;
    public int debugFontSize = 14;

    private AgentHealth agentHealth;
    private PlayerDetection playerDetection;
    private PatrolSystem patrolSystem;
    private AgentMovement agentMovement;
    private DebugDisplay debugDisplay;
    private RewardSystem rewardSystem;

    private const float HEALTH_NORMALIZATION_FACTOR = 100f;
    private int currentStepCount = 0;
    private Vector3 initialPosition;
    private bool isDead = false;
    private string currentState = "Idle";
    private string currentAction = "Idle";

    public override void Initialize()
    {
        InitializeComponents();
        InitializeSystems();
        initialPosition = transform.position;
        ResetAgentState();
        
        // Initialize health bar - FIX: Cast to int
        if (healthBar != null)
        {
            healthBar.SetMaxHealth((int)rl_EnemyController.enemyHP);
        }
    }

    public override void OnEpisodeBegin()
    {
        ResetAgentState();
        agentHealth.ResetHealth();
        WarpToRandomPatrolPoint();
        ResetTrainingArena();
        currentStepCount = 0;
        isDead = false;
        currentState = "Idle";
        currentAction = "Idle";
        
        // Ensure agent is active and can move
        gameObject.SetActive(true);
        if (navAgent != null)
        {
            navAgent.enabled = true;
            navAgent.isStopped = false;
        }
        
        Debug.Log($"{gameObject.name} episode began - Health: {agentHealth.CurrentHealth}");
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        AddHealthObservation(sensor);
        AddPlayerDistanceObservation(sensor);
        AddVelocityObservations(sensor);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (isDead) return;
        currentStepCount++;
        debugDisplay.IncrementSteps();
        
        ProcessMovementActions(actions);
        ProcessAttackAction(actions);
        UpdateDetectionAndBehavior();

        if (currentAction == "Chasing")
            rewardSystem.AddChaseStepReward();
        else if (currentAction.StartsWith("Patrol"))
            rewardSystem.AddPatrolStepReward();
        else if (currentAction == "Idle")
            rewardSystem.AddIdlePunishment();

        debugDisplay.UpdateCumulativeReward(GetCumulativeReward());
        // Check if max steps reached
        if (currentStepCount >= MaxStep)
        {
            Debug.Log($"{gameObject.name} reached max steps ({MaxStep}), ending episode");
            ResetTrainingArena();
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions.Clear();
        
        SetHeuristicMovement(continuousActions);
        SetHeuristicAttack(continuousActions);
    }

    void OnGUI()
    {
        if (showDebugInfo)
        {
            debugDisplay.DisplayDebugInfo(gameObject.name, currentState, currentAction, debugTextOffset, debugTextColor, debugFontSize);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (IsObstacleCollision(collision))
        {
            rewardSystem.AddObstaclePunishment();
        }
    }

    // PUBLIC METHOD: This needs to be called by external damage sources
    public void TakeDamage(float damage)
    {
        if (isDead) return;
        
        Debug.Log($"{gameObject.name} taking {damage} damage. Current health: {agentHealth.CurrentHealth}");
        agentHealth.TakeDamage(damage);
        
        // FIX: Cast to int for health bar
        if (healthBar != null)
        {
            healthBar.SetHealth((int)agentHealth.CurrentHealth);
        }
        if (agentHealth.IsDead)
        {
            Debug.Log($"{gameObject.name} died!");
            HandleDeath();
        }
        else
        {
            Debug.Log($"{gameObject.name} health after damage: {agentHealth.CurrentHealth}");
            HandleDamage();
        }
    }

    // FIX: Make TriggerHitAnimation public so RL_Player can call it
    public void TriggerHitAnimation()
    {
        if (animator != null && HasAnimatorParameter("getHit"))
        {
            animator.SetTrigger("getHit");
        }
    }

    public void SetPatrolPoints(Transform[] points)
    {
        patrolSystem.SetPatrolPoints(points);
    }

    public float CurrentHealth => agentHealth.CurrentHealth;
    public bool IsDead => isDead;

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
        // Find patrol points near this enemy's arena
        Collider[] colliders = Physics.OverlapSphere(transform.position, 20f);
        var points = colliders
            .Where(c => c.CompareTag("Patrol Point"))
            .Select(c => c.transform)
            .ToList();
        
        // Filter out points too close to other enemies
        var nearbyEnemies = Physics.OverlapSphere(transform.position, 10f)
            .Where(c => c.CompareTag("Enemy") && c.gameObject != gameObject)
            .Select(c => c.transform.position)
            .ToList();
            
        var validPoints = points
            .Where(p => !nearbyEnemies.Any(e => Vector3.Distance(p.position, e) < 2f))
            .ToList();
            
        // If no valid points, fall back to all points
        if (validPoints.Count == 0) validPoints = points;
        
        // Shuffle points
        System.Random rand = new System.Random();
        return validPoints.OrderBy(x => rand.Next()).ToArray();
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
        if (patrolPoints.Length > 0)
        {
            int randomIndex = Random.Range(0, patrolPoints.Length);
            navAgent.Warp(patrolPoints[randomIndex].position);
        }
        else
        {
            // Fallback to initial position if no patrol points
            navAgent.Warp(initialPosition);
        }
    }

    private void ResetTrainingArena()
    {
        var spawner = FindFirstObjectByType<RL_TrainingTargetSpawner>();
        spawner?.ResetArena();
    }

    private void AddHealthObservation(VectorSensor sensor)
    {
        sensor.AddObservation(agentHealth.HealthFraction);
    }

    private void AddPlayerDistanceObservation(VectorSensor sensor)
    {
        if (playerDetection.IsPlayerAvailable())
        {
            float normalizedDistance = playerDetection.GetDistanceToPlayer(transform.position) / HEALTH_NORMALIZATION_FACTOR;
            sensor.AddObservation(normalizedDistance);
        }
        else
        {
            sensor.AddObservation(0f);
        }
    }

    private void AddVelocityObservations(VectorSensor sensor)
    {
        Vector3 localVelocity = transform.InverseTransformDirection(navAgent.velocity);
        sensor.AddObservation(localVelocity.x / rl_EnemyController.moveSpeed);
        sensor.AddObservation(localVelocity.z / rl_EnemyController.moveSpeed);
    }

    private void ProcessMovementActions(ActionBuffers actions)
    {
        Vector3 movement = new Vector3(actions.ContinuousActions[0], 0, actions.ContinuousActions[1]);
        float rotation = actions.ContinuousActions[2];
        
        // If player is available and in attack range, prioritize facing the player
        if (playerDetection.IsPlayerAvailable() && 
            agentMovement.IsPlayerInAttackRange(playerDetection.GetPlayerPosition()))
        {
            // Face player but still allow some movement
            agentMovement.FaceTarget(playerDetection.GetPlayerPosition());
            // Reduce movement when in combat
            movement *= 0.3f;
        }
        
        agentMovement.ProcessMovement(movement, rotation);
    }

    private float lastAttackTime;
    private float attackCooldown = 0.5f; // Reduced from 1.0f to allow more frequent attacks

    private void ProcessAttackAction(ActionBuffers actions)
    {
        int branch = actions.DiscreteActions[0];
        bool shouldAttack = actions.DiscreteActions[0] == (int)EnemyHighLevelAction.Attack;
        bool playerInRange = playerDetection.IsPlayerAvailable() &&
        agentMovement.IsPlayerInAttackRange(playerDetection.GetPlayerPosition());

        bool canAttack = Time.time - lastAttackTime >= attackCooldown;
        
        // Face player when in attack range
        if (playerInRange)
        {
            agentMovement.FaceTarget(playerDetection.GetPlayerPosition());
        }
        
        // Log attack decision parameters for debugging
        if (playerInRange)
        {
            Debug.Log($"{gameObject.name} attack params – shouldAttack: {shouldAttack} (branch:{branch}), " +
            $"canAttack: {canAttack} ({Time.time - lastAttackTime:0.00}/{attackCooldown})");
        }
        
        // Attack when in range, attack condition met, and cooldown expired
        if (playerInRange && shouldAttack && canAttack)
        {
            lastAttackTime = Time.time;
            rewardSystem.AddAttackReward();
            TriggerAttackAnimation();
            currentState = "Attacking";
            currentAction = "Attacking";
            
            Debug.Log($"{gameObject.name} attacking player!");
            
            // Damage the player - FIXED: Better player detection and damage application
            var playerTransform = playerDetection.GetPlayerTransform();
            if (playerTransform != null)
            {
                // Try different ways to find the player component
                var player = playerTransform.GetComponent<RL_Player>();
                if (player == null)
                {
                    player = playerTransform.GetComponentInParent<RL_Player>();
                }
                if (player == null)
                {
                    player = playerTransform.GetComponentInChildren<RL_Player>();
                }
                
                if (player != null)
                {
                    Debug.Log($"Dealing {rl_EnemyController.attackDamage} damage to player");
                    bool playerDied = player.DamagePlayer(rl_EnemyController.attackDamage);

                    if (playerDied)
                    {
                        Debug.Log("Player died! Ending episode.");
                        rewardSystem.AddKillPlayerReward(); // Add reward for killing player
                        ResetTrainingArena();
                        EndEpisode();
                    }
                }
                else
                {
                    Debug.LogWarning($"Could not find RL_Player component on {playerTransform.name}");
                }
            }
        }
        else if (playerInRange)
        {
            // Always show chasing when player is in range but not attacking
            currentAction = "Chasing";
            
            // Face player even when not attacking
            agentMovement.FaceTarget(playerDetection.GetPlayerPosition());
        }
        else if (playerInRange && !canAttack)
        {
            Debug.Log($"{gameObject.name} can't attack yet (cooldown: {Time.time - lastAttackTime:0.00}/{attackCooldown}s)");
        }
        else if (playerInRange && !shouldAttack)
        {
            Debug.Log($"{gameObject.name} in range but Attack branch not selected (branch:{branch})");
        }
        
        // Update animation states - FIXED: Ensure proper animation transitions
        if (animator != null)
        {
            // Only set parameters that exist in the animator
            if (HasAnimatorParameter("isAttacking"))
            {
                animator.SetBool("isAttacking", playerInRange && canAttack);
            }
            if (HasAnimatorParameter("isWalking"))
            {
                // Only walk when not attacking and moving
                animator.SetBool("isWalking", !playerInRange && navAgent.velocity.magnitude > 0.1f);
            }
        }
        
        // Adjust movement speed based on combat state
        if (playerInRange)
        {
            navAgent.speed = rl_EnemyController.moveSpeed * 0.2f;
        }
        else
        {
            navAgent.speed = rl_EnemyController.moveSpeed;
        }
    }

    private bool HasAnimatorParameter(string parameterName)
    {
        if (animator == null) return false;
        
        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == parameterName)
                return true;
        }
        return false;
    }

    private void TriggerAttackAnimation()
    {
        if (animator != null && HasAnimatorParameter("attack"))
        {
            // Ensure walking is stopped before attacking
            if (HasAnimatorParameter("isWalking"))
                animator.SetBool("isWalking", false);
                
            animator.SetTrigger("attack");
            
            // Set attacking state
            if (HasAnimatorParameter("isAttacking"))
                animator.SetBool("isAttacking", true);
                
            // Reset attack state after animation completes
            StartCoroutine(ResetAttackState());
        }
    }
    
    private IEnumerator ResetAttackState()
    {
        // Wait for attack animation duration
        yield return new WaitForSeconds(0.5f);
        
        if (animator != null && HasAnimatorParameter("isAttacking"))
        {
            animator.SetBool("isAttacking", false);
        }
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
            string nearestPoint = patrolSystem.GetNearestPointName(transform.position);
            
            // Always show Patroling action when moving
            bool isMoving = navAgent.velocity.magnitude > 0.1f;
            currentAction = isMoving ? "Patroling" : "Idle";
            
            // Update state based on movement and pathfinding
            if (isMoving)
            {
                if (navAgent.hasPath && navAgent.remainingDistance > navAgent.stoppingDistance)
                {
                    currentState = "Pathfinding to " + nearestPoint;
                }
                else
                {
                    currentState = "Patroling near " + nearestPoint;
                }
            }
            else
            {
                currentState = "Position in Arena: " + nearestPoint;
            }
        }
        
        patrolSystem.UpdatePatrol(transform.position, rewardSystem);
    }

    private void SetHeuristicMovement(ActionSegment<float> continuousActions)
    {
        continuousActions[0] = Input.GetAxis("Horizontal");
        continuousActions[1] = Input.GetAxis("Vertical");
        continuousActions[2] = GetRotationInput();
    }

    private void SetHeuristicAttack(ActionSegment<float> continuousActions)
    {
        continuousActions[3] = Input.GetKey(KeyCode.Space) ? 1f : 0f;
    }

    private float GetRotationInput()
    {
        if (Input.GetKey(KeyCode.Q)) return -1f;
        if (Input.GetKey(KeyCode.E)) return 1f;
        return 0f;
    }

    private bool IsObstacleCollision(Collision collision)
    {
        return ((1 << collision.gameObject.layer) & obstacleMask) != 0;
    }

    private void HandleDeath()
    {
        isDead = true;
        TriggerDeathAnimation();
        rewardSystem.AddDeathPunishment();
        currentState = "Dead";
        currentAction = "Dead";
        
        Debug.Log($"{gameObject.name} handling death, resetting arena and ending episode");
        
        // Reset arena and end episode
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
        if (animator != null && HasAnimatorParameter("isDead"))
        {
            animator.SetBool("isDead", true);
        }
    }
}

// Helper classes with fixes
public class AgentHealth
{
    private float maxHealth;
    private float currentHealth;

    public AgentHealth(float maxHealth)
    {
        this.maxHealth = maxHealth;
        ResetHealth();
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        currentHealth = Mathf.Max(currentHealth - damage, 0f);
    }

    public float CurrentHealth => currentHealth;
    public float HealthFraction => currentHealth / maxHealth;
    public bool IsDead => currentHealth <= 0f;
}

public class PlayerDetection
{
    private RayPerceptionSensorComponent3D raySensor;
    private LayerMask obstacleMask;
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
            FindPlayerTransform(); // Try to find player again if lost
            return;
        }

        float distanceToPlayer = GetDistanceToPlayer(agentPosition);
        
        if (distanceToPlayer <= raySensor.RayLength)
        {
            CheckRayPerceptionVisibility();
            if (isPlayerVisible)
            {
                VerifyLineOfSight(agentPosition, distanceToPlayer);
            }
        }
    }

    private void FindPlayerTransform()
    {
        // First try finding by tag
        var playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null) {
            playerTransform = playerObject.transform;
            Debug.Log("Found player by tag");
            return;
        }
        
        // If not found, try finding by component
        var player = Object.FindFirstObjectByType<RL_Player>();
        if (player != null) {
            playerTransform = player.transform;
            Debug.Log("Found player by RL_Player component");
            return;
        }
        
        // If still not found, try finding any object with "Player" in name
        var playerByName = GameObject.Find("Player");
        if (playerByName != null) {
            playerTransform = playerByName.transform;
            Debug.Log("Found player by name");
            return;
        }
        
        // Last resort: find any RL_Player component in the scene
        var allPlayers = Object.FindObjectsByType<RL_Player>(FindObjectsSortMode.None);
        if (allPlayers.Length > 0) {
            playerTransform = allPlayers[0].transform;
            Debug.Log("Found player from all RL_Player instances");
            return;
        }
        
        Debug.LogWarning("Player not found!");
        playerTransform = null;
    }

    private void CheckRayPerceptionVisibility()
    {
        var rayOutputs = RayPerceptionSensor.Perceive(raySensor.GetRayPerceptionInput(), false);
        foreach (var ray in rayOutputs.RayOutputs)
        {
            if (ray.HasHit && ray.HitGameObject.CompareTag("Player"))
            {
                isPlayerVisible = true;
                break;
            }
        }
    }

    private void VerifyLineOfSight(Vector3 agentPosition, float distanceToPlayer)
    {
        Vector3 directionToPlayer = (playerTransform.position - agentPosition).normalized;
        
        if (Physics.Raycast(agentPosition, directionToPlayer, out RaycastHit hit, distanceToPlayer, obstacleMask))
        {
            if (!hit.collider.CompareTag("Player"))
            {
                isPlayerVisible = false;
            }
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
        
        float minDistance = float.MaxValue;
        int nearestIndex = -1;
        
        for (int i = 0; i < patrolPoints.Length; i++)
        {
            float distance = Vector3.Distance(agentPosition, patrolPoints[i].position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestIndex = i;
            }
        }
        
        if (minDistance < NEAR_POINT_DISTANCE)
        {
            // Use the actual GameObject name or generate a default name
            string pointName = patrolPoints[nearestIndex].gameObject.name;
            if (string.IsNullOrEmpty(pointName))
            {
                return "Point " + (nearestIndex + 1);
            }
            return pointName;
        }
        return "None";
    }

    private void AdvanceToNextWaypoint()
    {
        currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
        if (currentPatrolIndex == 0)
        {
            patrolLoopsCompleted++;
        }
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
    private NavMeshAgent navAgent;
    private Transform agentTransform;
    private float moveSpeed;
    private float turnSpeed;
    private float attackRange;

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
        ApplyMovement(movement);
        ApplyRotation(rotation);
    }

    private void ApplyMovement(Vector3 movement)
    {
        if (navAgent != null && navAgent.enabled)
        {
            navAgent.Move(movement * moveSpeed * Time.deltaTime);
        }
    }

    private void ApplyRotation(float rotation)
    {
        agentTransform.Rotate(0, rotation * turnSpeed * Time.deltaTime, 0);
    }

    public bool IsPlayerInAttackRange(Vector3 playerPosition)
    {
        return Vector3.Distance(agentTransform.position, playerPosition) <= attackRange;
    }
    
    public void FaceTarget(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - agentTransform.position).normalized;
        direction.y = 0; // Keep rotation only on y-axis
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
    private int patrolLoopsCompleted;
    private string agentState = "Idle";
    private string agentAction = "Idle";

    public void Reset()
    {
        cumulativeReward = 0f;
        episodeSteps = 0;
    }

    public void IncrementSteps()
    {
        episodeSteps++;
    }

    public void UpdateCumulativeReward(float reward)
    {
        cumulativeReward = reward;
    }

    public void DisplayDebugInfo(string agentName, string currentState, string currentAction, Vector2 offset, Color textColor, int fontSize)
    {
        GUIStyle labelStyle = CreateLabelStyle(textColor, fontSize);
        string debugText = FormatDebugText(agentName, currentState, currentAction);
        
        GUI.Label(new Rect(offset.x, offset.y, 300, 150), debugText, labelStyle);
    }

    private GUIStyle CreateLabelStyle(Color textColor, int fontSize)
    {
        return new GUIStyle
        {
            fontSize = fontSize,
            normal = { textColor = textColor }
        };
    }

    private string FormatDebugText(string agentName, string currentState, string currentAction)
    {
        return $"{agentName}:\nState: {currentState}\nAction: {currentAction}\nSteps: {episodeSteps}\nCumulative Reward: {cumulativeReward:F3}\nPatrol Loops: {patrolLoopsCompleted}";
    }
}

public class RewardSystem
{
    public Agent agent;
    private NormalEnemyRewards rewardConfig;

    public RewardSystem(Agent agent, NormalEnemyRewards rewardConfig)
    {
        this.agent = agent;
        this.rewardConfig = rewardConfig;
    }

    public void AddDetectionReward()
    {
        agent.AddReward(rewardConfig.DetectPlayerReward * Time.deltaTime);
    }

    public void AddIdlePunishment()
    {
        agent.AddReward(rewardConfig.IdlePunishment * Time.deltaTime);
    }

    public void AddPatrolReward()
    {
        agent.AddReward(rewardConfig.PatrolCompleteReward);
    }

    public void AddAttackReward()
    {
        agent.AddReward(rewardConfig.AttackPlayerReward);
    }

    public void AddKillPlayerReward()
    {
        agent.AddReward(rewardConfig.KillPlayerReward);
    }

    public void AddObstaclePunishment()
    {
        agent.AddReward(rewardConfig.ObstaclePunishment);
    }

    public void AddDeathPunishment()
    {
        agent.AddReward(rewardConfig.DiedByPlayerPunishment);
    }

    public void AddDamagePunishment()
    {
        agent.AddReward(rewardConfig.HitByPlayerPunishment);
    }
    
    public void AddExtraAttackIncentive()
    {
        agent.AddReward(rewardConfig.AttackIncentive);
    }

    public void AddChaseStepReward()
    {
        agent.AddReward(rewardConfig.ChaseStepReward);
    }

    public void AddPatrolStepReward()
    {
        agent.AddReward(rewardConfig.PatrolStepReward);
    }
}