using System.Collections;
using UnityEngine;

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
        SetupEnemyData();
        SetAnimationState(idle: true);
        IsInitialized = true;
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
            
            if (distanceToPlayer >= fleeDistance || fleeState.FleeTimer > 5f) // 5 second timeout
            {
                fleeState.StopFleeing();
                waypointNavigation.SetPatrolling(true); // Resume patrol
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
        
        Vector3 newPosition = transform.position + movementDirection * fleeSpeed * Time.deltaTime;
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
        
        if (Physics.Raycast(transform.position, directionToTarget, out RaycastHit hit,
        Vector3.Distance(transform.position, targetPosition), obstacleMask))
        {
            return;
        }
        
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
    public bool IsHealthLow() => enemyHP <= enemyData.enemyHealth * fleeHealthThreshold;
    public bool IsFleeing() => fleeState.IsFleeing;
    public bool IsKnockedBack() => knockbackState.IsKnockedBack;
    public bool IsDead() => healthState.IsDead;
    public float GetDistanceToCurrentWaypoint() => waypointNavigation.GetDistanceToCurrentWaypoint(transform.position);
    public Vector3 GetWaypointDirection() => waypointNavigation.GetDirectionToCurrentWaypoint(transform.position);
    #endregion
}
