using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine.AI;
using System.Linq;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(RayPerceptionSensorComponent3D))]
public class NormalEnemyAgent : Agent
{
    [Header("Movement / Combat Parameters")]
    public float moveSpeed = 3f;
    public float turnSpeed = 120f;
    public float attackRange = 2f;
    public float detectThreshold = 0.5f;

    [Header("Health / Damage")]
    public float maxHealth = 100f;
    public float attackDamage = 10f;
    public float fleeHealthThreshold = 0.2f;

    [Header("Health Bar")]
    public HealthBar healthBar; 

    [Header("Rewards Config")]
    public NormalEnemyRewards rewardConfig;
    public NormalEnemyStates stateConfig;

    [Header("References")]
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

    private const float HEALTH_NORMALIZATION_FACTOR = 20f;
    private int currentStepCount = 0;
    private Vector3 initialPosition;
    private bool isDead = false;

    public override void Initialize()
    {
        InitializeComponents();
        InitializeSystems();
        initialPosition = transform.position;
        ResetAgentState();
        
        // Initialize health bar - FIX: Cast to int
        if (healthBar != null)
        {
            healthBar.SetMaxHealth((int)maxHealth);
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
            debugDisplay.DisplayDebugInfo(gameObject.name, playerDetection.IsPlayerVisible, debugTextOffset, debugTextColor, debugFontSize);
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
        agentHealth = new AgentHealth(maxHealth);
        patrolSystem = new PatrolSystem(FindPatrolPoints());
        agentMovement = new AgentMovement(navAgent, transform, moveSpeed, turnSpeed, attackRange);
        debugDisplay = new DebugDisplay();
        rewardSystem = new RewardSystem(this, rewardConfig);
    }

    private void ConfigureNavMeshAgent()
    {
        navAgent.speed = moveSpeed;
        navAgent.angularSpeed = turnSpeed;
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
        sensor.AddObservation(localVelocity.x / moveSpeed);
        sensor.AddObservation(localVelocity.z / moveSpeed);
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
    private float attackCooldown = 1.0f;

    private void ProcessAttackAction(ActionBuffers actions)
    {
        bool shouldAttack = actions.ContinuousActions[3] > 0.05f;
        bool playerInRange = playerDetection.IsPlayerAvailable() && 
                            agentMovement.IsPlayerInAttackRange(playerDetection.GetPlayerPosition());
        bool canAttack = Time.time - lastAttackTime > attackCooldown;
        
        // Face player when in attack range
        if (playerInRange)
        {
            agentMovement.FaceTarget(playerDetection.GetPlayerPosition());
        }
        
        // Attack when in range, attack condition met, and cooldown expired
        if (playerInRange && shouldAttack && canAttack)
        {
            lastAttackTime = Time.time;
            rewardSystem.AddAttackReward();
            TriggerAttackAnimation();
            
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
                    Debug.Log($"Dealing {attackDamage} damage to player");
                    bool playerDied = player.DamagePlayer(attackDamage);

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
        
        // Update animation states - FIXED: Check if animator parameters exist
        if (animator != null)
        {
            // Only set parameters that exist in the animator
            if (HasAnimatorParameter("isAttacking"))
            {
                animator.SetBool("isAttacking", playerInRange);
            }
            if (HasAnimatorParameter("isWalking"))
            {
                animator.SetBool("isWalking", !playerInRange && navAgent.velocity.magnitude > 0.1f);
            }
        }
        
        // Adjust movement speed based on combat state
        if (playerInRange)
        {
            navAgent.speed = moveSpeed * 0.3f;
        }
        else
        {
            navAgent.speed = moveSpeed;
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
            animator.SetTrigger("attack");
            if (HasAnimatorParameter("isWalking"))
                animator.SetBool("isWalking", false);
            if (HasAnimatorParameter("isAttacking"))
                animator.SetBool("isAttacking", true);
        }
    }

    private void UpdateDetectionAndBehavior()
    {
        playerDetection.UpdatePlayerDetection(transform.position);
        
        if (playerDetection.IsPlayerVisible)
        {
            rewardSystem.AddDetectionReward();
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
            return;
        }
        
        // If not found, try finding by component
        var player = Object.FindFirstObjectByType<RL_Player>();
        if (player != null) {
            playerTransform = player.transform;
            return;
        }
        
        // If still not found, try finding any object with "Player" in name
        var playerByName = GameObject.Find("Player");
        if (playerByName != null) {
            playerTransform = playerByName.transform;
            return;
        }
        
        // Last resort: find any RL_Player component in the scene
        // FIX: Use FindObjectsByType instead of deprecated FindObjectsOfType
        var allPlayers = Object.FindObjectsByType<RL_Player>(FindObjectsSortMode.None);
        if (allPlayers.Length > 0) {
            playerTransform = allPlayers[0].transform;
            return;
        }
        
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

    public void DisplayDebugInfo(string agentName, bool playerVisible, Vector2 offset, Color textColor, int fontSize)
    {
        GUIStyle labelStyle = CreateLabelStyle(textColor, fontSize);
        string debugText = FormatDebugText(agentName, playerVisible);
        
        GUI.Label(new Rect(offset.x, offset.y, 300, 120), debugText, labelStyle);
    }

    private GUIStyle CreateLabelStyle(Color textColor, int fontSize)
    {
        return new GUIStyle
        {
            fontSize = fontSize,
            normal = { textColor = textColor }
        };
    }

    private string FormatDebugText(string agentName, bool playerVisible)
    {
        string state = playerVisible ? "Player Visible" : "Patroling";
        return $"{agentName}:\nState: {state}\nSteps: {episodeSteps}\nCumulative Reward: {cumulativeReward:F3}\nPatrol Loops: {patrolLoopsCompleted}";
    }
}

public class RewardSystem
{
    private Agent agent;
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
}