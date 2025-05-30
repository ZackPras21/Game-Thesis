using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class NormalEnemyAgents : Agent
{
    [Header("References")]
    public RL_EnemyController enemyController;
    public Transform malePlayerTransform;
    public Transform femalePlayerTransform;
    public Transform[] patrolPoints; // Patrol points for the enemy to move to
    private int currentPatrolIndex = 0; // Current patrol point index
    
    [Header("RL Settings")]
    public float detectionReward = 0.1f;
    public float attackReward = 0.2f;
    public float killReward = 1f;
    public float damagePenalty = -0.5f;
    public float idlePenalty = -0.01f;

    private NormalEnemyRewards normalEnemyRewards;
    private NormalEnemyState currentState;

    private float _positionCheckTimer = 0f;
    private float _positionCheckInterval = 1f;
    private Vector3 _lastPosition;

    // Initialize references
    public override void Initialize()
    {
        normalEnemyRewards = new NormalEnemyRewards();
        currentState = new NormalEnemyState();  // Initialize the state
    }

    // Collect observations from the environment
    public override void CollectObservations(VectorSensor sensor)
{
    // Enemy state observations
    sensor.AddObservation(enemyController.m_PlayerInRange);
    sensor.AddObservation(enemyController.m_PlayerNear);
    sensor.AddObservation(enemyController.m_IsPatrol);
    sensor.AddObservation(enemyController.m_IsAttacking);
    sensor.AddObservation((int)enemyController.enemyType);
    sensor.AddObservation(enemyController.GetHealthPercentage());

    // Movement observations
    sensor.AddObservation(enemyController.transform.position);
    sensor.AddObservation(enemyController.transform.forward);

    // Player position observations for both Male and Female players
    if (malePlayerTransform != null)
    {
        float malePlayerDistance = Vector3.Distance(transform.position, malePlayerTransform.position);
        sensor.AddObservation(malePlayerDistance / enemyController.viewRadius);

        Vector3 malePlayerDirection = (malePlayerTransform.position - transform.position).normalized;
        sensor.AddObservation(malePlayerDirection.x);
        sensor.AddObservation(malePlayerDirection.z);
    }

    if (femalePlayerTransform != null)
    {
        float femalePlayerDistance = Vector3.Distance(transform.position, femalePlayerTransform.position);
        sensor.AddObservation(femalePlayerDistance / enemyController.viewRadius);

        Vector3 femalePlayerDirection = (femalePlayerTransform.position - transform.position).normalized;
        sensor.AddObservation(femalePlayerDirection.x);
        sensor.AddObservation(femalePlayerDirection.z);
    }
}

    // Handle actions and update the agent's state
    public override void OnActionReceived(ActionBuffers actions)
    {
        // Continuous movement (x, z direction)
        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];

        // Continuous attack intensity (0-1)
        float attackIntensity = actions.ContinuousActions[2];

        // Special behavior (still binary)
        int specialBehavior = actions.DiscreteActions[0];

        // Calculate movement vector
        Vector3 moveDirection = new Vector3(moveX, 0, moveZ).normalized;

        // Update enemy behavior based on actions
        if (moveDirection.magnitude > 0.1f && patrolPoints.Length > 0)
        {
            enemyController.m_IsPatrol = true;
            // Move towards current patrol point
            Vector3 targetPos = patrolPoints[currentPatrolIndex].transform.position;
            Vector3 patrolMoveDir = (targetPos - transform.position).normalized;
            enemyController.transform.position += patrolMoveDir * Time.deltaTime * 3f;
            
            // Rotate towards target
            enemyController.RotateTowardsPlayer(targetPos, enemyController.rotationSpeed);
            
            // Move to next patrol point if reached current one
            if (Vector3.Distance(transform.position, targetPos) < 1.5f)
            {
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            }
            
            // Small reward for moving
            AddReward(0.005f * moveDirection.magnitude);
        }
        else
        {
            enemyController.m_IsPatrol = false;
            // Stop movement by not updating position
            AddReward(idlePenalty);
        }

        // Handle attack with intensity
        enemyController.m_IsAttacking = attackIntensity > 0.5f;

        // Handle special behaviors when HP is low
        if (enemyController.IsHealthLow())
        {
            HandleLowHPBehavior(specialBehavior);
        }

        // Position-based rewards
        _positionCheckTimer += Time.deltaTime;
        if (_positionCheckTimer >= _positionCheckInterval)
        {
            float distanceMoved = Vector3.Distance(transform.position, _lastPosition);
            if (distanceMoved > 0.1f)
            {
                AddReward(0.005f); // Small reward for moving
            }
            _lastPosition = transform.position;
            _positionCheckTimer = 0f;
        }
    }

    private void HandleLowHPBehavior(int behaviorType)
    {
        switch (enemyController.enemyType)
        {
            case EnemyType.Creep:
                // Circle around player
                if (behaviorType == 1 && PlayerController.Instance != null)
                {
                    Vector3 circlePos = PlayerController.Instance.transform.position;
                    circlePos += Quaternion.Euler(0, Time.time * 100f, 0) * Vector3.forward * 3f;
                    Vector3 moveDir = (circlePos - transform.position).normalized;
                    enemyController.transform.position += moveDir * Time.deltaTime * 3f;
                }
                break;
                
            case EnemyType.Medium1: // Humanoid
                // Wait for other enemies
                if (behaviorType == 1)
                {
                    // Stop movement by not updating position
                }
                break;
                
            case EnemyType.Medium2: // Bull
                // Run away then attack if alone
                if (behaviorType == 1)
                {
                    if (AreOtherEnemiesAlive())
                    {
                        // Run away from player
                        Vector3 fleeDirection = transform.position - PlayerController.Instance.transform.position;
                        Vector3 fleePos = transform.position + fleeDirection.normalized * 5f;
                        Vector3 moveDir = (fleePos - transform.position).normalized;
                        enemyController.transform.position += moveDir * Time.deltaTime * 4f;
                    }
                    else
                    {
                        // Attack if alone
                        enemyController.m_IsAttacking = true;
                    }
                }
                break;
        }
    }

    private bool AreOtherEnemiesAlive()
    {
        EnemyController[] enemies = UnityEngine.Object.FindObjectsByType<EnemyController>(UnityEngine.FindObjectsSortMode.None);
        foreach (EnemyController enemy in enemies)
        {
            if (enemy != enemyController && !enemy.IsDead())
                return true;
        }
        return false;
    }

    // Heuristic function for manual control (for testing)
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        var discreteActions = actionsOut.DiscreteActions;
        
        // Movement controls (WASD)
        continuousActions[0] = Input.GetKey(KeyCode.D) ? 1 : Input.GetKey(KeyCode.A) ? -1 : 0; // X axis
        continuousActions[1] = Input.GetKey(KeyCode.W) ? 1 : Input.GetKey(KeyCode.S) ? -1 : 0; // Z axis
        
        // Attack control (Space)
        continuousActions[2] = Input.GetKey(KeyCode.Space) ? 1 : 0; // Attack intensity
        
        // Special behavior (Shift)
        discreteActions[0] = Input.GetKey(KeyCode.LeftShift) ? 1 : 0;
    }

    // Called by EnemyController when events occur
    public void OnPlayerDetected()
    {
        AddReward(detectionReward);
    }

    public void OnAttackHit()
    {
        AddReward(attackReward);
    }

    public void OnPlayerKilled()
    {
        AddReward(killReward);
        EndEpisode();
    }

    public void OnDamageTaken()
    {
        AddReward(damagePenalty);
    }

    public void OnDeath()
    {
        EndEpisode();
    }
}
