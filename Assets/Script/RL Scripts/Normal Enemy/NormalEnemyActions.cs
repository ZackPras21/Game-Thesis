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
        RotateAgent(agentTransform, rotateY, turnSpeed);
        MoveAgent(agentTransform, navAgent, moveX, moveZ, moveSpeed);
    }

    private static void RotateAgent(Transform agentTransform, float rotateY, float turnSpeed) =>
        agentTransform.Rotate(0f, rotateY * turnSpeed * Time.deltaTime, 0f);

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
        navAgent.isStopped = false;
        if (speed > 0f) navAgent.speed = speed;
        navAgent.SetDestination(destination);
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
        private readonly RayPerceptionSensorComponent3D raySensor;
        private readonly LayerMask obstacleMask;
        private Transform playerTransform;
        private bool isPlayerVisible;
        private float lastPlayerDistance;
        private Vector3 lastPlayerPosition;

        private float lastPlayerCheckTime;
        private const float PLAYER_CHECK_INTERVAL = 1f; // Reduced for better responsiveness

        public PlayerDetection(RayPerceptionSensorComponent3D raySensor, LayerMask obstacleMask)
        {
            this.raySensor = raySensor;
            this.obstacleMask = obstacleMask;
            FindPlayerTransform();
        }

        public void Reset()
        {
            isPlayerVisible = false;
            lastPlayerDistance = float.MaxValue;
            lastPlayerPosition = Vector3.zero;
            FindPlayerTransform();
        }

        // FIXED: Fully utilize RayPerceptionSensor3D instead of manual raycasting
        public void UpdatePlayerDetection(Vector3 agentPosition)
        {
            isPlayerVisible = false;

            if (!IsPlayerAvailable() || !playerTransform.gameObject.activeInHierarchy)
            {
                if (Time.time - lastPlayerCheckTime > PLAYER_CHECK_INTERVAL)
                {
                    FindPlayerTransform();
                    lastPlayerCheckTime = Time.time;
                }
                if (!IsPlayerAvailable()) return;
            }

            try
            {
                // FIXED: Use RayPerceptionSensor3D for all detection
                var rayOutputs = RayPerceptionSensor.Perceive(raySensor.GetRayPerceptionInput(), false);
                
                // Check all ray outputs for player detection
                foreach (var rayOutput in rayOutputs.RayOutputs)
                {
                    if (rayOutput.HasHit && rayOutput.HitGameObject != null)
                    {
                        if (rayOutput.HitGameObject.CompareTag("Player"))
                        {
                            isPlayerVisible = true;
                            lastPlayerDistance = rayOutput.HitFraction * raySensor.RayLength;
                            lastPlayerPosition = playerTransform.position;
                            break;
                        }
                    }
                }

                // FIXED: Additional validation using RayPerceptionSensor data
                if (isPlayerVisible)
                {
                    float actualDistance = Vector3.Distance(agentPosition, playerTransform.position);
                    // Ensure consistency between sensor data and actual distance
                    if (actualDistance > raySensor.RayLength * 1.1f) // Small tolerance
                    {
                        isPlayerVisible = false;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Player detection error: {e.Message}");
                playerTransform = null;
            }
        }

        private void FindPlayerTransform()
        {
            // Find active player - prefer RL_Player component
            var rlPlayer = Object.FindFirstObjectByType<RL_Player>();
            if (rlPlayer != null && rlPlayer.gameObject.activeInHierarchy)
            {
                playerTransform = rlPlayer.transform;
                return;
            }

            // Fallback to tag-based search
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null && playerObj.activeInHierarchy)
            {
                playerTransform = playerObj.transform;
            }
        }

        // FIXED: Properties using RayPerceptionSensor data when available
        public bool IsPlayerAvailable() => playerTransform != null && playerTransform.gameObject.activeInHierarchy;
        public bool IsPlayerVisible => isPlayerVisible;
        public Vector3 GetPlayerPosition() => playerTransform?.position ?? Vector3.zero;
        public Transform GetPlayerTransform() => playerTransform;
        
        public float GetDistanceToPlayer(Vector3 agentPosition)
        {
            if (!IsPlayerAvailable()) return float.MaxValue;
            
            // Use sensor distance if player is visible, otherwise calculate actual distance
            if (isPlayerVisible && lastPlayerDistance > 0)
            {
                return lastPlayerDistance;
            }
            
            return Vector3.Distance(agentPosition, playerTransform.position);
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

        public bool HasValidPatrolPoints() => patrolPoints != null && patrolPoints.Length > 0;
        public Transform[] GetPatrolPoints() => patrolPoints;
        public int PatrolLoopsCompleted => patrolLoopsCompleted;
        public bool IsIdlingAtSpawn() => isIdlingAtSpawn;
        public float GetIdleTimeRemaining() => isIdlingAtSpawn ? Mathf.Max(0f, IDLE_DURATION_AT_SPAWN - idleTimer) : 0f;
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
            if (navAgent != null && navAgent.enabled)
            {
                navAgent.velocity = Vector3.zero;
                navAgent.isStopped = false;
                navAgent.ResetPath();
            }
        }

        public void ProcessMovement(Vector3 movement, float rotation)
        {
            if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
            {
                // Use NavMeshAgent's built-in obstacle avoidance
                Vector3 worldMovement = agentTransform.TransformDirection(movement).normalized;
                Vector3 targetPosition = agentTransform.position + worldMovement * 0.5f;
                
                // Set destination instead of direct movement for better pathfinding
                navAgent.SetDestination(targetPosition);
                navAgent.speed = moveSpeed;
            }
            else
            {
                // Fallback for direct transform movement
                agentTransform.position += movement * moveSpeed * Time.deltaTime;
            }

            // Apply rotation
            agentTransform.Rotate(0, rotation * turnSpeed * Time.deltaTime, 0);
        }

        public void MoveToTarget(Vector3 targetPosition)
        {
            if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
            {
                navAgent.isStopped = false;
                navAgent.speed = moveSpeed;
                navAgent.SetDestination(targetPosition);
                
                // Add some debug info for patrol movement
                Debug.DrawLine(agentTransform.position, targetPosition, Color.blue, 0.1f);
            }
            else
            {
                // Fallback direct movement with basic obstacle avoidance
                Vector3 direction = (targetPosition - agentTransform.position).normalized;
                
                // Simple raycast for obstacle avoidance
                if (Physics.Raycast(agentTransform.position, direction, out RaycastHit hit, 2f))
                {
                    // Adjust direction slightly to avoid obstacle
                    Vector3 avoidDirection = Vector3.Cross(direction, Vector3.up);
                    direction = (direction + avoidDirection * 0.5f).normalized;
                }
                
                agentTransform.position += direction * moveSpeed * Time.deltaTime;
            }
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
                    Time.deltaTime * turnSpeed
                );
            }
        }
    }
    #endregion
}