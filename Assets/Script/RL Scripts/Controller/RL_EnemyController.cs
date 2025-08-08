using System.Collections;
using UnityEngine;

public class RL_EnemyController : MonoBehaviour
{
    #region Serialized Fields
    [Header("Combat Configuration")]
    [SerializeField] private float fleeHealthThreshold = 0.2f;
    [SerializeField] private float fleeDistance = 8f;
    [SerializeField] private float fleeDetectionRadius = 10f;
    [SerializeField] private float fleeDuration = 3f;
    [SerializeField] private HealthBar healthBar;
    [SerializeField] private BoxCollider attackCollider;

    [Header("Movement Configuration")]
    [SerializeField] public Transform[] waypoints; 
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
    public NormalEnemyActions.FleeState fleeState;
    public int enemyHP;
    public float attackRange = 3f; 
    #endregion

    #region Private Variables
    private PlayerTrackingState playerTracking;
    private WaypointNavigationState waypointNavigation;
    private EnemyStatDisplay statDisplay;
    private Rigidbody rigidBody;
    private KnockbackState knockbackState;
    private NormalEnemyAgent agent;

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
        if (GetComponent<NormalEnemyAgent>()?.enabled == true) return;

        knockbackState.UpdateKnockback();
        fleeState.UpdateTimer();
        UpdatePlayerTracking();

        // Handle fleeing with time limit
        if (fleeState.IsFleeing)
        {
            HandleFleeingBehavior();
        }
        else if (ShouldFlee())
        {
            InitiateFlee();
        }
        else if (!knockbackState.IsKnockedBack)
        {
            HandleCombatBehavior();
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

    private void OnEnable()  => RL_Player.OnPlayerDestroyed += HandlePlayerDestroyed;
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
        agent = GetComponent<NormalEnemyAgent>();
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
        if (!playerTracking.IsPlayerAlive || playerTracking.PlayerTransform == null)
        {
            StopFleeing();
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, playerTracking.PlayerPosition);
        
        // Stop fleeing if: reached safe distance, fled too long, or health recovered
        if (distanceToPlayer >= fleeDistance || 
            fleeState.FleeTimer >= fleeDuration || 
            !IsHealthLow())
        {
            StopFleeing();
        }
    }

    private void StopFleeing()
    {
        fleeState.StopFleeing();
        waypointNavigation.SetPatrolling(true);
        combatState.SetCanAttack(true);
    }

    private void InitiateFlee()
    {
        Vector3 fleeDirection = CalculateFleeDirection();
        fleeState.StartFleeing(fleeDirection);
        waypointNavigation.SetPatrolling(false);
        combatState.SetCanAttack(false); // Disable attacking while fleeing
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
    private IEnumerator ExecuteKnockbackMovement(Vector3 direction)
    {
        float elapsed = 0f;
        
        while (elapsed < KNOCKBACK_DURATION)
        {
            if (!knockbackState.IsKnockedBack) break;
            
            // FIXED: Let physics handle knockback properly
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void RotateTowardsTarget(Vector3 targetPosition)
    {
        var directionToTarget = (targetPosition - transform.position).normalized;
        var targetRotation = Quaternion.LookRotation(directionToTarget);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, agent.rotationSpeed * Time.deltaTime);
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
            return;
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
