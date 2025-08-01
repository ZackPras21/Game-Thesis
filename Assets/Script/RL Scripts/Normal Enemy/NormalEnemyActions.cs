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

            // FIXED: Check if player transform is null or destroyed before accessing it
            if (!IsPlayerAvailable() || playerTransform == null || !playerTransform.gameObject.activeInHierarchy)
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
                            
                            // FIXED: Store last known position safely
                            if (playerTransform != null)
                            {
                                lastPlayerPosition = playerTransform.position;
                            }
                            break;
                        }
                    }
                }

                // FIXED: Additional validation using RayPerceptionSensor data
                if (isPlayerVisible && playerTransform != null)
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
        private int waypointsVisitedInCurrentLoop; // NEW: Track waypoints in current loop

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
            waypointsVisitedInCurrentLoop = 0; 
        }

        public void UpdatePatrol(Vector3 agentPosition, NavMeshAgent navAgent, NormalEnemyRewards rewardConfig, NormalEnemyAgent agent, float deltaTime)
        {
            if (!HasValidPatrolPoints()) return;
            
            if (IsIdlingAtSpawn())
            {
                // Stop any movement during idle
                if (navAgent != null && navAgent.enabled)
                {
                    navAgent.isStopped = true;
                    navAgent.ResetPath();
                }
                UpdateIdleTimer();
                return;
            }

            Vector3 currentTarget = GetCurrentPatrolTarget();
            float distanceToTarget = Vector3.Distance(agentPosition, currentTarget);


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
                // FIXED: Reward based on actual movement, not NavMesh velocity
                Vector3 directionToTarget = GetDirectionToCurrentTarget(agentPosition);
                if (directionToTarget.magnitude > 0.1f)
                {
                    rewardConfig.AddPatrolStepReward(agent, deltaTime);
                }
            }
        }

        // FIXED: New method to get direction for RL movement
        public Vector3 GetDirectionToCurrentTarget(Vector3 agentPosition)
        {
            if (!HasValidPatrolPoints()) return Vector3.zero;
            
            Vector3 targetPosition = patrolPoints[currentPatrolIndex].position;
            Vector3 direction = (targetPosition - agentPosition).normalized;
            return direction;
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

            waypointsVisitedInCurrentLoop++; // FIXED: Increment visited counter
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;

            // FIXED: Only complete loop when we've visited ALL waypoints AND returned to start
            if (currentPatrolIndex == 0 && waypointsVisitedInCurrentLoop >= patrolPoints.Length)
            {
                patrolLoopsCompleted++;
                waypointsVisitedInCurrentLoop = 0; // Reset for next loop
                isIdlingAtSpawn = true;
                idleTimer = 0f;
                Debug.Log($"Patrol loop {patrolLoopsCompleted} completed after visiting all {patrolPoints.Length} waypoints");
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
            waypointsVisitedInCurrentLoop = 0; // FIXED: Reset waypoint counter on respawn
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
        private Vector3 smoothedVelocity; // NEW: For smooth movement
        private const float VELOCITY_SMOOTHING = 0.1f;

        public AgentMovement(NavMeshAgent navAgent, Transform agentTransform, float moveSpeed, float turnSpeed, float attackRange)
        {
            this.navAgent = navAgent;
            this.agentTransform = agentTransform;
            this.moveSpeed = moveSpeed;
            this.turnSpeed = turnSpeed;
            this.attackRange = attackRange;
            this.smoothedVelocity = Vector3.zero;
        }

        public void Reset()
        {
            smoothedVelocity = Vector3.zero; // FIXED: Reset smoothed velocity
            if (navAgent != null && navAgent.enabled)
            {
                navAgent.velocity = Vector3.zero;
                navAgent.isStopped = true;
                navAgent.ResetPath();
            }
        }

        // FIXED: Completely rewritten RL movement for smoothness
        public void ProcessRLMovement(Vector3 movement, float rotation)
        {
            // FIXED: Completely disable NavMesh for RL control
            if (navAgent != null && navAgent.enabled)
            {
                navAgent.isStopped = true;
                navAgent.ResetPath();
                navAgent.velocity = Vector3.zero;
            }

            // FIXED: Wall avoidance integrated into movement
            Vector3 targetVelocity = Vector3.zero;
            
            if (movement.magnitude > 0.1f)
            {
                // Transform to world space
                Vector3 worldMovement = agentTransform.TransformDirection(movement);
                
                // FIXED: Apply wall avoidance before setting velocity
                Vector3 avoidedMovement = ApplyWallAvoidance(worldMovement);
                targetVelocity = avoidedMovement.normalized * moveSpeed;
            }

            // FIXED: Smooth velocity interpolation
            smoothedVelocity = Vector3.Lerp(smoothedVelocity, targetVelocity, Time.fixedDeltaTime * 10f);

            // FIXED: Apply movement with obstacle checking
            if (smoothedVelocity.magnitude > 0.01f)
            {
                Vector3 newPosition = agentTransform.position + smoothedVelocity * Time.fixedDeltaTime;
                
                // FIXED: Check for obstacles before moving
                if (IsPositionValid(newPosition) && !WillHitObstacle(newPosition))
                {
                    agentTransform.position = newPosition;
                }
                else
                {
                    // FIXED: Try alternative directions when blocked
                    Vector3 alternativePosition = FindAlternativePosition();
                    if (alternativePosition != Vector3.zero)
                    {
                        agentTransform.position = alternativePosition;
                    }
                }
            }
            
            // FIXED: Smooth rotation
            if (Mathf.Abs(rotation) > 0.01f)
            {
                float rotationAmount = rotation * turnSpeed * Time.fixedDeltaTime;
                agentTransform.Rotate(0, rotationAmount, 0, Space.Self);
            }
        }

        private Vector3 ApplyWallAvoidance(Vector3 intendedMovement)
        {
            Vector3 avoidanceForce = Vector3.zero;
            float avoidanceDistance = 2f;
            
            // Check multiple directions for obstacles
            Vector3[] checkDirections = {
                intendedMovement.normalized,
                (intendedMovement + agentTransform.right * 0.5f).normalized,
                (intendedMovement - agentTransform.right * 0.5f).normalized
            };
            
            foreach (Vector3 direction in checkDirections)
            {
                if (Physics.Raycast(agentTransform.position + Vector3.up * 0.5f, direction, out RaycastHit hit, avoidanceDistance))
                {
                    if (((1 << hit.collider.gameObject.layer) & LayerMask.GetMask("Obstacle", "Wall")) != 0)
                    {
                        // Calculate avoidance force perpendicular to the wall
                        Vector3 wallNormal = hit.normal;
                        wallNormal.y = 0; // Keep horizontal
                        avoidanceForce += wallNormal * (avoidanceDistance - hit.distance) / avoidanceDistance;
                    }
                }
            }
            
            // Combine intended movement with avoidance
            Vector3 finalMovement = intendedMovement + avoidanceForce * 2f;
            return finalMovement.magnitude > 0.01f ? finalMovement : intendedMovement;
        }

        private bool WillHitObstacle(Vector3 newPosition)
        {
            Vector3 direction = (newPosition - agentTransform.position).normalized;
            float distance = Vector3.Distance(agentTransform.position, newPosition);
            
            return Physics.SphereCast(
                agentTransform.position + Vector3.up * 0.5f,
                0.4f,
                direction,
                out RaycastHit hit,
                distance + 0.2f,
                LayerMask.GetMask("Obstacle", "Wall")
            );
        }

        private Vector3 FindAlternativePosition()
        {
            // Try alternative directions when blocked
            Vector3[] alternatives = {
                agentTransform.position + agentTransform.right * 0.5f * Time.fixedDeltaTime,
                agentTransform.position - agentTransform.right * 0.5f * Time.fixedDeltaTime,
                agentTransform.position + agentTransform.forward * 0.3f * Time.fixedDeltaTime,
                agentTransform.position - agentTransform.forward * 0.3f * Time.fixedDeltaTime
            };
            
            foreach (Vector3 altPos in alternatives)
            {
                if (IsPositionValid(altPos) && !WillHitObstacle(altPos))
                {
                    return altPos;
                }
            }
            
            return Vector3.zero; // No valid alternative found
        }

        // FIXED: Simple boundary check without NavMesh dependency
        private bool IsPositionValid(Vector3 position)
        {
            // Check if position is on NavMesh (for boundary only)
            return UnityEngine.AI.NavMesh.SamplePosition(position, out UnityEngine.AI.NavMeshHit hit, 0.5f, UnityEngine.AI.NavMesh.AllAreas);
        }

        // FIXED: Smoother target facing
        public void FaceTarget(Vector3 targetPosition)
        {
            Vector3 direction = (targetPosition - agentTransform.position);
            direction.y = 0;
            
            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                // FIXED: Use FixedDeltaTime for consistent rotation
                agentTransform.rotation = Quaternion.Slerp(
                    agentTransform.rotation,
                    targetRotation,
                    turnSpeed * Time.fixedDeltaTime
                );
            }
        }

        public bool IsPlayerInAttackRange(Vector3 playerPosition) =>
            Vector3.SqrMagnitude(agentTransform.position - playerPosition) <= attackRange * attackRange;

        public void StopMovement()
        {
            if (navAgent != null && navAgent.enabled)
            {
                navAgent.isStopped = true;
                navAgent.ResetPath();
                navAgent.velocity = Vector3.zero;
            }
        }
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