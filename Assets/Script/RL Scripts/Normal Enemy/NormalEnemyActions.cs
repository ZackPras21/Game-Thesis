using System.Linq;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.AI;

public enum EnemyHighLevelAction
{
    Idle = 0,
    Patrol = 1,
    Detect = 2,
    Chase = 3,
    Attack = 4,
    Dead = 5
}

public sealed class NormalEnemyActions
{
    private const float IDLE_DURATION_AT_SPAWN = 5.0f;
    private const float MOVEMENT_DESTINATION_OFFSET = 0.5f;

    #region Movement Actions
    public void ApplyMovement(
    Transform agentTransform,
    NavMeshAgent navAgent,
    float moveX,
    float moveZ,
    float rotateY,
    float moveSpeed,
    float turnSpeed)
    {
        // Apply rotation first, then movement
        RotateAgent(agentTransform, rotateY, turnSpeed);
        MoveAgent(agentTransform, navAgent, moveX, moveZ, moveSpeed);
    }

    private static void RotateAgent(Transform agentTransform, float rotateY, float turnSpeed)
    {
        // Ensure smooth rotation with clamped speed
        float clampedRotation = Mathf.Clamp(rotateY * turnSpeed * Time.deltaTime, -180f, 180f);
        agentTransform.Rotate(0f, clampedRotation, 0f);
    }

    private void MoveAgent(Transform agentTransform, NavMeshAgent navAgent, float moveX, float moveZ, float moveSpeed)
    {
        Vector3 localMovement = new(moveX, 0f, moveZ);
        Vector3 worldMovement = agentTransform.TransformDirection(localMovement).normalized;

        if (HasMovementInput(moveX, moveZ))
            SetAgentDestination(navAgent, agentTransform.position + worldMovement * MOVEMENT_DESTINATION_OFFSET, moveSpeed);
        else
            StopAgent(navAgent);
    }

    private static bool HasMovementInput(float moveX, float moveZ) =>
        moveZ != 0f || moveX != 0f;

    private static void SetAgentDestination(NavMeshAgent navAgent, Vector3 destination, float speed = 0f)
    {
        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = false;
            if (speed > 0f) 
            {
                navAgent.speed = speed; // Use the provided speed consistently
            }
            navAgent.SetDestination(destination);
        }
    }

    private static void StopAgent(NavMeshAgent navAgent)
    {
        navAgent.velocity = Vector3.zero;
        navAgent.isStopped = true;
    }
    #endregion

    #region Action Helper Class 
    public class PlayerDetection
    {
        protected Transform playerTransform;
        protected bool isPlayerVisible;
        protected Vector3 lastPlayerPosition;
        protected float lastPlayerCheckTime;
        protected const float PLAYER_CHECK_INTERVAL = 1f;

        public virtual void Reset()
        {
            isPlayerVisible = false;
            lastPlayerPosition = Vector3.zero;
            FindPlayerTransform();
        }

        protected void FindPlayerTransform()
        {
            playerTransform = null;
            
            var rlPlayer = Object.FindFirstObjectByType<RL_Player>();
            if (rlPlayer != null && rlPlayer.gameObject.activeInHierarchy)
            {
                playerTransform = rlPlayer.transform;
                return;
            }

            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null && playerObj.activeInHierarchy)
            {
                playerTransform = playerObj.transform;
            }
        }

        public bool IsPlayerAvailable() 
        {
            return playerTransform != null && 
                playerTransform.gameObject != null && 
                playerTransform.gameObject.activeInHierarchy;
        }
        
        public bool IsPlayerVisible => isPlayerVisible && IsPlayerAvailable();
        
        public Vector3 GetPlayerPosition() 
        {
            if (IsPlayerAvailable())
            {
                try
                {
                    Vector3 currentPos = playerTransform.position;
                    lastPlayerPosition = currentPos;
                    return currentPos;
                }
                catch (System.Exception)
                {
                    playerTransform = null;
                }
            }
            
            return lastPlayerPosition != Vector3.zero ? lastPlayerPosition : Vector3.zero;
        }
        
        public virtual float GetDistanceToPlayer(Vector3 agentPosition)
        {
            if (!IsPlayerAvailable()) return float.MaxValue;
            return Vector3.Distance(agentPosition, GetPlayerPosition());
        }
    }

    public class PatrolSystem
    {
        private Transform[] patrolPoints;
        private int currentPatrolIndex;
        private int patrolLoopsCompleted;
        private bool isIdlingAtSpawn;
        private float idleTimer;

        private const float PATROL_WAYPOINT_TOLERANCE = 2f;
        private const float IDLE_DURATION_AT_SPAWN = 2f;

        public PatrolSystem(Transform[] patrolPoints)
        {
            this.patrolPoints = patrolPoints;
            Reset();
        }

        public void Reset()
        {
            currentPatrolIndex = 0;
            patrolLoopsCompleted = 0;
            isIdlingAtSpawn = false;
            idleTimer = 0f;
        }

        public void ResetToFirstPoint()
        {
            currentPatrolIndex = 0;
            isIdlingAtSpawn = false;
            idleTimer = 0f;
        }

        public void UpdatePatrol(Vector3 agentPosition, NavMeshAgent navAgent, NormalEnemyRewards rewardConfig, NormalEnemyAgent agent, float deltaTime)
        {
            if (!HasValidPatrolPoints()) return;
            if (IsIdlingAtSpawn())
            {
                if (navAgent != null && navAgent.enabled)
                {
                    navAgent.isStopped = true;
                }

                UpdateIdleTimer();
                return;
            }

            Vector3 currentTarget = GetCurrentPatrolTarget();
            float distanceToTarget = Vector3.Distance(agentPosition, currentTarget);

            if (navAgent != null && navAgent.enabled)
            {
                navAgent.isStopped = false;
                navAgent.SetDestination(currentTarget);

                if (navAgent.speed <= 0)
                {
                    navAgent.speed = 3.5f;
                }
            }

            // Check if reached waypoint
            if (distanceToTarget < PATROL_WAYPOINT_TOLERANCE)
            {
                bool completedLoop = AdvanceToNextWaypoint();
                if (completedLoop)
                {
                    rewardConfig.AddPatrolReward(agent);
                }
            }
            else
            {
                if (navAgent != null && navAgent.velocity.magnitude > 0.1f)
                {
                    rewardConfig.AddPatrolStepReward(agent, deltaTime);
                }
            }
        }

        public Vector3 GetCurrentPatrolTarget()
        {
            if (!HasValidPatrolPoints()) return Vector3.zero;
            return patrolPoints[currentPatrolIndex].position;
        }

        public string GetCurrentPatrolPointName()
        {
            if (!HasValidPatrolPoints()) return "None";

            string pointName = patrolPoints[currentPatrolIndex].gameObject.name;
            return string.IsNullOrEmpty(pointName) ? $"Point {currentPatrolIndex + 1}" : pointName;
        }

        // Fixed: Handle waypoint advancement with proper sequencing
        public bool AdvanceToNextWaypoint()
        {
            if (!HasValidPatrolPoints()) return false;

            // Move to next waypoint
            int nextIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            currentPatrolIndex = nextIndex;

            // Check if completing a full loop (back to point A/index 0)
            if (nextIndex == 0)
            {
                patrolLoopsCompleted++;
                isIdlingAtSpawn = true;
                idleTimer = 0f;
                Debug.Log($"Patrol loop {patrolLoopsCompleted} completed. Starting idle period at {GetCurrentPatrolPointName()}");
                return true; // Completed a loop
            }

            return false; // Normal waypoint advancement
        }

        // Fixed: Add method to update idle timer and return completion status
        public bool UpdateIdleTimer()
        {
            if (!isIdlingAtSpawn) return false;

            idleTimer += Time.deltaTime;

            if (idleTimer >= IDLE_DURATION_AT_SPAWN)
            {
                isIdlingAtSpawn = false;
                idleTimer = 0f;
                return true; // Idle complete
            }

            return false; // Still idling
        }

        public void SetPatrolPoints(Transform[] points)
        {
            patrolPoints = points;
            Reset();
        }

        public void ResetToSpecificPoint(int pointIndex)
        {
            if (patrolPoints == null || patrolPoints.Length == 0) return;
            
            currentPatrolIndex = Mathf.Clamp(pointIndex, 0, patrolPoints.Length - 1);
            patrolLoopsCompleted = 0;
            isIdlingAtSpawn = false;
            idleTimer = 0f;
        }

        public bool HasValidPatrolPoints() => patrolPoints != null && patrolPoints.Length > 0;
        public Transform[] GetPatrolPoints() => patrolPoints;
        public int PatrolLoopsCompleted => patrolLoopsCompleted;
        public bool IsIdlingAtSpawn() => isIdlingAtSpawn;
        public float GetIdleTimeRemaining() => isIdlingAtSpawn ? Mathf.Max(0f, IDLE_DURATION_AT_SPAWN - idleTimer) : 0f;
    }

    public class FleeState
    {
        public bool IsFleeing { get; private set; }
        public Vector3 FleeDirection { get; private set; }
        public float FleeTimer { get; private set; } 
        public void StartFleeing(Vector3 direction)
        {
            IsFleeing = true;
            FleeDirection = direction;
            FleeTimer = 0f; 
        }
        
        public void StopFleeing()
        {
            IsFleeing = false;
            FleeTimer = 0f;
        }
        
        public void UpdateTimer()
        {
            if (IsFleeing)
            {
                FleeTimer += Time.deltaTime;
            }
        }
    }
    #endregion
}