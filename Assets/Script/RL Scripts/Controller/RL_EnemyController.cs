using System.Collections;
using UnityEngine;

public class RL_EnemyController : MonoBehaviour
{
    #region Serialized Fields
    [Header("Combat Configuration")]
    public int enemyHP;
    public float attackRange = 3f; 
    [SerializeField] private float detectThreshold = 0.5f; 
    [SerializeField] private float fleeHealthThreshold = 0.2f; // Unused, consider removal if not implemented
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
    private const float ATTACK_COOLDOWN = 2f;
    private const float KNOCKBACK_FORCE = 10f; // Unused, consider removal if not implemented
    private const float KNOCKBACK_DURATION = 0.5f; // Unused, consider removal if not implemented
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
        if (enemyStates.PlayerTracking.IsInRange && 
            enemyStates.PlayerTracking.PlayerTransform != null && 
            enemyStates.PlayerTracking.IsPlayerAlive)
        {
            RotateTowardsTarget(enemyStates.PlayerTracking.PlayerPosition);

            if (enemyStates.CombatState.CanAttack)
                StartCoroutine(ExecuteAttackSequence());
        }
        else if (!enemyStates.CombatState.IsAttacking)
        {
            SetAnimationState(idle: true);
        }
    }

    private void HandlePlayerEnterCombat()
    {
        enemyStates.CombatState.SetAttacking(true);
        enemyStates.CombatState.SetCanAttack(true);
    }

    private void HandlePlayerExitCombat() => enemyStates.CombatState.SetAttacking(false);

    private IEnumerator ExecuteAttackSequence()
    {
        enemyStates.CombatState.SetCanAttack(false);
        enemyStates.CombatState.SetAttacking(true);

        if (attackCollider != null) attackCollider.enabled = true;

        SetAnimationState(attacking: true);
        yield return new WaitForSeconds(ATTACK_DURATION * 0.4f); 
        ExecuteAttackDamage();

        if (attackCollider != null) attackCollider.enabled = false;

        SetAnimationState(attacking: false, idle: true);
        yield return new WaitForSeconds(ATTACK_DURATION * 0.6f);

        enemyStates.CombatState.SetAttacking(false);
        enemyStates.CombatState.SetCanAttack(true);
    }

    public void AgentAttack()
    {
        if (enemyStates.CombatState.CanAttack)
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
            enemyStates.WaypointNavigation.MoveToNextWaypoint();
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
        if (enemyStates.CombatState.IsAttacking && enemyStates.CombatState.CanAttack) return;

        if (enemyStates.PlayerTracking.IsInRange)
            RotateTowardsTarget(enemyStates.PlayerTracking.PlayerPosition);
    }

    private void SetAnimationState(bool idle = false, bool walking = false, bool attacking = false, bool dead = false)
    {
        if (animator == null) return;

        animator.SetBool("isIdle", idle);
        animator.SetBool("isWalking", walking);
        animator.SetBool("isAttacking", attacking);
        animator.SetBool("isDead", dead);
        
        enemyStates.IsIdle = idle;
        enemyStates.CombatState.SetAttacking(attacking);
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