using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RL_EnemyController : MonoBehaviour
{
    [Header("Combat Configuration")]
    [SerializeField] public int enemyHP;
    [SerializeField] public float attackDamage = 10f;
    [SerializeField] public float attackRange = 2f;
    [SerializeField] public float detectThreshold = 0.5f;
    [SerializeField] public float fleeHealthThreshold = 0.2f;
    [SerializeField] private HealthBar healthBar;
    [SerializeField] private BoxCollider attackCollider;
    
    [Header("Movement Configuration")]
    [SerializeField] public float rotationSpeed = 5f;
    [SerializeField] public float moveSpeed = 3f;
    [SerializeField] public float waypointThreshold = 0.5f;
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private LayerMask obstacleMask;

    [Header("AI Behavior")]
    [SerializeField] private float startWaitTime = 4f;
    [SerializeField] private float timeToRotate = 2f;
    [SerializeField] private float separationRadius = 2f;
    [SerializeField] private LayerMask enemyMask;

    [Header("Component References")]
    [SerializeField] private Animator animator;
    [SerializeField] private LootManager lootManager;
    [SerializeField] private VFXManager vfxManager;
    [SerializeField] private Transform particlePosition;
    [SerializeField] private EnemyType enemyType;
    [SerializeField] private NonBossEnemyState enemyState;

    // State tracking
    private PlayerTrackingState playerTracking;
    private WaypointNavigationState waypointNavigation;
    private CombatState combatState;
    private HealthState healthState;

    // Component references
    private EnemyData enemyData;
    private EnemyStatDisplay statDisplay;
    private Rigidbody rigidBody;

    private const float ATTACK_DURATION = 1f;
    private const float ATTACK_COOLDOWN = 2f;
    private const float KNOCKBACK_FORCE = 10f;
    private const float KNOCKBACK_DURATION = 0.5f;
    private const float DESTROY_DELAY = 8f;

    #region Unity Lifecycle

    private void Start()
    {
        InitializeComponents();
        InitializeStates();
        SetupEnemyData();
        ForceInitialAnimationState();
    }

    private void Update()
    {
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
            ShowEnemyStats();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayerHitbox(other))
        {
            HandlePlayerExitCombat();
            HideEnemyStats();
        }
    }

    private void OnEnable()
    {
        RL_Player.OnPlayerDestroyed += HandlePlayerDestroyed;
    }

    private void OnDisable()
    {
        RL_Player.OnPlayerDestroyed -= HandlePlayerDestroyed;
    }

    #endregion

    #region Initialization

    private void InitializeComponents()
    {
        attackCollider = GetComponent<BoxCollider>();
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
        enemyData = GetEnemyDataByType();
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
            _ => CreepEnemyData.Instance
        };
    }

    private void InitializeHealthBar()
    {
        if (healthBar != null)
        {
            healthBar.SetMaxHealth(enemyHP);
        }
    }

    private void ForceInitialAnimationState()
    {
        if (animator != null)
        {
            SetAnimationState(idle: true, walking: false, attacking: false, dead: false);
        }
    }

    #endregion

    #region Player Tracking

    private void UpdatePlayerTracking()
    {
        if (playerTracking.PlayerTransform == null)
        {
            HandleLostPlayer();
        }
    }

    private void HandleLostPlayer()
    {
        playerTracking.SetInRange(false);
        waypointNavigation.SetPatrolling(true);
        SetAnimationState(idle: false, walking: true);
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

    private void HandlePlayerDestroyed()
    {
        playerTracking.HandlePlayerDestroyed();
    }

    #endregion

    #region Combat System

    private void HandleCombatBehavior()
    {
        if (playerTracking.IsInRange && playerTracking.PlayerTransform != null && playerTracking.IsPlayerAlive)
        {
            RotateTowardsTarget(playerTracking.PlayerPosition);
            
            if (combatState.CanAttack)
            {
                StartCoroutine(ExecuteAttackSequence());
            }
        }
    }

    private void HandlePlayerEnterCombat()
    {
        combatState.SetAttacking(true);
        combatState.SetCanAttack(true);
    }

    private void HandlePlayerExitCombat()
    {
        combatState.SetAttacking(false);
    }

    private IEnumerator ExecuteAttackSequence()
    {
        combatState.SetCanAttack(false);
        
        SetAnimationState(attacking: true, idle: false, walking: false);
        yield return new WaitForSeconds(ATTACK_DURATION);
        
        SetAnimationState(attacking: false, idle: true);
        yield return new WaitForSeconds(ATTACK_COOLDOWN);
        
        combatState.SetCanAttack(true);
    }

    public void AttackEnd()
    {
        ExecuteAttackDamage();
        SetAnimationState(idle: true);
        HandlePostAttackMovement();
    }

    private void ExecuteAttackDamage()
    {
        Collider[] hitTargets = Physics.OverlapBox(
            attackCollider.bounds.center,
            attackCollider.bounds.extents,
            attackCollider.transform.rotation,
            LayerMask.GetMask("Player"));

        foreach (Collider target in hitTargets)
        {
            ProcessPlayerDamage(target);
        }
    }

    private void ProcessPlayerDamage(Collider target)
    {
        RL_Player player = target.GetComponent<RL_Player>();
        if (player != null && player.CurrentHealth > 0f)
        {
            player.DamagePlayer(enemyData.enemyAttack);
            PlayAttackSound();
        }
    }

    public void TakeDamage(int damageAmount)
    {
        enemyHP -= damageAmount;
        UpdateHealthBar();

        if (enemyHP > 0)
        {
            HandleDamageReaction();
        }
        else
        {
            HandleDeath();
        }
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
        if (!healthState.IsDead)
        {
            playerTracking.SetInRange(true);
            waypointNavigation.SetPatrolling(false);
            
            if (RL_Player.Instance != null)
            {
                playerTracking.SetPlayerPosition(RL_Player.Instance.transform.position);
                RotateTowardsTarget(playerTracking.PlayerPosition);
            }
        }
    }

    #endregion

    #region Movement System

    private void HandleMovementBehavior()
    {
        if (healthState.IsDead) return;

        if (!playerTracking.IsInRange && HasValidWaypoints())
        {
            ExecuteWaypointMovement();
        }
        else if (!combatState.IsAttacking)
        {
            SetAnimationState(idle: true, walking: false);
        }
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
        Vector3 targetPosition = waypointNavigation.GetCurrentWaypointPosition();
        Vector3 movementDirection = CalculateMovementDirection(targetPosition);
        
        RotateTowardsTarget(targetPosition);
        ExecuteMovement(movementDirection);
        
        CheckWaypointReached(targetPosition);
    }

    private Vector3 CalculateMovementDirection(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        return ApplyObstacleAvoidance(direction);
    }

    private Vector3 ApplyObstacleAvoidance(Vector3 direction)
    {
        if (Physics.SphereCast(transform.position, 0.5f, direction, out RaycastHit hit, 2f, obstacleMask))
        {
            Vector3 avoidanceDirection = Vector3.Cross(hit.normal, Vector3.up).normalized;
            return (direction + avoidanceDirection * 0.5f).normalized;
        }
        return direction;
    }

    private void ExecuteMovement(Vector3 direction)
    {
        Vector3 newPosition = transform.position + direction * moveSpeed * Time.deltaTime;
        rigidBody.MovePosition(newPosition);
    }

    private void CheckWaypointReached(Vector3 targetPosition)
    {
        float distanceToWaypoint = Vector3.Distance(transform.position, targetPosition);
        if (distanceToWaypoint < waypointThreshold)
        {
            waypointNavigation.MoveToNextWaypoint();
        }
    }

    private void RotateTowardsTarget(Vector3 targetPosition)
    {
        Vector3 directionToTarget = (targetPosition - transform.position).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    #endregion

    #region Animation Management

    private void UpdateAnimationStates()
    {
        if (combatState.IsAttacking && combatState.CanAttack)
        {
            return; // Attack handling is done in HandleCombatBehavior
        }

        if (playerTracking.IsInRange)
        {
            RotateTowardsTarget(playerTracking.PlayerPosition);
        }
    }

    private void SetAnimationState(bool idle = false, bool walking = false, bool attacking = false, bool dead = false)
    {
        if (animator == null) return;

        animator.SetBool("isIdle", idle);
        animator.SetBool("isWalking", walking);
        animator.SetBool("isAttacking", attacking);
        animator.SetBool("isDead", dead);
    }

    private void PlayHitAnimation()
    {
        if (animator != null)
        {
            animator.SetTrigger("getHit");
        }
    }

    #endregion

    #region Health and Death

    private void UpdateHealthBar()
    {
        if (healthBar != null)
        {
            healthBar.SetHealth(enemyHP);
        }
    }

    private void HandleDeath()
    {
        healthState.SetDead(true);
        SetAnimationState(dead: true);
        
        PlayDeathSound();
        DisableCollider();
        HideHealthBar();
        NotifyGameProgression();
        SpawnLoot();
        
        Destroy(gameObject, DESTROY_DELAY);
    }

    private void DisableCollider()
    {
        GetComponent<Collider>().enabled = false;
    }

    private void HideHealthBar()
    {
        if (healthBar != null)
        {
            healthBar.gameObject.SetActive(false);
        }
    }

    private void SpawnLoot()
    {
        if (lootManager != null)
        {
            lootManager.SpawnGearLoot(transform);
        }
    }

    #endregion

    #region Audio and Effects

    private void PlayHitSound()
    {
        if (AudioManager.instance != null)
        {
            AudioManager.instance.PlayEnemyGetHitSound(enemyType);
        }
    }

    private void PlayDeathSound()
    {
        if (AudioManager.instance != null)
        {
            AudioManager.instance.PlayEnemyDieSound(enemyType);
        }
    }

    private void PlayAttackSound()
    {
        if (AudioManager.instance != null)
        {
            AudioManager.instance.PlayEnemyAttackSound(enemyType);
        }
    }

    private void CreateHitEffect()
    {
        if (vfxManager != null)
        {
            vfxManager.EnemyGettingHit(particlePosition, enemyType);
        }
    }

    #endregion

    #region UI Management

    private void ShowEnemyStats()
    {
        if (statDisplay != null)
        {
            statDisplay.ShowEnemyStats();
        }
    }

    private void HideEnemyStats()
    {
        if (statDisplay != null)
        {
            statDisplay.HideEnemyStats();
        }
    }

    #endregion

    #region Utility Methods

    private bool IsPlayerHitbox(Collider collider)
    {
        return collider.CompareTag("Player") && 
               collider.gameObject.layer == LayerMask.NameToLayer("Hitbox");
    }

    private bool HasValidWaypoints()
    {
        return waypoints != null && waypoints.Length > 0;
    }

    private void HandlePostAttackMovement()
    {
        // Additional post-attack logic can be added here
    }

    private void NotifyGameProgression()
    {
        if (GameProgression.Instance != null)
        {
            GameProgression.Instance.EnemyKill();
        }
    }

    #endregion

    #region Public API

    public float GetHealthPercentage()
    {
        return (float)enemyHP / enemyData.enemyHealth;
    }

    public bool IsHealthLow()
    {
        return enemyHP <= enemyData.enemyHealth * 0.2f;
    }

    public bool IsDead()
    {
        return healthState.IsDead;
    }

    public float GetDistanceToCurrentWaypoint()
    {
        return waypointNavigation.GetDistanceToCurrentWaypoint(transform.position);
    }

    public Vector3 GetWaypointDirection()
    {
        return waypointNavigation.GetDirectionToCurrentWaypoint(transform.position);
    }

    public bool IsCaughtPlayer => false; // Simplified for clean code

    #endregion
}

#region State Classes

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

    public void SetInRange(bool inRange)
    {
        IsInRange = inRange;
    }

    public void SetPlayerPosition(Vector3 position)
    {
        PlayerPosition = position;
    }

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
    private Transform[] waypoints;
    private int currentWaypointIndex;
    private float startWaitTime;
    
    public bool IsPatrolling { get; private set; }
    public float WaitTime { get; private set; }

    public WaypointNavigationState(Transform[] waypoints, float waitTime)
    {
        this.waypoints = waypoints;
        this.startWaitTime = waitTime;
        this.currentWaypointIndex = Random.Range(0, waypoints?.Length ?? 0);
        this.IsPatrolling = false;
        this.WaitTime = 0f;
    }

    public void SetPatrolling(bool patrolling)
    {
        IsPatrolling = patrolling;
    }

    public void MoveToNextWaypoint()
    {
        if (waypoints != null && waypoints.Length > 0)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            IsPatrolling = true;
            WaitTime = startWaitTime;
        }
    }

    public Vector3 GetCurrentWaypointPosition()
    {
        if (waypoints != null && waypoints.Length > 0 && currentWaypointIndex < waypoints.Length)
        {
            return waypoints[currentWaypointIndex].position;
        }
        return Vector3.zero;
    }

    public void DecrementWaitTime()
    {
        WaitTime -= Time.deltaTime;
    }

    public float GetDistanceToCurrentWaypoint(Vector3 currentPosition)
    {
        if (waypoints == null || waypoints.Length == 0) return -1f;
        return Vector3.Distance(currentPosition, GetCurrentWaypointPosition());
    }

    public Vector3 GetDirectionToCurrentWaypoint(Vector3 currentPosition)
    {
        if (waypoints == null || waypoints.Length == 0) return Vector3.zero;
        return (GetCurrentWaypointPosition() - currentPosition).normalized;
    }
}

public class CombatState
{
    public bool IsAttacking { get; private set; }
    public bool CanAttack { get; private set; } = true;

    public void SetAttacking(bool attacking)
    {
        IsAttacking = attacking;
    }

    public void SetCanAttack(bool canAttack)
    {
        CanAttack = canAttack;
    }
}

public class HealthState
{
    public bool IsDead { get; private set; }

    public void SetDead(bool dead)
    {
        IsDead = dead;
    }
}

#endregion