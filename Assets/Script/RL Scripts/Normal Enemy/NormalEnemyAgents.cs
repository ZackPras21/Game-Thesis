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
    private const float ATTACK_THRESHOLD = 0.5f;

    public override void Initialize()
    {
        InitializeComponents();
        InitializeSystems();
        ResetAgentState();
    }

    public override void OnEpisodeBegin()
    {
        ResetAgentState();
        agentHealth.ResetHealth();
        WarpToRandomPatrolPoint();
        ResetTrainingArena();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        AddHealthObservation(sensor);
        AddPlayerDistanceObservation(sensor);
        AddVelocityObservations(sensor);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        debugDisplay.IncrementSteps();
        
        if (!playerDetection.IsPlayerAvailable()) return;

        ProcessMovementActions(actions);
        ProcessAttackAction(actions);
        UpdateDetectionAndBehavior();
        
        // FIX 1: Update debug display with current cumulative reward
        debugDisplay.UpdateCumulativeReward(GetCumulativeReward());
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

    public void TakeDamage(float damage)
    {
        agentHealth.TakeDamage(damage);
        
        if (agentHealth.IsDead)
        {
            HandleDeath();
        }
        else
        {
            HandleDamage();
        }
    }

    public void SetPatrolPoints(Transform[] points)
    {
        patrolSystem.SetPatrolPoints(points);
    }

    public float CurrentHealth => agentHealth.CurrentHealth;

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
    }

    private Transform[] FindPatrolPoints()
    {
        return GameObject.FindGameObjectsWithTag("Patrol Point")
            .Select(obj => obj.transform)
            .ToArray();
    }

    private void ResetAgentState()
    {
        playerDetection?.Reset();
        patrolSystem?.Reset();
        debugDisplay?.Reset();
        agentMovement?.Reset();
    }

    private void WarpToRandomPatrolPoint()
    {
        var patrolPoints = patrolSystem.GetPatrolPoints();
        if (patrolPoints.Length > 0)
        {
            int randomIndex = Random.Range(0, patrolPoints.Length);
            navAgent.Warp(patrolPoints[randomIndex].position);
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
        
        agentMovement.ProcessMovement(movement, rotation);
    }

    private void ProcessAttackAction(ActionBuffers actions)
    {
        bool shouldAttack = actions.ContinuousActions[3] > ATTACK_THRESHOLD;
        
        if (shouldAttack && agentMovement.IsPlayerInAttackRange(playerDetection.GetPlayerPosition()))
        {
            rewardSystem.AddAttackReward();
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
        TriggerDeathAnimation();
        rewardSystem.AddDeathPunishment();
        RespawnPlayer();
        EndEpisode();
    }

    private void HandleDamage()
    {
        TriggerHitAnimation();
        rewardSystem.AddDamagePunishment();
    }

    private void TriggerDeathAnimation()
    {
        if (animator != null)
        {
            Debug.Log("Setting death animation");
            animator.SetTrigger("isDead");
        }
    }

    private void TriggerHitAnimation()
    {
        animator?.SetTrigger("getHit");
    }

    private void RespawnPlayer()
    {
        var spawner = FindFirstObjectByType<RL_TrainingTargetSpawner>();
        var playerTransform = playerDetection.GetPlayerTransform();
        
        if (spawner != null && playerTransform != null)
        {
            spawner.RespawnPlayer();
        }
    }
}

// Helper classes 
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
        
        if (!IsPlayerAvailable()) return;

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
        var playerObject = GameObject.FindGameObjectWithTag("Player");
        playerTransform = playerObject?.transform;
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
        navAgent.velocity = Vector3.zero;
        navAgent.isStopped = true;
    }

    public void ProcessMovement(Vector3 movement, float rotation)
    {
        ApplyMovement(movement);
        ApplyRotation(rotation);
    }

    private void ApplyMovement(Vector3 movement)
    {
        navAgent.Move(movement * moveSpeed * Time.deltaTime);
    }

    private void ApplyRotation(float rotation)
    {
        agentTransform.Rotate(0, rotation * turnSpeed * Time.deltaTime, 0);
    }

    public bool IsPlayerInAttackRange(Vector3 playerPosition)
    {
        return Vector3.Distance(agentTransform.position, playerPosition) <= attackRange;
    }
}

// FIX 1: Updated DebugDisplay to properly track and display cumulative reward
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

    // FIX 1: Added method to update cumulative reward from agent
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