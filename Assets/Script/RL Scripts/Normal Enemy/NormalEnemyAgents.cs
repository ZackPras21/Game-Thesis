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
    // ───── PUBLIC INSPECTOR FIELDS ───────────────
    [Header("Movement / Combat Parameters")]
    public float moveSpeed = 3f;
    public float turnSpeed = 120f;
    public float attackRange = 2f;
    public float detectThreshold = 0.5f; 
    // (If RayPerceptionSensor returns a normalized distance < detectThreshold for "Player" tag, we say “I see the player”.)

    [Header("Health / Damage")]
    public float maxHealth = 100f;
    public float attackDamage = 10f;
    public float HP_FleeThreshold = 0.2f; 
    // (If current health / maxHealth < HP_FleeThreshold, we switch to “Flee” or “LowHP” policy.)

    [Header("Rewards Config")]
    public NormalEnemyRewards rewardConfig;   // (Reference your existing reward config ScriptableObject)
    public NormalEnemyStates stateConfig;     // (Reference your existing state machine/enum)

    [Header("References")]
    public Animator animator;
    public NavMeshAgent navAgent;
    public LayerMask obstacleMask;

    // ───── PRIVATE FIELDS ─────────────────────────
    private float currentHealth;
    private Transform playerTransform;
    private bool hasEverSeenPlayer = false;
    private float prevDistanceToPlayer = Mathf.Infinity;
    private Vector3 lastPosition;
    private int stepsSinceLastMove = 0;
    private float timeSinceLastMove = 0f;
    private Vector3 lastKnownPlayerPosition;
    private float lastSeenTime = 0f;

    // Ray sensor component reference (set automatically in Initialize())
    private RayPerceptionSensorComponent3D raySensor;

    // RL-driven behavior only
    private bool playerVisible = false; // For observation only

    // Cached reward‐state references
    private float detectPhaseTimer = 0f;
    private const float detectPhaseDuration = 0.5f;

    // Patrol state
    [Header("Patrol Settings")]
    [Tooltip("Tag name for patrol points in the scene")]
    public string patrolPointTag = "PatrolPoint";
    private Transform[] patrolPoints;
    private int currentPatrolIndex = 0;
    private int patrolLoopsCompleted = 0;
    
    [Header("Debug Visualization")]
    public bool showDebugInfo = true;
    public static bool TrainingActive = true;
    public Vector2 debugTextOffset = new Vector2(10, 10);
    public Color debugTextColor = Color.white;
    public int debugFontSize = 14;
    private float cumulativeReward = 0f;
    private int episodeSteps = 0;
    private int successfulKills = 0;

    // ───── UNITY / ML‑AGENTS CALLBACKS ────────────────

    public override void Initialize()
    {
        navAgent = GetComponent<NavMeshAgent>();
        raySensor = GetComponent<RayPerceptionSensorComponent3D>();
        navAgent.speed = moveSpeed;
        navAgent.angularSpeed = turnSpeed;
        navAgent.stoppingDistance = 0.1f;

        currentHealth = maxHealth;
        playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
        patrolPoints = GameObject.FindGameObjectsWithTag("Patrol Point").Select(o => o.transform).ToArray();
        ResetState();
    }

    private void ResetState()
    {
        playerVisible = false;
        currentPatrolIndex = 0;
        patrolLoopsCompleted = 0;
        cumulativeReward = 0f;
        episodeSteps = 0;
        lastPosition = transform.position;
        stepsSinceLastMove = 0;
        timeSinceLastMove = 0f;
    }
    
    // Unity's OnGUI method is called every frame when rendering GUI elements.
    void OnGUI()
    {
        if (showDebugInfo)
        {
            // Set up the label style with the color, font size, etc.
            GUIStyle labelStyle = new GUIStyle();
            labelStyle.fontSize = debugFontSize;
            labelStyle.normal.textColor = debugTextColor;
            
            // Display the agent state and other debug info at the top-left corner
            string debugText =
            $"{gameObject.name}:" +
            $"State: {(playerVisible ? "Player Visible" : "Patroling")}\n" +
            $"Steps: {episodeSteps} | Reward: {cumulativeReward:F2} | Patrol Loops: {patrolLoopsCompleted}";
            
            // Draw main text
            GUI.Label(new Rect(debugTextOffset.x, debugTextOffset.y, 300, 100), debugText, labelStyle);
        }
    }

    public override void OnEpisodeBegin()
    {
        ResetState();
        currentHealth = maxHealth;
        // warp to random patrol point
        int idx = Random.Range(0, patrolPoints.Length);
        navAgent.Warp(patrolPoints[idx].position);
        RL_TrainingTargetSpawner sp = FindObjectOfType<RL_TrainingTargetSpawner>();
        if (sp != null) sp.ResetArena();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Health
        sensor.AddObservation(currentHealth / maxHealth);

        // Distance to player
        if (playerTransform != null)
        {
            float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            sensor.AddObservation(distToPlayer / 20f);
        }
        else
        {
            sensor.AddObservation(0f);
        }

        // Velocity
        Vector3 v = transform.InverseTransformDirection(navAgent.velocity);
        sensor.AddObservation(v.x / moveSpeed);
        sensor.AddObservation(v.z / moveSpeed);
    }

    
    private void UpdateDetection()
    {
        playerVisible = false;
        if (playerTransform == null)
        {
            playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (playerTransform == null) return;
        }

        float d = Vector3.Distance(transform.position, playerTransform.position);
        if (d <= raySensor.RayLength)
        {
            // Use ray sensor's built-in detection (non-batched)
            var rayOutputs = RayPerceptionSensor.Perceive(raySensor.GetRayPerceptionInput(), false);
            foreach (var ray in rayOutputs.RayOutputs)
            {
                if (ray.HasHit && ray.HitGameObject.CompareTag("Player"))
                {
                    playerVisible = true;
                    break;
                }
            }

            // Additional check for obstacles
            if (playerVisible)
            {
                Vector3 dir = (playerTransform.position - transform.position).normalized;
                if (Physics.Raycast(transform.position, dir, out RaycastHit hit, d, obstacleMask))
                {
                    if (!hit.collider.CompareTag("Player"))
                    {
                        playerVisible = false;
                    }
                }
            }
        }
    }

    private void UpdateBehavior()
    {
        // Let RL decide all actions through policy
        if (playerVisible)
        {
            AddReward(rewardConfig.DetectPlayerReward * Time.deltaTime);
        }
        
        if (patrolPoints.Length == 0) return;
        Vector3 target = patrolPoints[currentPatrolIndex].position;
        if (Vector3.Distance(transform.position, target) < 0.5f)
        {
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            if (currentPatrolIndex == 0) patrolLoopsCompleted++;
            AddReward(rewardConfig.PatrolCompleteReward);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
	    episodeSteps++;
        // Handle movement directly from actions
        Vector3 move = new Vector3(actions.ContinuousActions[0], 0, actions.ContinuousActions[1]);
        navAgent.Move(move * moveSpeed * Time.deltaTime);

        // Handle rotation
        float turn = actions.ContinuousActions[2];
        transform.Rotate(0, turn * turnSpeed * Time.deltaTime, 0);

        // Only proceed if player exists
        if (playerTransform == null)
        {
            playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (playerTransform == null) return;
        }

        // Handle attack if in range
        if (Vector3.Distance(transform.position, playerTransform.position) <= attackRange)
        {
            if (actions.ContinuousActions[3] > 0.5f)
            {
                AddReward(rewardConfig.AttackPlayerReward);
                // Perform attack
            }
        }

        // Update detection and rewards
        UpdateDetection();
        UpdateBehavior();
    }

    void OnCollisionEnter(Collision col)
    {
        if (((1 << col.gameObject.layer) & obstacleMask) != 0)
        {
            AddReward(rewardConfig.ObstaclePunishment);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {

        // Optional: define human input for debugging (e.g. arrow keys to move, space to attack)
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions.Clear();
        continuousActions[0] = Input.GetAxis("Horizontal");
        continuousActions[1] = Input.GetAxis("Vertical");
        continuousActions[2] = Input.GetKey(KeyCode.Q) ? -1f : (Input.GetKey(KeyCode.E) ? 1f : 0f);
        continuousActions[3] = Input.GetKey(KeyCode.Space) ? 1f : 0f;
    }

    public void OnEpisodeEnd()
    {
        // Reset to default state
        currentHealth = maxHealth;
        hasEverSeenPlayer = false;
        prevDistanceToPlayer = Mathf.Infinity;
        currentPatrolIndex = 0;
        
        // Reset NavMeshAgent
        navAgent.Warp(transform.position);
        navAgent.velocity = Vector3.zero;
        navAgent.isStopped = true;
    }
    
    public void SetPatrolPoints(Transform[] points)
    {
        patrolPoints = points;
        currentPatrolIndex = 0;
    }

    // ───── PUBLIC “Damage” API ────────────────
    public void TakeDamage(float amount)
    {
        currentHealth = Mathf.Max(currentHealth - amount, 0f);
        if (currentHealth <= 0f)
        {
            // Play death animation, spawn loot, etc.
            if (animator != null)
            {
                Debug.Log("Setting death animation");
                animator.SetTrigger("isDead");
            }
            AddReward(rewardConfig.DiedByPlayerPunishment);

            // Find spawner and respawn player
            RL_TrainingEnemySpawner spawner = FindObjectOfType<RL_TrainingEnemySpawner>();
            if (spawner != null && playerTransform != null)
            {
                spawner.RespawnPlayer(playerTransform.gameObject);
            }

            EndEpisode();
        }
        else
        {
            // Play “hit” feedback
            if (animator != null)
            {
                animator.SetTrigger("getHit");
            }
            AddReward(rewardConfig.HitByPlayerPunishment);
        }
    }

    public float CurrentHealth => currentHealth;
}
