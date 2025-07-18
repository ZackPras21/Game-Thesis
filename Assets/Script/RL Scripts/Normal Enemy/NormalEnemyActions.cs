// FileName: /NormalEnemyActions.cs
using UnityEngine;
using UnityEngine.AI;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using System.Linq;

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
    #region Constants
    private const float MOVEMENT_DESTINATION_OFFSET = 0.5f;
    private const float WAYPOINT_ARRIVAL_TOLERANCE = 0.1f; //unimplemented
    #endregion

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

    public static void SetAgentDestination(NavMeshAgent navAgent, Vector3 destination, float speed = 0f)
    {
        navAgent.isStopped = false;
        if (speed > 0f) navAgent.speed = speed;
        navAgent.SetDestination(destination);
    }

    public static void StopAgent(NavMeshAgent navAgent)
    {
        navAgent.velocity = Vector3.zero;
        navAgent.isStopped = true;
    }
    #endregion
    #region Action Classes

    public class PlayerDetection
    {
        private readonly RayPerceptionSensorComponent3D raySensor;
        private readonly LayerMask obstacleMask;
        private Transform playerTransform;
        private bool isPlayerVisible;

        private float lastPlayerCheckTime;
        private const float PLAYER_CHECK_INTERVAL = 2f; 

        public PlayerDetection(RayPerceptionSensorComponent3D raySensor, LayerMask obstacleMask)
        {
            this.raySensor = raySensor;
            this.obstacleMask = obstacleMask;
            FindPlayerTransform();
        }

        public void Reset()
        {
            isPlayerVisible = false;
            FindPlayerTransform(); // Re-find player on reset
        }

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
                // Additional null check for destroyed player
                if (playerTransform == null || !playerTransform.gameObject.activeInHierarchy)
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
            catch (MissingReferenceException)
            {
                playerTransform = null; // Player object was destroyed
                FindPlayerTransform(); // Try to find the new player
            }
        }

        private void FindPlayerTransform()
        {
            playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (playerTransform == null)
            {
                var player = Object.FindFirstObjectByType<RL_Player>();
                if (player != null && player.gameObject.activeInHierarchy)
                {
                    playerTransform = player.transform;
                }
            }
        }

        private void CheckRayPerceptionVisibility()
        {
            var rayOutputs = RayPerceptionSensor.Perceive(raySensor.GetRayPerceptionInput(), false);
            isPlayerVisible = rayOutputs.RayOutputs.Any(ray =>
                ray.HasHit && ray.HitGameObject != null && ray.HitGameObject.CompareTag("Player"));
        }

        private void VerifyLineOfSight(Vector3 agentPosition, float distanceToPlayer)
        {
            Vector3 directionToPlayer = (playerTransform.position - agentPosition).normalized;
            Vector3 rayOrigin = agentPosition + directionToPlayer * 0.5f;

            if (Physics.Raycast(rayOrigin, directionToPlayer, out RaycastHit hit, distanceToPlayer, obstacleMask))
            {
                if (hit.collider != null && !hit.collider.CompareTag("Player"))
                    isPlayerVisible = false;
            }
        }

        public bool IsPlayerAvailable() => playerTransform != null && playerTransform.gameObject.activeInHierarchy;
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

        public void UpdatePatrol(Vector3 agentPosition, NormalEnemyRewards.RewardSystem rewardSystem)
        {
            if (patrolPoints == null || patrolPoints.Length == 0) return;

            Vector3 targetWaypoint = patrolPoints[currentPatrolIndex].position;

            if (Vector3.Distance(agentPosition, targetWaypoint) < PATROL_WAYPOINT_TOLERANCE)
            {
                AdvanceToNextWaypoint();
                rewardSystem.AddPatrolReward();
            }
        }

        public string GetNearestPointName(Vector3 agentPosition)
        {
            if (patrolPoints == null || patrolPoints.Length == 0) return "None";

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

        public bool HasReachedNewPatrolPoint()
        {
            // This can be used to track if agent reached a new patrol point
            // Implementation depends on your specific needs
            return false; // Placeholder - implement based on your requirements
        }

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
            if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
            {
                navAgent.Move(movement * moveSpeed * Time.deltaTime);
            }
            else
            {
                agentTransform.position += movement * moveSpeed * Time.deltaTime;
            }

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
                    Time.deltaTime * turnSpeed
                );
            }
        }
    }
    #endregion
}
