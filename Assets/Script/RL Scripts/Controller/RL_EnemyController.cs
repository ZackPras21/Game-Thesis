using System.Collections;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.AI;

public class RL_EnemyController : MonoBehaviour
{
    #region Serialized Fields
    [Header("Combat Configuration")]
    [SerializeField] private float fleeHealthThreshold = 0.2f;
    [SerializeField] private float fleeSpeed = 5f;
    [SerializeField] private float fleeDistance = 8f;
    [SerializeField] private float fleeDetectionRadius = 10f;
    [SerializeField] private HealthBar healthBar;
    [SerializeField] private BoxCollider attackCollider;

    [Header("Movement Configuration")]
    [SerializeField] public float rotationSpeed = 5f;
    [SerializeField] public float moveSpeed = 3f;
    [SerializeField] private float waypointThreshold = 0.5f;
    [SerializeField] public Transform[] waypoints;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private float startWaitTime = 4f;

    [Header("Component References")]
    [SerializeField] private Animator animator;
    [SerializeField] private LootManager lootManager;
    [SerializeField] private VFXManager vfxManager;
    [SerializeField] private Transform particlePosition;
    [SerializeField] private EnemyType enemyType;
    #endregion

    #region Public Properties & Variables
    public EnemyData enemyData;
    public bool IsInitialized { get; private set; }
    public CombatState combatState;
    public HealthState healthState;
    public int enemyHP;
    public float attackRange = 3f;
    public EnhancedPlayerDetection playerDetection;
    public UnifiedMovementSystem movementSystem;
    #endregion

    #region Private Variables
    private PlayerTrackingState playerTracking;
    private WaypointNavigationState waypointNavigation;
    private EnemyStatDisplay statDisplay;
    private Rigidbody rigidBody;
    private NormalEnemyActions.FleeState fleeState;
    private KnockbackState knockbackState;

    private const float ATTACK_DURATION = 1f;
    private const float ATTACK_COOLDOWN = 2f;
    private const float KNOCKBACK_FORCE = 3f;
    private const float KNOCKBACK_DURATION = 0.3f;
    private const float DESTROY_DELAY = 8f;
    #endregion

    #region Unity Lifecycle
    private void Awake() => ForceInitialize();

    private void Update()
    {
        if (!IsInitialized) return;

        knockbackState.UpdateKnockback();
        fleeState.UpdateTimer(); // FIX: Update flee timer
        UpdatePlayerTracking();

        if (ShouldFlee())
            HandleFleeingBehavior();
        else if (!knockbackState.IsKnockedBack)
        {
            HandleCombatBehavior();
            HandleMovementBehavior();
        }

        UpdateAnimationStates();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayerHitbox(other))
        {
            HandlePlayerEnterCombat();
            statDisplay?.ShowEnemyStats();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayerHitbox(other))
        {
            HandlePlayerExitCombat();
            statDisplay?.HideEnemyStats();
        }
    }

    private void OnEnable() => RL_Player.OnPlayerDestroyed += HandlePlayerDestroyed;
    private void OnDisable() => RL_Player.OnPlayerDestroyed -= HandlePlayerDestroyed;
    #endregion

    #region Initialization
    public void ForceInitialize()
    {
        if (IsInitialized) return;

        InitializeComponents();
        InitializeStates();
        InitializeMovementSystems();
        SetupEnemyData();
        SetAnimationState(idle: true);
        IsInitialized = true;
    }

    private void InitializeMovementSystems()
    {
        var normalEnemyAgent = GetComponent<NormalEnemyAgent>();
        if (normalEnemyAgent != null)
        {
            var rayPerceptionSensor = GetComponent<RayPerceptionSensorComponent3D>();
            var navAgent = GetComponent<NavMeshAgent>();
            
            if (rayPerceptionSensor != null && navAgent != null)
            {
                playerDetection = new EnhancedPlayerDetection(rayPerceptionSensor, obstacleMask);
                movementSystem = new UnifiedMovementSystem(navAgent, transform, moveSpeed, 
                    rotationSpeed, attackRange, playerDetection);
            }
        }
    }

    private void InitializeComponents()
    {
        statDisplay = GetComponent<EnemyStatDisplay>();
        rigidBody = GetComponent<Rigidbody>();
    }

    private void InitializeStates()
    {
        playerTracking = new PlayerTrackingState();
        waypointNavigation = new WaypointNavigationState(waypoints, startWaitTime);
        combatState = new CombatState();
        healthState = new HealthState();
        knockbackState = new KnockbackState();
        fleeState = new NormalEnemyActions.FleeState();
    }

    private void SetupEnemyData()
    {
        enemyData ??= GetEnemyDataByType() ?? CreateDefaultEnemyData();
        enemyHP = enemyData.enemyHealth;
        InitializeHealthBar();
    }

    private EnemyData GetEnemyDataByType()
    {
        return enemyType switch
        {
            EnemyType.Creep => CreepEnemyData.Instance,
            EnemyType.Medium1 => Medium1EnemyData.Instance,
            EnemyType.Medium2 => Medium2EnemyData.Instance,
            _ => null
        };
    }

    private EnemyData CreateDefaultEnemyData()
    {
        Debug.LogError($"Failed to get enemy data for type: {enemyType}", gameObject);
        var defaultData = ScriptableObject.CreateInstance<EnemyData>();
        defaultData.enemyHealth = 100;
        defaultData.enemyAttack = 10;
        return defaultData;
    }

    public void ReinitializeData()
    {
        if (enemyData == null) SetupEnemyData();
    }

    public void InitializeHealthBar()
    {
        if (healthBar != null && enemyData != null)
        {
            healthBar.SetMaxHealth(enemyData.enemyHealth);
            healthBar.SetHealth(enemyHP);
        }
    }
    #endregion

    #region Player Tracking
    private void UpdatePlayerTracking()
    {
        if (playerTracking.PlayerTransform == null)
        {
            playerTracking.SetInRange(false);
            waypointNavigation.SetPatrolling(true);
            SetAnimationState(walking: true);
        }
    }

    public void SetTarget(Transform target)
    {
        if (target == null || !playerTracking.IsPlayerAlive)
        {
            playerTracking.ClearTarget();
            return;
        }

        playerTracking.SetTarget(target);
        waypointNavigation.SetPatrolling(false);
    }

    private void HandlePlayerDestroyed() => playerTracking.HandlePlayerDestroyed();
    #endregion

    #region Combat
    private void HandleCombatBehavior()
    {
        if (fleeState.IsFleeing || knockbackState.IsKnockedBack) return;

        if (playerTracking.IsInRange && playerTracking.PlayerTransform != null && playerTracking.IsPlayerAlive)
        {
            RotateTowardsTarget(playerTracking.PlayerPosition);

            if (combatState.CanAttack && !ShouldFlee())
                StartCoroutine(ExecuteAttackSequence());
        }
        else if (!combatState.IsAttacking)
        {
            SetAnimationState(idle: true);
        }
    }

    private void HandlePlayerEnterCombat()
    {
        combatState.SetAttacking(true);
        combatState.SetCanAttack(true);
    }

    private void HandlePlayerExitCombat() => combatState.SetAttacking(false);

    private IEnumerator ExecuteAttackSequence()
    {
        combatState.SetCanAttack(false);
        combatState.SetAttacking(true);

        if (attackCollider != null) attackCollider.enabled = true;

        SetAnimationState(attacking: true);
        yield return new WaitForSeconds(ATTACK_DURATION);

        if (attackCollider != null) attackCollider.enabled = false;

        SetAnimationState(attacking: false, idle: true);
        yield return new WaitForSeconds(ATTACK_COOLDOWN);

        combatState.SetAttacking(false);
        combatState.SetCanAttack(true);
    }

    public void AgentAttack()
    {
        if (combatState.CanAttack)
            StartCoroutine(ExecuteAttackSequence());
    }

    public void AttackEnd()
    {
        ExecuteAttackDamage();
        SetAnimationState(idle: true);
    }

    private void ExecuteAttackDamage()
    {
        var hitTargets = Physics.OverlapBox(
            attackCollider.bounds.center,
            attackCollider.bounds.extents,
            attackCollider.transform.rotation,
            LayerMask.GetMask("Player"));

        foreach (var target in hitTargets)
        {
            var player = target.GetComponent<RL_Player>();
            if (player != null && player.CurrentHealth > 0f)
            {
                player.DamagePlayer(enemyData.enemyAttack);
                PlayAttackSound();
            }
        }
    }

    private bool ShouldFlee()
    {
        return IsHealthLow() && playerTracking.IsPlayerAlive &&
            Vector3.Distance(transform.position, playerTracking.PlayerPosition) <= fleeDetectionRadius;
    }

    private void HandleFleeingBehavior()
    {
        if (!fleeState.IsFleeing)
            InitiateFlee();

        ExecuteFleeWithTimeout();
        SetAnimationState(walking: true);
    }

    private void InitiateFlee()
    {
        Vector3 fleeDirection = CalculateFleeDirection();
        fleeState.StartFleeing(fleeDirection);
        waypointNavigation.SetPatrolling(false);
    }

    private Vector3 CalculateFleeDirection()
    {
        Vector3 playerDirection = (transform.position - playerTracking.PlayerPosition).normalized;
        Vector3 fleeDirection = playerDirection;

        // Add some randomness to flee direction
        fleeDirection += Random.insideUnitSphere * 0.3f;
        fleeDirection.y = 0;
        return fleeDirection.normalized;
    }

    private void ExecuteFleeWithTimeout()
    {
        if (playerTracking.IsPlayerAlive && playerTracking.PlayerTransform != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTracking.PlayerPosition);

            if (distanceToPlayer >= fleeDistance || fleeState.FleeTimer > 5f)
            {
                fleeState.StopFleeing();
                waypointNavigation.SetPatrolling(true);
                return;
            }
        }
        else
        {
            fleeState.StopFleeing();
            waypointNavigation.SetPatrolling(true);
            return;
        }

        Vector3 fleeTarget = transform.position + fleeState.FleeDirection * fleeDistance;
        Vector3 movementDirection = (fleeTarget - transform.position).normalized;

        movementDirection = ApplyObstacleAvoidance(movementDirection);

        // Use consistent flee speed (clamped to prevent sudden bursts)
        float fleeSpeedThisFrame = Mathf.Min(fleeSpeed, moveSpeed * 1.5f);
        Vector3 newPosition = transform.position + movementDirection * fleeSpeedThisFrame * Time.deltaTime;
        rigidBody.MovePosition(newPosition);

        RotateTowardsTarget(fleeTarget);
    }


    public void TakeDamage(int damageAmount, Vector3 attackerPosition = default)
    {
        enemyHP = Mathf.Max(enemyHP - damageAmount, 0);
        GetComponent<NormalEnemyAgent>()?.HandleDamage();
        UpdateHealthBar();

        if (enemyHP > 0)
        {
            HandleDamageReaction();
            ApplyKnockbackFromAttacker(attackerPosition);
        }
        else
        {
            HandleDeath();
        }
    }

    private void ApplyKnockbackFromAttacker(Vector3 attackerPosition)
    {
        if (attackerPosition != Vector3.zero)
        {
            Vector3 knockbackDirection = (transform.position - attackerPosition).normalized;
            knockbackDirection.y = 0; // Keep knockback horizontal
            ApplyKnockback(knockbackDirection);
        }
    }

    public void ApplyKnockback(Vector3 direction)
    {
        knockbackState.ApplyKnockback(direction, KNOCKBACK_DURATION);

        // Apply immediate force
        if (rigidBody != null)
        {
            rigidBody.AddForce(direction * KNOCKBACK_FORCE, ForceMode.Impulse);
        }

        // Alternative movement-based knockback if rigidbody approach doesn't work well
        StartCoroutine(ExecuteKnockbackMovement(direction));
    }

    public void OnKnockbackReceived(Vector3 source)
    {
        Vector3 knockbackDirection = (transform.position - source).normalized;
        ApplyKnockback(knockbackDirection);
    }

    private void HandleDamageReaction()
    {
        PlayHitAnimation();
        PlayHitSound();
        CreateHitEffect();
        ReactToPlayerAttack();
    }

    private void ReactToPlayerAttack()
    {
        if (healthState.IsDead) return;

        playerTracking.SetInRange(true);
        waypointNavigation.SetPatrolling(false);

        if (RL_Player.Instance != null)
        {
            playerTracking.SetPlayerPosition(RL_Player.Instance.transform.position);
            RotateTowardsTarget(playerTracking.PlayerPosition);
        }
    }
    #endregion

    #region Movement
    private void HandleMovementBehavior()
    {
        if (healthState.IsDead || knockbackState.IsKnockedBack) return;

        if (fleeState.IsFleeing)
        {
            // Fleeing movement is handled in HandleFleeingBehavior
            return;
        }

        if (!playerTracking.IsInRange && HasValidWaypoints())
            ExecuteWaypointMovement();
        else if (!combatState.IsAttacking)
            SetAnimationState(idle: true);
    }

    private void ExecuteWaypointMovement()
    {
        if (waypointNavigation.IsPatrolling)
        {
            MoveToCurrentWaypoint();
        }
        else if (waypointNavigation.WaitTime <= 0)
        {
            waypointNavigation.SetPatrolling(true);
        }
        else
        {
            waypointNavigation.DecrementWaitTime();
        }
    }

    private void MoveToCurrentWaypoint()
    {
        var targetPosition = waypointNavigation.GetCurrentWaypointPosition();
        var movementDirection = CalculateMovementDirection(targetPosition);

        RotateTowardsTarget(targetPosition);
        ExecuteMovement(movementDirection);
        CheckWaypointReached(targetPosition);
    }

    private Vector3 CalculateMovementDirection(Vector3 targetPosition)
    {
        var direction = (targetPosition - transform.position).normalized;
        return ApplyObstacleAvoidance(direction);
    }

    private Vector3 ApplyObstacleAvoidance(Vector3 direction)
    {
        // Use multiple raycasts for better obstacle detection
        float rayDistance = 2f;
        float avoidanceRadius = 0.7f;

        // Check forward
        if (Physics.SphereCast(transform.position, avoidanceRadius, direction, out var hitForward, rayDistance, obstacleMask))
        {
            Vector3 avoidDirection = Vector3.Cross(hitForward.normal, Vector3.up).normalized;
            Vector3 newDirection = (direction + avoidDirection * 0.8f).normalized;
            
            // Apply rotation when avoiding obstacles
            if (newDirection != direction)
            {
                Quaternion targetRotation = Quaternion.LookRotation(newDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
            
            return newDirection;
        }

        // Check left and right
        Vector3 leftDirection = Quaternion.AngleAxis(-45f, Vector3.up) * direction;
        Vector3 rightDirection = Quaternion.AngleAxis(45f, Vector3.up) * direction;

        bool leftBlocked = Physics.SphereCast(transform.position, avoidanceRadius * 0.5f, leftDirection, out _, rayDistance * 0.7f, obstacleMask);
        bool rightBlocked = Physics.SphereCast(transform.position, avoidanceRadius * 0.5f, rightDirection, out _, rayDistance * 0.7f, obstacleMask);

        Vector3 finalDirection = direction;
        
        if (leftBlocked && !rightBlocked)
        {
            finalDirection = Vector3.Slerp(direction, rightDirection, 0.6f).normalized;
        }
        else if (rightBlocked && !leftBlocked)
        {
            finalDirection = Vector3.Slerp(direction, leftDirection, 0.6f).normalized;
        }

        // Apply rotation for avoidance
        if (finalDirection != direction)
        {
            Quaternion targetRotation = Quaternion.LookRotation(finalDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        return finalDirection;
    }
    private void ExecuteMovement(Vector3 direction)
    {
        // Use consistent speed from moveSpeed (enemy data)
        var newPosition = transform.position + direction * moveSpeed * Time.deltaTime;

        // Clamp movement to prevent sudden speed bursts
        float maxMovementThisFrame = moveSpeed * Time.deltaTime;
        Vector3 clampedMovement = Vector3.ClampMagnitude(newPosition - transform.position, maxMovementThisFrame);

        rigidBody.MovePosition(transform.position + clampedMovement);
    }

    private IEnumerator ExecuteKnockbackMovement(Vector3 direction)
    {
        float elapsed = 0f;

        while (elapsed < KNOCKBACK_DURATION)
        {
            if (!knockbackState.IsKnockedBack) break;

            float progress = elapsed / KNOCKBACK_DURATION;
            float force = Mathf.Lerp(KNOCKBACK_FORCE, 0f, progress);

            Vector3 knockbackMovement = direction * force * Time.deltaTime;
            Vector3 newPosition = transform.position + knockbackMovement;

            // Ensure we don't move through obstacles
            if (!Physics.SphereCast(transform.position, 0.5f, knockbackMovement.normalized,
                out RaycastHit hit, knockbackMovement.magnitude, obstacleMask))
            {
                rigidBody.MovePosition(newPosition);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void CheckWaypointReached(Vector3 targetPosition)
    {
        if (Vector3.Distance(transform.position, targetPosition) < waypointThreshold)
            waypointNavigation.MoveToNextWaypoint();
    }

    private void RotateTowardsTarget(Vector3 targetPosition)
    {
        var directionToTarget = (targetPosition - transform.position).normalized;
        var targetRotation = Quaternion.LookRotation(directionToTarget);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }
    #endregion

    #region Animation & Visuals
    private void UpdateAnimationStates()
    {
        if (combatState.IsAttacking && combatState.CanAttack) return;

        if (playerTracking.IsInRange)
            RotateTowardsTarget(playerTracking.PlayerPosition);
    }

    private void SetAnimationState(bool idle = false, bool walking = false, bool attacking = false, bool dead = false)
    {
        if (animator == null) return;

        animator.SetBool("isIdle", idle);
        animator.SetBool("isWalking", walking);
        animator.SetBool("isAttacking", attacking);
        animator.SetBool("isDead", dead);
    }

    private void PlayHitAnimation() => animator?.SetTrigger("getHit");

    private void UpdateHealthBar() => healthBar?.SetHealth(enemyHP);

    public void ShowHealthBar() => healthBar?.gameObject.SetActive(true);
    #endregion

    #region Death & Loot
    private void HandleDeath()
    {
        healthState.SetDead(true);
        SetAnimationState(dead: true);
        PlayDeathSound();
        GetComponent<Collider>().enabled = false;
        healthBar?.gameObject.SetActive(false);
        NotifyGameProgression();
        SpawnLoot();

        var agent = GetComponent<NormalEnemyAgent>();
        if (agent != null)
        {
            agent.HandleEnemyDeath(); // Notify the ML-Agent that it died
        }
        else
        {
            Destroy(gameObject, DESTROY_DELAY); // Destroy if not an ML-Agent
        }
    }

    private void SpawnLoot()
    {
        if (lootManager != null)
            lootManager.SpawnGearLoot(transform);
        else
            Debug.LogWarning("SpawnLoot: lootManager reference is null", this);
    }
    #endregion

    #region Audio & VFX
    private void PlayHitSound() => AudioManager.instance?.PlayEnemyGetHitSound(enemyType);
    private void PlayDeathSound() => AudioManager.instance?.PlayEnemyDieSound(enemyType);
    private void PlayAttackSound() => AudioManager.instance?.PlayEnemyAttackSound(enemyType);
    private void CreateHitEffect() => vfxManager?.EnemyGettingHit(particlePosition, enemyType);
    #endregion

    #region Utility Methods
    private bool IsPlayerHitbox(Collider collider) =>
        collider.CompareTag("Player") && collider.gameObject.layer == LayerMask.NameToLayer("Hitbox");

    private bool HasValidWaypoints() => waypoints != null && waypoints.Length > 0;
    private void NotifyGameProgression() => GameProgression.Instance?.EnemyKill();
    public float GetHealthPercentage() => (float)enemyHP / enemyData.enemyHealth;
    public bool IsHealthLow() => enemyHP <= enemyData.enemyHealth * fleeHealthThreshold;
    public bool IsFleeing() => fleeState.IsFleeing;
    public bool IsKnockedBack() => knockbackState.IsKnockedBack;
    public bool IsDead() => healthState.IsDead;
    public float GetDistanceToCurrentWaypoint() => waypointNavigation.GetDistanceToCurrentWaypoint(transform.position);
    public Vector3 GetWaypointDirection() => waypointNavigation.GetDirectionToCurrentWaypoint(transform.position);
    #endregion
    

    public class EnhancedPlayerDetection
    {
        private readonly RayPerceptionSensorComponent3D raySensor;
        private readonly LayerMask obstacleMask;
        private Transform playerTransform;
        private bool isPlayerVisible;
        private float lastPlayerDistance;
        private Vector3 lastPlayerPosition;
        private float lastPlayerCheckTime;
        private const float PLAYER_CHECK_INTERVAL = 0.5f;

        // Obstacle detection data
        private ObstacleInfo currentObstacleInfo;

        public struct ObstacleInfo
        {
            public bool hasObstacleAhead;
            public bool hasObstacleLeft;
            public bool hasObstacleRight;
            public float distanceAhead;
            public float distanceLeft;
            public float distanceRight;
        }

        public EnhancedPlayerDetection(RayPerceptionSensorComponent3D raySensor, LayerMask obstacleMask)
        {
            this.raySensor = raySensor;
            this.obstacleMask = obstacleMask;
            FindPlayerTransform();
            currentObstacleInfo = new ObstacleInfo();
        }

        public void Reset()
        {
            isPlayerVisible = false;
            lastPlayerDistance = float.MaxValue;
            lastPlayerPosition = Vector3.zero;
            currentObstacleInfo = new ObstacleInfo();
            FindPlayerTransform();
        }

        public void UpdatePlayerDetection(Vector3 agentPosition)
        {
            isPlayerVisible = false;
            UpdateObstacleDetection();

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
                var rayOutputs = RayPerceptionSensor.Perceive(raySensor.GetRayPerceptionInput(), false);

                foreach (var rayOutput in rayOutputs.RayOutputs)
                {
                    if (rayOutput.HasHit && rayOutput.HitGameObject != null)
                    {
                        if (rayOutput.HitGameObject.CompareTag("Player"))
                        {
                            isPlayerVisible = true;
                            lastPlayerDistance = rayOutput.HitFraction * raySensor.RayLength;
                            
                            if (playerTransform != null)
                            {
                                lastPlayerPosition = playerTransform.position;
                            }
                            break;
                        }
                    }
                }

                if (isPlayerVisible && playerTransform != null)
                {
                    float actualDistance = Vector3.Distance(agentPosition, playerTransform.position);
                    if (actualDistance > raySensor.RayLength * 1.1f)
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

        private void UpdateObstacleDetection()
        {
            if (raySensor == null) return;
            
            var rayOutputs = RayPerceptionSensor.Perceive(raySensor.GetRayPerceptionInput(), false);
            
            currentObstacleInfo = new ObstacleInfo
            {
                distanceAhead = float.MaxValue,
                distanceLeft = float.MaxValue,
                distanceRight = float.MaxValue
            };

            int rayCount = rayOutputs.RayOutputs.Length;
            if (rayCount == 0) return;

            int centerRayIndex = rayCount / 2;
            int leftRayIndex = centerRayIndex - rayCount / 4;
            int rightRayIndex = centerRayIndex + rayCount / 4;

            leftRayIndex = Mathf.Clamp(leftRayIndex, 0, rayCount - 1);
            rightRayIndex = Mathf.Clamp(rightRayIndex, 0, rayCount - 1);

            CheckObstacleInDirection(rayOutputs.RayOutputs, centerRayIndex, ref currentObstacleInfo.hasObstacleAhead, ref currentObstacleInfo.distanceAhead);
            CheckObstacleInDirection(rayOutputs.RayOutputs, leftRayIndex, ref currentObstacleInfo.hasObstacleLeft, ref currentObstacleInfo.distanceLeft);
            CheckObstacleInDirection(rayOutputs.RayOutputs, rightRayIndex, ref currentObstacleInfo.hasObstacleRight, ref currentObstacleInfo.distanceRight);
        }

        private void CheckObstacleInDirection(RayPerceptionOutput.RayOutput[] rayOutputs, int rayIndex, ref bool hasObstacle, ref float distance)
        {
            if (rayIndex >= 0 && rayIndex < rayOutputs.Length)
            {
                var rayOutput = rayOutputs[rayIndex];
                if (rayOutput.HasHit && rayOutput.HitGameObject != null)
                {
                    if (((1 << rayOutput.HitGameObject.layer) & obstacleMask) != 0)
                    {
                        hasObstacle = true;
                        distance = rayOutput.HitFraction * raySensor.RayLength;
                    }
                }
            }
        }

        private void FindPlayerTransform()
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
        
        public Transform GetPlayerTransform() => IsPlayerAvailable() ? playerTransform : null;

        public float GetDistanceToPlayer(Vector3 agentPosition)
        {
            if (!IsPlayerAvailable()) return float.MaxValue;

            try
            {
                Vector3 playerPos = GetPlayerPosition();
                
                if (isPlayerVisible && lastPlayerDistance > 0)
                {
                    return lastPlayerDistance;
                }

                return Vector3.Distance(agentPosition, playerPos);
            }
            catch (System.Exception)
            {
                return float.MaxValue;
            }
        }

        public ObstacleInfo GetObstacleInfo() => currentObstacleInfo;
    }


    public class UnifiedMovementSystem
    {
        private readonly NavMeshAgent navAgent;
        private readonly Transform agentTransform;
        private readonly float moveSpeed;
        private readonly float turnSpeed;
        private readonly float attackRange;
        private readonly EnhancedPlayerDetection playerDetection;

        private bool isRLControlled = false;
        private Vector3 lastMovementDirection = Vector3.zero;
        private float currentMovementSpeed = 0f;

        public UnifiedMovementSystem(NavMeshAgent navAgent, Transform agentTransform, float moveSpeed, 
            float turnSpeed, float attackRange, EnhancedPlayerDetection playerDetection)
        {
            this.navAgent = navAgent;
            this.agentTransform = agentTransform;
            this.moveSpeed = moveSpeed;
            this.turnSpeed = turnSpeed;
            this.attackRange = attackRange;
            this.playerDetection = playerDetection;
        }

        public void ResetMovement()
        {
            if (navAgent != null && navAgent.enabled)
            {
                navAgent.velocity = Vector3.zero;
                navAgent.isStopped = false;
                navAgent.ResetPath();
                navAgent.speed = moveSpeed; // Ensure consistent speed
            }
            isRLControlled = false;
            lastMovementDirection = Vector3.zero;
            currentMovementSpeed = 0f;
        }

       public void ProcessRLMovement(Vector3 movement, float rotation, Vector3 targetPosition = default)
        {
            isRLControlled = true;
            
            // Stop NavMesh pathfinding when RL takes control
            if (navAgent != null && navAgent.enabled)
            {
                navAgent.ResetPath();
                navAgent.isStopped = true;
                navAgent.updateRotation = false;
            }

            // Apply movement with consistent speed
            if (movement.magnitude > 0.1f)
            {
                Vector3 worldMovement = agentTransform.TransformDirection(movement).normalized;
                Vector3 targetPos = agentTransform.position + worldMovement * moveSpeed * Time.fixedDeltaTime;
                
                // Use Rigidbody for smoother movement
                var rb = agentTransform.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 velocity = worldMovement * moveSpeed;
                    rb.linearVelocity = new Vector3(velocity.x, rb.linearVelocity.y, velocity.z);
                }
                
                currentMovementSpeed = moveSpeed;
                lastMovementDirection = worldMovement;
            }
            else
            {
                var rb = agentTransform.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
                }
                currentMovementSpeed = 0f;
            }

            // Prioritized rotation handling
            if (targetPosition != default)
            {
                Vector3 direction = (targetPosition - agentTransform.position);
                direction.y = 0;
                
                if (direction.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    agentTransform.rotation = Quaternion.Slerp(
                        agentTransform.rotation,
                        targetRotation,
                        turnSpeed * Time.fixedDeltaTime
                    );
                }
            }
            else if (Mathf.Abs(rotation) > 0.1f)
            {
                agentTransform.Rotate(0, rotation * turnSpeed * Time.fixedDeltaTime, 0);
            }
        }

        public void ProcessNavMeshMovement(Vector3 targetPosition)
        {
            isRLControlled = false;
            
            if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
            {
                navAgent.isStopped = false;
                navAgent.speed = moveSpeed; // Ensure consistent speed
                navAgent.updateRotation = true; // Let NavMesh handle rotation
                navAgent.SetDestination(targetPosition);
                
                currentMovementSpeed = navAgent.velocity.magnitude;
            }
        }

        public void ProcessFleeMovement(Vector3 fleeTarget)
        {
            if (UnityEngine.AI.NavMesh.SamplePosition(fleeTarget, out UnityEngine.AI.NavMeshHit hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                if (navAgent != null && navAgent.enabled)
                {
                    navAgent.isStopped = false;
                    navAgent.SetDestination(hit.position);
                    navAgent.speed = moveSpeed * 1.2f; // Slightly faster flee speed (controlled)
                    navAgent.updateRotation = true;
                }
            }
            currentMovementSpeed = navAgent != null ? navAgent.velocity.magnitude : 0f;
        }

        public void HandleKnockback()
        {
            if (navAgent != null && navAgent.enabled)
            {
                navAgent.isStopped = true;
                navAgent.ResetPath();
            }
            currentMovementSpeed = 0f;
        }

        public void StopMovement()
        {
            isRLControlled = false;
            if (navAgent != null && navAgent.enabled)
            {
                navAgent.isStopped = true;
                navAgent.ResetPath();
                navAgent.velocity = Vector3.zero;
            }
            currentMovementSpeed = 0f;
        }

        public void ApplyMovementInfluence(Vector3 movement)
        {
            if (navAgent != null && navAgent.enabled && movement.magnitude > 0.1f)
            {
                Vector3 currentDestination = navAgent.destination;
                Vector3 influence = agentTransform.TransformDirection(movement) * 2f;
                Vector3 newDestination = currentDestination + influence;
                
                if (UnityEngine.AI.NavMesh.SamplePosition(newDestination, out UnityEngine.AI.NavMeshHit hit, 3f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    navAgent.SetDestination(hit.position);
                }
            }
        }

        public void ApplyAntiStuckBehavior()
        {
            Vector3 randomDirection = Random.insideUnitSphere;
            randomDirection.y = 0;
            randomDirection.Normalize();
            
            Vector3 unstuckTarget = agentTransform.position + randomDirection * 3f;
            
            if (UnityEngine.AI.NavMesh.SamplePosition(unstuckTarget, out UnityEngine.AI.NavMeshHit hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                if (navAgent != null && navAgent.enabled)
                {
                    navAgent.isStopped = false;
                    navAgent.SetDestination(hit.position);
                }
            }
        }

        public void WarpToPosition(Vector3 position)
        {
            if (navAgent != null)
            {
                navAgent.enabled = false;
                agentTransform.position = position;
                navAgent.enabled = true;
                
                if (navAgent.isOnNavMesh)
                {
                    navAgent.Warp(position);
                }
            }
            else
            {
                agentTransform.position = position;
            }
        }

        public bool IsPlayerInAttackRange(Vector3 playerPosition) =>
            Vector3.SqrMagnitude(agentTransform.position - playerPosition) <= attackRange * attackRange;

        public bool IsMoving() => currentMovementSpeed > 0.1f;
        public float GetCurrentSpeed() => currentMovementSpeed;
    }
}
