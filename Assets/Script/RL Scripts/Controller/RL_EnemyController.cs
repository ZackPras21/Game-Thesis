using System.Collections;
using UnityEngine;

public class RL_EnemyController : MonoBehaviour
{
    #region Serialized Fields
    [Header("Combat Configuration")]
    public int enemyHP;
    public float attackRange = 3f; 
    [SerializeField] private float detectThreshold = 0.5f; 
    [SerializeField] private float fleeHealthThreshold = 0.2f; 
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
    public NormalEnemyStates enemyStates { get; private set; }
    public bool IsMLControlled { get; set; } = false;
    #endregion

    #region Private Variables
    private EnemyStatDisplay statDisplay;
    private Rigidbody rigidBody;

    private const float ATTACK_DURATION = 1f;
    private const float KNOCKBACK_FORCE = 10f; 
    private const float KNOCKBACK_DURATION = 0.5f; 
    private const float DESTROY_DELAY = 8f;
    #endregion

    #region Unity Lifecycle
    private void Awake() => ForceInitialize();

    private void Update()
    {
        if (!IsInitialized || IsMLControlled) return;

        UpdateEnemyStates();
        UpdatePlayerTracking();
        HandleCombatBehavior();
        HandleMovementBehavior();
        UpdateAnimationStates();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayerHitbox(other))
        {
            enemyStates.PlayerTracking.SetInRange(true);
            statDisplay?.ShowEnemyStats();
            
            // Set target for ML Agent
            if (IsMLControlled)
            {
                SetTarget(other.transform);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayerHitbox(other))
        {
            enemyStates.PlayerTracking.SetInRange(false);
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
        SetupEnemyData();
        InitializeEnemyStates();
        SetAnimationState(idle: true);
        IsInitialized = true;
    }

    private void InitializeComponents()
    {
        attackCollider = GetComponent<BoxCollider>();
        statDisplay = GetComponent<EnemyStatDisplay>();
        rigidBody = GetComponent<Rigidbody>();
    }

    private void InitializeEnemyStates()
    {
        Transform playerTransform = RL_Player.Instance?.transform;
        enemyStates = new NormalEnemyStates(
            transform,
            enemyHP,
            enemyData.enemyHealth,
            playerTransform,
            detectThreshold,
            obstacleMask,
            waypoints,
            startWaitTime
        );
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
            EnemyType.Medium1 => Medium1EnemyData.medium1EnemyData,
            EnemyType.Medium2 => Medium2EnemyData.medium2EnemyData,
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

    #region State Updates
    private void UpdateEnemyStates()
    {
        if (enemyStates == null) return;

        Transform playerTransform = RL_Player.Instance?.transform;
        
        enemyStates.UpdateStates(
            transform,
            enemyHP,
            enemyData.enemyHealth,
            playerTransform,  
            detectThreshold,
            obstacleMask
        );
        
        if (playerTransform != null && enemyStates.PlayerTracking.PlayerTransform != playerTransform)
        {
            SetTarget(playerTransform);
        }
    }
    #endregion

    #region Player Tracking
    private void UpdatePlayerTracking()
    {
        if (enemyStates.PlayerTracking.PlayerTransform == null)
        {
            enemyStates.PlayerTracking.SetInRange(false);
            enemyStates.WaypointNavigation.SetPatrolling(true);
            SetAnimationState(walking: true);
        }
    }

    public void SetTarget(Transform target)
    {
        if (target == null || !enemyStates.PlayerTracking.IsPlayerAlive)
        {
            enemyStates.PlayerTracking.ClearTarget();
            return;
        }

        enemyStates.PlayerTracking.SetTarget(target);
        enemyStates.WaypointNavigation.SetPatrolling(false);
    }

    private void HandlePlayerDestroyed() => enemyStates.PlayerTracking.HandlePlayerDestroyed();
    #endregion

    #region Combat
    private void HandleCombatBehavior()
    {
        // Don't handle combat if ML Agent is controlling this enemy
        if (IsMLControlled) return;
            
        if (enemyStates.PlayerTracking.IsInRange &&
            enemyStates.PlayerTracking.PlayerTransform != null &&
            enemyStates.PlayerTracking.IsPlayerAlive)
        {
            RotateTowardsTarget(enemyStates.PlayerTracking.PlayerPosition);
            
            // Only allow attack if not already attacking and can attack based on cooldown
            if (enemyStates.CombatState.CanAttack && !enemyStates.CombatState.IsAttacking)
            {
                StartCoroutine(ExecuteAttackSequence());
            }
        }
        else if (!enemyStates.CombatState.IsAttacking)
        {
            SetAnimationState(idle: true);
        }
    }

    private void HandlePlayerEnterCombat()
    {
        enemyStates.CombatState.SetAttacking(true);
    }

    private void HandlePlayerExitCombat() => enemyStates.CombatState.SetAttacking(false);

    private IEnumerator ExecuteAttackSequence()
    {
        // Set attacking state immediately to prevent re-entry
        enemyStates.CombatState.SetAttacking(true);
        SetAnimationState(attacking: true);
        
        // Store player position for hit detection at the start of the attack
        Vector3 attackTargetPosition = enemyStates.PlayerTracking.PlayerPosition;
        
        // Wait for a portion of the animation before applying damage
        yield return new WaitForSeconds(ATTACK_DURATION * 0.4f);
        
        // Execute damage and notify ML-Agent if present
        bool hitPlayer = ExecuteAttackDamage(attackTargetPosition);
        
        // Notify ML Agent about attack result
        if (IsMLControlled)
        {
            var mlAgent = GetComponent<NormalEnemyAgent>();
            if (mlAgent != null)
            {
                if (hitPlayer)
                    mlAgent.OnAttackHit();
                else
                    mlAgent.OnAttackMissed();
            }
        }
        // Wait for the remainder of the animation
        yield return new WaitForSeconds(ATTACK_DURATION * 0.6f);
        
        // Reset attacking state and update cooldown
        enemyStates.CombatState.SetAttacking(false);
        enemyStates.CombatState.StartAttackCooldown(); // Ensure cooldown starts
        SetAnimationState(attacking: false, idle: true);
    }
    
    public void MLAgentAttack()
    {
        var combat = enemyStates.CombatState;
        if (!combat.CanAttack || combat.IsAttacking)
        {
            Debug.Log($"{name} ML attack blocked - CanAttack: {combat.CanAttack}, IsAttacking: {combat.IsAttacking}");
            return;
        }

        StartCoroutine(ExecuteAttackSequence());
    }

    public void AttackEnd()
    {
        SetAnimationState(idle: true);
    }

    private bool ExecuteAttackDamage(Vector3 attackTargetPosition)
    {
        var hitTargets = Physics.OverlapSphere(
            transform.position + transform.forward * (attackRange * 0.5f),
            attackRange,
            LayerMask.GetMask("Player"));

        foreach (var target in hitTargets)
        {
            var player = target.GetComponent<RL_Player>();
            if (player != null && player.CurrentHealth > 0f)
            {
                // Check if player is still within reasonable range of where we aimed
                float distanceFromTarget = Vector3.Distance(player.transform.position, attackTargetPosition);
                if (distanceFromTarget <= attackRange * 1.5f)
                {
                    // Apply damage with knockback
                    ApplyDamageWithKnockback(player);
                    PlayAttackSound();
                    
                    // Check if player died from this attack
                    if (player.CurrentHealth <= 0f && IsMLControlled)
                    {
                        GetComponent<NormalEnemyAgent>()?.HandlePlayerKilled();
                    }
                    
                    return true;
                }
            }
        }
        return false;
    }

    private void ApplyDamageWithKnockback(RL_Player player)
    {
        // Apply damage
        player.DamagePlayer(enemyData.enemyAttack);
        
        // Apply knockback
        Vector3 knockbackDirection = (player.transform.position - transform.position).normalized;
        knockbackDirection.y = 0; // Keep knockback horizontal
        
        Rigidbody playerRb = player.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            playerRb.AddForce(knockbackDirection * KNOCKBACK_FORCE, ForceMode.Impulse);
            StartCoroutine(ApplyKnockbackDuration(playerRb));
        }
    }

    private IEnumerator ApplyKnockbackDuration(Rigidbody playerRb)
    {
        if (playerRb == null) yield break;
        
        // Store original drag
        float originalDrag = playerRb.linearDamping;
        
        // Reduce drag during knockback for better effect
        playerRb.linearDamping = 0.5f;
        
        yield return new WaitForSeconds(KNOCKBACK_DURATION);
        
        // Check if rigidbody still exists before restoring drag
        if (playerRb != null)
        {
            playerRb.linearDamping = originalDrag;
        }
}

    public void TakeDamage(int damageAmount)
    {
        enemyHP = Mathf.Max(enemyHP - damageAmount, 0);
        GetComponent<NormalEnemyAgent>()?.HandleDamage();
        UpdateHealthBar();

        if (enemyHP > 0)
            HandleDamageReaction();
        else
            HandleDeath();
    }

    private void HandleDamageReaction()
    {
        PlayHitAnimation();
        PlayHitSound();
        CreateHitEffect();
        ReactToPlayerAttack();
        
        // Check if should flee
        if (GetHealthPercentage() <= fleeHealthThreshold)
        {
            StartCoroutine(FleeFromPlayer());
        }
    }

    private void ReactToPlayerAttack()
    {
        if (enemyStates.HealthState.IsDead) return;

        enemyStates.PlayerTracking.SetInRange(true);
        enemyStates.WaypointNavigation.SetPatrolling(false);

        if (RL_Player.Instance != null)
        {
            enemyStates.PlayerTracking.SetPlayerPosition(RL_Player.Instance.transform.position);
            RotateTowardsTarget(enemyStates.PlayerTracking.PlayerPosition);
        }
    }
    #endregion

    #region Movement
    private void HandleMovementBehavior()
    {
        if (enemyStates.HealthState.IsDead) return;

        if (!enemyStates.PlayerTracking.IsInRange && HasValidWaypoints())
            ExecuteWaypointMovement();
        else if (!enemyStates.CombatState.IsAttacking)
            SetAnimationState(idle: true);
    }

    private void ExecuteWaypointMovement()
    {
        if (enemyStates.WaypointNavigation.IsPatrolling)
        {
            MoveToCurrentWaypoint();
        }
        else if (enemyStates.WaypointNavigation.WaitTime <= 0)
        {
            enemyStates.WaypointNavigation.SetPatrolling(true);
        }
        else
        {
            enemyStates.WaypointNavigation.DecrementWaitTime();
        }
    }

    private void MoveToCurrentWaypoint()
    {
        var targetPosition = enemyStates.WaypointNavigation.GetCurrentWaypointPosition();
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
        if (Physics.SphereCast(transform.position, 0.7f, direction, out var hit, 2f, obstacleMask))
        {
            var avoidanceDirection = Vector3.Cross(hit.normal, Vector3.up).normalized;
            return (direction + avoidanceDirection * 0.7f).normalized;
        }
        return direction;
    }

    private void ExecuteMovement(Vector3 direction)
    {
        var newPosition = transform.position + direction * moveSpeed * Time.deltaTime;
        rigidBody.MovePosition(newPosition);
    }

    private void CheckWaypointReached(Vector3 targetPosition)
    {
        if (Vector3.Distance(transform.position, targetPosition) < waypointThreshold)
        {
            enemyStates.WaypointNavigation.MoveToNextWaypoint();
            
            // Optional: Add a small delay at waypoint
            enemyStates.WaypointNavigation.SetPatrolling(false);
            //enemyStates.WaypointNavigation(startWaitTime);
        }
    }

    private void RotateTowardsTarget(Vector3 targetPosition)
    {
        var directionToTarget = (targetPosition - transform.position).normalized;
        var targetRotation = Quaternion.LookRotation(directionToTarget);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private IEnumerator FleeFromPlayer()
    {
        if (enemyStates.PlayerTracking.PlayerTransform == null) yield break;
        
        Vector3 fleeDirection = (transform.position - enemyStates.PlayerTracking.PlayerPosition).normalized;
        float fleeDistance = 10f;
        Vector3 fleeTarget = transform.position + fleeDirection * fleeDistance;
        
        // Move away from player
        float fleeTime = 2f;
        float elapsedTime = 0f;
        Vector3 startPosition = transform.position;
        
        while (elapsedTime < fleeTime && !enemyStates.HealthState.IsDead)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / fleeTime;
            
            Vector3 newPosition = Vector3.Lerp(startPosition, fleeTarget, t);
            rigidBody.MovePosition(newPosition);
            
            // Face away from player
            RotateTowardsTarget(transform.position + fleeDirection);
            
            yield return null;
        }
        
        // After fleeing, resume normal behavior
        enemyStates.PlayerTracking.SetInRange(false);
    }
    #endregion

    #region Animation & Visuals
    private void UpdateAnimationStates()
    {
        if (enemyStates.CombatState.IsAttacking) return;
        bool isMoving = rigidBody.linearVelocity.magnitude > 0.1f; 
        SetAnimationState(idle: !isMoving, walking: isMoving);
        if (enemyStates.PlayerTracking.IsInRange)
        {
            RotateTowardsTarget(enemyStates.PlayerTracking.PlayerPosition);
        }
    }

    private void SetAnimationState(bool idle = false, bool walking = false, bool attacking = false, bool dead = false)
    {
        if (animator == null) return;
        animator.SetBool("isIdle", idle);
        animator.SetBool("isWalking", walking);
        animator.SetBool("isAttacking", attacking);
        animator.SetBool("isDead", dead);

        enemyStates.IsIdle = idle;
        enemyStates.HealthState.SetDead(dead);
        enemyStates.WaypointNavigation.SetPatrolling(walking); 
    }

    private void PlayHitAnimation() => animator?.SetTrigger("getHit");

    private void UpdateHealthBar() => healthBar?.SetHealth(enemyHP);

    public void ShowHealthBar() => healthBar?.gameObject.SetActive(true);
    #endregion

    #region Death & Loot
    private void HandleDeath()
    {
        enemyStates.HealthState.SetDead(true);
        SetAnimationState(dead: true);
        PlayDeathSound();
        GetComponent<Collider>().enabled = false;
        healthBar?.gameObject.SetActive(false);
        NotifyGameProgression();
        SpawnLoot();

        var agent = GetComponent<NormalEnemyAgent>();
        if (agent != null)
        {
            agent.HandleEnemyDeath(); 
        }
        else
        {
            Destroy(gameObject, DESTROY_DELAY); 
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
    public bool IsHealthLow() => enemyHP <= enemyData.enemyHealth * 0.2f;
    public bool IsDead() => enemyStates.HealthState.IsDead;
    public float GetDistanceToCurrentWaypoint() => enemyStates.WaypointNavigation.GetDistanceToCurrentWaypoint(transform.position);
    public Vector3 GetWaypointDirection() => enemyStates.WaypointNavigation.GetDirectionToCurrentWaypoint(transform.position);
    #endregion
}

#region Debug Display
public class DebugDisplay
{
    private float cumulativeReward;
    private int episodeSteps;

    public void Reset()
    {
        cumulativeReward = 0f;
        episodeSteps = 0;
    }

    public void IncrementSteps() => episodeSteps++;
    public void UpdateCumulativeReward(float reward) => cumulativeReward = reward;

    public void DisplayDebugInfo(string agentName, string currentState, string currentAction, Vector2 offset, Color textColor, int fontSize, int patrolLoops)
    {
        var labelStyle = new GUIStyle
        {
            fontSize = fontSize,
            normal = { textColor = textColor }
        };

        string debugText = $"{agentName}:\nState: {currentState}\nAction: {currentAction}\nSteps: {episodeSteps}\nCumulative Reward: {cumulativeReward:F3}\nPatrol Loops: {patrolLoops}";
        GUI.Label(new Rect(offset.x, offset.y, 300, 150), debugText, labelStyle);
    }
}
#endregion