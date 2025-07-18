/* using System.Collections;
using UnityEngine;

public class RL_EnemyControllerOld : MonoBehaviour
{
    #region Serialized Fields
    [Header("Combat Configuration")]
    public int enemyHP;
    public float attackRange = 3f; 
    [SerializeField] private float detectThreshold = 0.5f; // Unused, consider removal if not implemented
    [SerializeField] private float fleeHealthThreshold = 0.2f; // Unused, consider removal if not implemented
    [SerializeField] private HealthBar healthBar;
    [SerializeField] private BoxCollider attackCollider;

    [Header("Movement Configuration")]
    [SerializeField] public float rotationSpeed = 5f;
    [SerializeField] public float moveSpeed = 3f;
    [SerializeField] private float waypointThreshold = 0.5f;
    [SerializeField] public Transform[] waypoints; // Made public for spawner to set
    [SerializeField] private LayerMask obstacleMask;

    [Header("AI Behavior")]
    [SerializeField] private float startWaitTime = 4f;
    [SerializeField] private float timeToRotate = 2f; // Unused, consider removal if not implemented
    [SerializeField] private float separationRadius = 2f; // Unused, consider removal if not implemented
    [SerializeField] private LayerMask enemyMask; // Unused, consider removal if not implemented

    [Header("Component References")]
    [SerializeField] private Animator animator;
    [SerializeField] private LootManager lootManager;
    [SerializeField] private VFXManager vfxManager;
    [SerializeField] private Transform particlePosition;
    [SerializeField] private EnemyType enemyType;
    [SerializeField] private NonBossEnemyState enemyState; // Unused, consider removal if not implemented
    #endregion

    #region Public Properties & Variables
    public EnemyData enemyData;
    public bool IsInitialized { get; private set; }
    #endregion

    #region Private Variables
    private PlayerTrackingState playerTracking;
    private WaypointNavigationState waypointNavigation;
    public CombatState combatState; // Public for NormalEnemyAgent access
    public HealthState healthState; // Public for NormalEnemyAgent access
    private EnemyStatDisplay statDisplay;
    private Rigidbody rigidBody;

    private const float ATTACK_DURATION = 1f;
    private const float ATTACK_COOLDOWN = 2f;
    private const float KNOCKBACK_FORCE = 10f; // Unused, consider removal if not implemented
    private const float KNOCKBACK_DURATION = 0.5f; // Unused, consider removal if not implemented
    private const float DESTROY_DELAY = 8f;
    #endregion

    #region Unity Lifecycle
    private void Awake() => ForceInitialize();

    private void Update()
    {
        if (!IsInitialized) return;

        UpdatePlayerTracking();
        HandleCombatBehavior();
        HandleMovementBehavior();
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
        // attackCollider is serialized, so it should be assigned in inspector.
        // If not, uncomment: attackCollider = GetComponent<BoxCollider>();
        statDisplay = GetComponent<EnemyStatDisplay>();
        rigidBody = GetComponent<Rigidbody>();
    }

    private void InitializeStates()
    {
        playerTracking = new PlayerTrackingState();
        waypointNavigation = new WaypointNavigationState(waypoints, startWaitTime);
        combatState = new CombatState();
        healthState = new HealthState();
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
        if (playerTracking.IsInRange && playerTracking.PlayerTransform != null && playerTracking.IsPlayerAlive)
        {
            RotateTowardsTarget(playerTracking.PlayerPosition);

            if (combatState.CanAttack)
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
        if (healthState.IsDead) return;

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
    public bool IsHealthLow() => enemyHP <= enemyData.enemyHealth * 0.2f;
    public bool IsDead() => healthState.IsDead;
    public float GetDistanceToCurrentWaypoint() => waypointNavigation.GetDistanceToCurrentWaypoint(transform.position);
    public Vector3 GetWaypointDirection() => waypointNavigation.GetDirectionToCurrentWaypoint(transform.position);
    public bool IsCaughtPlayer => false; // Unused, consider removal if not implemented
    #endregion
}

#region State Classes
// These state classes remain largely the same, but are included for completeness.
// Ensure they are defined outside the main class or in their own files.

public class PlayerTrackingState
{
    public Vector3 PlayerPosition { get; private set; }
    public Transform PlayerTransform { get; private set; }
    public bool IsInRange { get; private set; }
    public bool IsPlayerAlive { get; private set; } = true;

    public void SetTarget(Transform target)
    {
        PlayerTransform = target;
        IsInRange = true;
        PlayerPosition = target.position;
    }

    public void SetInRange(bool inRange) => IsInRange = inRange;
    public void SetPlayerPosition(Vector3 position) => PlayerPosition = position;
    public void ClearTarget()
    {
        IsInRange = false;
        PlayerTransform = null;
    }

    public void HandlePlayerDestroyed()
    {
        IsInRange = false;
        IsPlayerAlive = false;
        PlayerTransform = null;
    }
}

public class WaypointNavigationState
{
    private readonly Transform[] waypoints;
    private readonly float startWaitTime;
    private int currentWaypointIndex;

    public bool IsPatrolling { get; private set; }
    public float WaitTime { get; private set; }

    public WaypointNavigationState(Transform[] waypoints, float waitTime)
    {
        this.waypoints = waypoints;
        startWaitTime = waitTime;
        currentWaypointIndex = Random.Range(0, waypoints?.Length ?? 0);
    }

    public void SetPatrolling(bool patrolling) => IsPatrolling = patrolling;
    public void DecrementWaitTime() => WaitTime -= Time.deltaTime;

    public void MoveToNextWaypoint()
    {
        if (waypoints?.Length > 0)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            IsPatrolling = true;
            WaitTime = startWaitTime;
        }
    }

    public Vector3 GetCurrentWaypointPosition() =>
        waypoints?.Length > 0 && currentWaypointIndex < waypoints.Length
            ? waypoints[currentWaypointIndex].position
            : Vector3.zero;

    public float GetDistanceToCurrentWaypoint(Vector3 currentPosition) =>
        waypoints?.Length > 0 ? Vector3.Distance(currentPosition, GetCurrentWaypointPosition()) : -1f;

    public Vector3 GetDirectionToCurrentWaypoint(Vector3 currentPosition) =>
        waypoints?.Length > 0 ? (GetCurrentWaypointPosition() - currentPosition).normalized : Vector3.zero;
}

public class CombatState
{
    public bool IsAttacking { get; private set; }
    public bool CanAttack { get; private set; } = true;

    public void SetAttacking(bool attacking) => IsAttacking = attacking;
    public void SetCanAttack(bool canAttack) => CanAttack = canAttack;
}

public class HealthState
{
    public bool IsDead { get; private set; }
    public void SetDead(bool dead) => IsDead = dead;
}
#endregion */
