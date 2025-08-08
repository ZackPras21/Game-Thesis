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

        public void UpdatePlayerDetection(Vector3 agentPosition)
        {
            isPlayerVisible = false;

            if (!IsPlayerAvailable())
            {
                if (Time.time - lastPlayerCheckTime > PLAYER_CHECK_INTERVAL)
                {
                    FindPlayerTransform();
                    lastPlayerCheckTime = Time.time;
                }
                return;
            }

            try
            {
                var rayOutputs = RayPerceptionSensor.Perceive(raySensor.GetRayPerceptionInput(), false);
                
                foreach (var rayOutput in rayOutputs.RayOutputs)
                {
                    if (rayOutput.HasHit && rayOutput.HitGameObject?.CompareTag("Player") == true)
                    {
                        isPlayerVisible = true;
                        lastPlayerDistance = rayOutput.HitFraction * raySensor.RayLength;
                        lastPlayerPosition = playerTransform.position;
                        break;
                    }
                }
            }
            catch (System.Exception)
            {
                playerTransform = null;
                isPlayerVisible = false;
            }
        }

        private void FindPlayerTransform()
        {
            // Clear existing reference first
            playerTransform = null;
            
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

        // FIXED: Enhanced null checking for all properties and methods
        public bool IsPlayerAvailable() 
        {
            return playerTransform != null && 
                playerTransform.gameObject != null && 
                playerTransform.gameObject.activeInHierarchy;
        }
        
        public bool IsPlayerVisible => isPlayerVisible && IsPlayerAvailable();
        
        // FIXED: Safe position access with fallback to last known position
        public Vector3 GetPlayerPosition() 
        {
            if (IsPlayerAvailable())
            {
                try
                {
                    Vector3 currentPos = playerTransform.position;
                    lastPlayerPosition = currentPos; // Update last known position
                    return currentPos;
                }
                catch (System.Exception)
                {
                    // Player transform was destroyed between null check and access
                    playerTransform = null;
                }
            }
            
            // Return last known position if available, otherwise zero
            return lastPlayerPosition != Vector3.zero ? lastPlayerPosition : Vector3.zero;
        }
        
        public Transform GetPlayerTransform() => IsPlayerAvailable() ? playerTransform : null;

        public float GetDistanceToPlayer(Vector3 agentPosition)
        {
            if (!IsPlayerAvailable()) return float.MaxValue;

            try
            {
                Vector3 playerPos = GetPlayerPosition();
                
                // Use sensor distance if player is visible, otherwise calculate actual distance
                if (isPlayerVisible && lastPlayerDistance > 0)
                {
                    return lastPlayerDistance;
                }

                return Vector3.Distance(agentPosition, playerPos);
            }
            catch (System.Exception)
            {
                // If we can't get distance, return max value
                return float.MaxValue;
            }
        }
    }

   public class PatrolSystem
    {
        private Transform[] patrolPoints;
        private int currentPatrolIndex;
        private int patrolLoopsCompleted;
        private bool isIdlingAtSpawn;
        private float idleTimer;
        private const float IDLE_DURATION_AT_SPAWN = 2f;

        public PatrolSystem(Transform[] patrolPoints)
        {
            this.patrolPoints = patrolPoints;
            Reset();
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

        public bool AdvanceToNextWaypoint()
        {
            if (!HasValidPatrolPoints()) return false;

            int nextIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            currentPatrolIndex = nextIndex;

            if (nextIndex == 0)
            {
                patrolLoopsCompleted++;
                isIdlingAtSpawn = true;
                idleTimer = 0f;
                Debug.Log($"Patrol loop {patrolLoopsCompleted} completed. Starting idle period at {GetCurrentPatrolPointName()}");
                return true;
            }

            return false;
        }

        public bool UpdateIdleTimer()
        {
            if (!isIdlingAtSpawn) return false;

            idleTimer += Time.deltaTime;

            if (idleTimer >= IDLE_DURATION_AT_SPAWN)
            {
                isIdlingAtSpawn = false;
                idleTimer = 0f;
                return true;
            }

            return false;
        }

        public void Reset()
        {
            currentPatrolIndex = 0;
            patrolLoopsCompleted = 0;
            isIdlingAtSpawn = false;
            idleTimer = 0f;
        }

        public void ResetToSpecificPoint(int pointIndex)
        {
            if (patrolPoints == null || patrolPoints.Length == 0) return;
            
            currentPatrolIndex = Mathf.Clamp(pointIndex, 0, patrolPoints.Length - 1);
            patrolLoopsCompleted = 0;
            isIdlingAtSpawn = false;
            idleTimer = 0f;
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
    
    public class FleeState
    {
        private bool isFleeing;
        private Vector3 fleeDirection;
        private float fleeTimer;
        
        public bool IsFleeing => isFleeing;
        public Vector3 FleeDirection => fleeDirection;
        public float FleeTimer => fleeTimer;
        
        public void StartFleeing(Vector3 direction)
        {
            isFleeing = true;
            fleeDirection = direction.normalized;
            fleeTimer = 0f;
        }
        
        public void StopFleeing()
        {
            isFleeing = false;
            fleeTimer = 0f;
        }
        
        public void UpdateTimer()
        {
            if (isFleeing)
            {
                fleeTimer += Time.deltaTime;
            }
        }
        
        public void Reset()
        {
            isFleeing = false;
            fleeTimer = 0f;
            fleeDirection = Vector3.zero;
        }
    }

    #endregion
}