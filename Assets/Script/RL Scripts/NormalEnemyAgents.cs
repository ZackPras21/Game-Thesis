using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class NormalEnemyAgents : Agent
{
    [Header("References")]
    public EnemyController enemyController;
    
    [Header("RL Settings")]
    public float detectionReward = 0.1f;
    public float attackReward = 0.2f;
    public float killReward = 1f;
    public float damagePenalty = -0.5f;
    public float idlePenalty = -0.01f;
    
    private int _currentEpisode = 0;
    private float _cumulativeReward = 0f;
    private Vector3 _lastPosition;
    private float _positionCheckInterval = 0.5f;
    private float _positionCheckTimer = 0f;

    public override void Initialize()
    {
        _currentEpisode = 0;
        _cumulativeReward = 0f;
        _lastPosition = transform.position;
        
        if (enemyController == null)
            enemyController = GetComponent<EnemyController>();
    }

    public override void OnEpisodeBegin()
    {
        _currentEpisode++;
        _cumulativeReward = 0f;
        _lastPosition = transform.position;
        enemyController.SetupInitialValues();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Enemy state observations
        sensor.AddObservation(enemyController.m_PlayerInRange);
        sensor.AddObservation(enemyController.m_PlayerNear);
        sensor.AddObservation(enemyController.m_IsPatrol);
        sensor.AddObservation(enemyController.m_IsAttacking);
        sensor.AddObservation((int)enemyController.enemyType);
        sensor.AddObservation(enemyController.GetHealthPercentage());
        
        // Navigation observations
        if (enemyController.navMeshAgent != null)
        {
            sensor.AddObservation(enemyController.navMeshAgent.velocity.magnitude);
            sensor.AddObservation(enemyController.navMeshAgent.remainingDistance);
        }
        
        // Player position observations
        if (PlayerController.Instance != null)
        {
            float distance = Vector3.Distance(
                transform.position,
                PlayerController.Instance.transform.position);
            sensor.AddObservation(distance / enemyController.viewRadius);
            
            Vector3 direction = (PlayerController.Instance.transform.position - transform.position).normalized;
            sensor.AddObservation(direction.x);
            sensor.AddObservation(direction.z);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Action 0: Movement state (0=idle, 1=patrol, 2=chase)
        int moveState = actions.DiscreteActions[0];
        
        // Action 1: Attack (0=no, 1=yes)
        int attack = actions.DiscreteActions[1];
        
        // Action 2: Special behavior based on HP (0=normal, 1=low HP behavior)
        int specialBehavior = actions.DiscreteActions[2];
        
        // Update enemy behavior based on actions
        switch (moveState)
        {
            case 0: // Idle
                enemyController.m_IsPatrol = false;
                enemyController.navMeshAgent.isStopped = true;
                AddReward(idlePenalty);
                break;
                
            case 1: // Patrol
                enemyController.m_IsPatrol = true;
                enemyController.navMeshAgent.isStopped = false;
                break;
                
            case 2: // Chase
                enemyController.m_IsPatrol = false;
                enemyController.navMeshAgent.isStopped = false;
                break;
        }
        
        // Handle attack
        enemyController.m_IsAttacking = attack == 1;
        
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
                    enemyController.navMeshAgent.SetDestination(circlePos);
                }
                break;
                
            case EnemyType.Medium1: // Humanoid
                // Wait for other enemies
                if (behaviorType == 1)
                {
                    enemyController.navMeshAgent.isStopped = true;
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
                        enemyController.navMeshAgent.SetDestination(transform.position + fleeDirection.normalized * 5f);
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
        EnemyController[] enemies = FindObjectsOfType<EnemyController>();
        foreach (EnemyController enemy in enemies)
        {
            if (enemy != enemyController && !enemy.IsDead())
                return true;
        }
        return false;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = Input.GetKey(KeyCode.Space) ? 1 : 0; // Chase
        discreteActions[1] = Input.GetKey(KeyCode.A) ? 1 : 0; // Attack
        discreteActions[2] = 0; // Default special behavior
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
