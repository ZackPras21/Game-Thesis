using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RL_EnemyController : MonoBehaviour
{
    #region Variables

    // RL State
    [Header("RL State")]
    public Vector3 m_PlayerPosition;
    public Vector3 playerLastPosition;
    public float m_WaitTime;
    public float m_TimeToRotate;

    public float rotationSpeed = 5f;
    public float moveSpeed = 3f;
    public float waypointThreshold = 0.5f;
    public bool IsCaughtPlayer => m_CaughtPlayer;
    private int m_CurrentWaypointIndex = 0;

    public Transform[] waypoints;
    public LayerMask obstacleMask; // For pathfinding collision avoidance

    // Combat
    [Header("Combat")]
    public int enemyHP;
    [SerializeField] private HealthBar healthBar;
    public BoxCollider boxCollider;
    private bool canAttack = true;

    // State
    [Header("State")]
    public float startWaitTime = 4;
    public float timeToRotate = 2;
    [Header("ML-Agents Access")]
    public bool m_PlayerInRange;
    public bool m_PlayerNear;
    public bool m_IsPatrol;
    bool m_CaughtPlayer;
    public bool m_IsAttacking = false;
    public Vector3 PlayerPosition => m_PlayerPosition;
    private bool isDead = false;
    public NonBossEnemyState nonBossEnemyState;

    // References
    [Header("References")]
    public Animator animator;
    public LootManager lootManager;
    public VFXManager vfxManager;
    public Transform positionParticles;
    public EnemyType enemyType;
    private EnemyData enemyData;
    private List<Vector3> occupiedWaypoints = new List<Vector3>();
    public float separationRadius = 2f;
    public LayerMask enemyMask;
    public static RL_Player Instance;
    private bool playerAlive = true;
    private Transform playerTransform;

    // New reference for EnemyStatDisplay
    private EnemyStatDisplay enemyStatDisplay;

    #endregion

    #region Unity Methods

    #endregion

    #region Unity Methods

    void Start()
    {
        InitializeEnemyData();
        boxCollider = GetComponent<BoxCollider>();
        enemyStatDisplay = GetComponent<EnemyStatDisplay>();
        SetupInitialValues();

        // Force Animator into Idle state on spawn:
        if (animator != null)
        {
            animator.SetBool("isIdle", true);
            animator.SetBool("isWalking", false);
            animator.SetBool("isAttacking", false);
            animator.SetBool("isDead", false);
        }
    }

    void Update()
    {
        if (playerTransform == null)
        {
            // If our existing reference was destroyed, we force “patrol” mode
            m_PlayerInRange = false;
            m_IsPatrol = true;
            animator.SetBool("isIdle", false);
            animator.SetBool("isWalking", true);
        }

        if (m_PlayerInRange && playerTransform != null && playerAlive)
        {
            // Rotate→Move→Attack normally
            RotateTowardsPlayer(playerTransform.position, rotationSpeed);
            StartCoroutine(Attack());
        }
        else
        {
            MoveBetweenWaypoints();
        }
        
        if (!isDead)
        {
            if (!m_PlayerInRange && waypoints != null && waypoints.Length > 0)
            {
                MoveBetweenWaypoints();
            }
        }
        
        HandleAttacking();

        // Only patrol/way‐point move when not in range (IsPatrol == true)
        if (!m_PlayerInRange && waypoints != null && waypoints.Length > 0)
        {
            MoveBetweenWaypoints();
        }
        else if (!m_IsAttacking)
        {
            // If m_PlayerInRange is true AND not attacking, ensure enemy goes Idle:
            animator.SetBool("isIdle", true);
            animator.SetBool("isWalking", false);
        }
    }
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && other.gameObject.layer == LayerMask.NameToLayer("Hitbox"))
        {
            m_IsAttacking = true;
            canAttack = true;
        }

        // Show stats when the player is nearby
        if (enemyStatDisplay != null)
        {
            enemyStatDisplay.ShowEnemyStats();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && other.gameObject.layer == LayerMask.NameToLayer("Hitbox"))
        {
            m_IsAttacking = false;
            
            // Hide stats when the player moves away
            if (enemyStatDisplay != null)
            {
                enemyStatDisplay.HideEnemyStats();
            }
        }
    }

    #endregion

    #region Private Methods

    private void InitializeEnemyData()
    {
        switch (enemyType)
        {
            case EnemyType.Creep:
                enemyData = CreepEnemyData.Instance;
                break;
            case EnemyType.Medium1:
                enemyData = Medium1EnemyData.medium1EnemyData;
                break;
            case EnemyType.Medium2:
                enemyData = Medium2EnemyData.medium2EnemyData;
                break;
        }

        enemyHP = enemyData.enemyHealth;
        if (healthBar != null)
        {
            healthBar.SetMaxHealth(enemyHP);
        }
    }

    public void SetupInitialValues()
    {
        
        m_PlayerPosition = Vector3.zero;
        m_IsPatrol = false; // Start in active state
        m_CaughtPlayer = false;
        m_WaitTime = 0f; // No wait time
        m_TimeToRotate = 0f; // No rotation delay
        m_PlayerInRange = true; // Always consider player in range
        m_CurrentWaypointIndex = Random.Range(0, waypoints.Length); 
        
    }


    private void HandleAttacking()
    {
        if (m_IsAttacking && canAttack)
        {
            StartCoroutine(Attack());
        }

        if (m_PlayerInRange)
        {
            RotateTowardsPlayer(m_PlayerPosition, rotationSpeed);
        }
    }


    public void RotateTowardsPlayer(Vector3 playerPosition, float rotationSpeed)
    {
        Vector3 directionToPlayer = (playerPosition - transform.position).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

     private void OnEnable()
    {
        RL_Player.OnPlayerDestroyed += HandlePlayerDestroyed;
    }

    private void OnDisable()
    {
        RL_Player.OnPlayerDestroyed -= HandlePlayerDestroyed;
    }

    private void HandlePlayerDestroyed()
    {
        // Immediately drop “inRange” and clear the Transform reference
        m_PlayerInRange = false;
        playerAlive = false;
        playerTransform = null;
    }

    public void SetTarget(Transform target)
    {
        if (target == null)
        {
            // If someone calls SetTarget(null), drop out
            m_PlayerInRange = false;
            playerTransform = null;
            return;
        }

        // If the player is flagged as dead, ignore
        if (!playerAlive) return;

        playerTransform = target;
        m_PlayerInRange = true;
        m_IsPatrol = false;
        m_PlayerPosition = target.position;
    }


    private Vector3 GetSeparationVector()
    {
        Vector3 separation = Vector3.zero;
        Collider[] colliders = Physics.OverlapSphere(transform.position, separationRadius, enemyMask);
        foreach (Collider collider in colliders)
        {
            if (collider.transform != transform)
            {
                Vector3 direction = transform.position - collider.transform.position;
                separation += direction.normalized / direction.magnitude;
            }
        }
        return separation;
    }

    private IEnumerator RemoveOccupiedPointAfterDelay(Vector3 point, float delay)
    {
        yield return new WaitForSeconds(delay);
        occupiedWaypoints.Remove(point);
    }



    private void CaughtPlayer()
    {
        m_CaughtPlayer = true;
    }


    #endregion

    #region Public Methods
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
        return isDead;
    }
    #endregion


    #region Combat Methods

    public void TakeDamage(int damageAmount)
    {
        enemyHP -= damageAmount;

        if (healthBar != null)
        {
            healthBar.SetHealth(enemyHP);
        }

        if (enemyHP > 0)
        {
            GetHit();
            vfxManager.EnemyGettingHit(positionParticles, enemyType);
            OnPlayerAttack();
        }
        else
        {
            Die();
        }
    }
    public void OnPlayerAttack()
    {
        if (!isDead)
        {
            m_PlayerInRange = true;
            m_IsPatrol = false;
            m_PlayerPosition = RL_Player.Instance.transform.position;

            RotateTowardsPlayer(m_PlayerPosition, rotationSpeed);

            // Player tracking logic remains without NavMesh
        }
    }

    private void NextPoint()
    {
        // RL-friendly waypoint selection
        m_CurrentWaypointIndex = (m_CurrentWaypointIndex + 1) % waypoints.Length;
        m_IsPatrol = true;
        m_WaitTime = startWaitTime;
    }

    private void MoveBetweenWaypoints()
    {
        if (waypoints == null || waypoints.Length == 0 || m_PlayerInRange) return;

        Vector3 targetPosition = waypoints[m_CurrentWaypointIndex].position;
        Vector3 direction = (targetPosition - transform.position).normalized;

        // Obstacle avoidance
        if (Physics.SphereCast(transform.position, 0.5f, direction, out RaycastHit hit, 2f, obstacleMask))
        {
            Vector3 avoidDirection = Vector3.Cross(hit.normal, Vector3.up).normalized;
            direction = (direction + avoidDirection * 0.5f).normalized;
        }

        if (m_IsPatrol)
        {
            // Animation states are now handled by NormalEnemyActions
            RotateTowardsPlayer(targetPosition, rotationSpeed);

            float distanceToWaypoint = Vector3.Distance(transform.position, targetPosition);
            if (distanceToWaypoint < waypointThreshold)
            {
                NextPoint();
            }
        }
        else if (m_WaitTime <= 0)
        {
            m_IsPatrol = true;
        }
        else
        {
            m_WaitTime -= Time.deltaTime;
        }
        Vector3 newPos = transform.position + direction * moveSpeed * Time.deltaTime;
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.MovePosition(newPos);
    }

    // RL Helper Methods
    public float GetDistanceToCurrentWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return -1f;
        return Vector3.Distance(transform.position, waypoints[m_CurrentWaypointIndex].position);
    }

    public Vector3 GetWaypointDirection()
    {
        if (waypoints == null || waypoints.Length == 0) return Vector3.zero;
        return (waypoints[m_CurrentWaypointIndex].position - transform.position).normalized;
    }


    private void GetHit()
    {
        animator.SetTrigger("getHit");
        if (AudioManager.instance != null)
        {
            AudioManager.instance.PlayEnemyGetHitSound(enemyType);
        }
    }

    private void Die()
    {
        animator.SetBool("isDead", true);
        if (AudioManager.instance != null)
        {
            AudioManager.instance.PlayEnemyDieSound(enemyType);
        }


        GetComponent<Collider>().enabled = false;

        if (healthBar != null)
        {
            healthBar.gameObject.SetActive(false);
        }
        GameProgression.Instance.EnemyKill();
        lootManager.SpawnGearLoot(transform);
        isDead = true;
        Destroy(gameObject, 8f);
    }


    IEnumerator Attack()
    {
        canAttack = false;
        // 1) Set Attack on:
        animator.SetBool("isAttacking", true);
        animator.SetBool("isIdle", false);
        animator.SetBool("isWalking", false);

        // 2) Wait for the “Attack” clip length (1 second assumed):
        yield return new WaitForSeconds(1f);

        // 3) Turn Attack off; decide next state – assume Idle if still in range:
        animator.SetBool("isAttacking", false);

        // If still “player in range,” maybe stay Idle until next attack:
        animator.SetBool("isIdle", true);

        yield return new WaitForSeconds(2f);

        canAttack = true;
    }

    public void AttackEnd()
    {
        // Visualize attack range in Scene view
        Debug.DrawLine(transform.position, boxCollider.bounds.center, Color.red, 1f);

        Collider[] colliders = Physics.OverlapBox(
            boxCollider.bounds.center,
            boxCollider.bounds.extents,
            boxCollider.transform.rotation,
            LayerMask.GetMask("Player"));  // Use Player layer instead of playerMask

        foreach (Collider collider in colliders)
        {
            RL_Player player = collider.GetComponent<RL_Player>();
            if (player != null && player.CurrentHealth > 0f)
            {
                player.DamagePlayer(enemyData.enemyAttack);
                if (AudioManager.instance != null)
                {
                    AudioManager.instance.PlayEnemyAttackSound(enemyType);
                }
                else
                {
                    Debug.LogWarning("[RL_EnemyController] RL_Player.Instance is null. Cannot damage the player.");
                }
                break;
            }
        }
        animator.SetBool("isIdle", true);
        MoveBetweenWaypoints();
    }

    IEnumerator knockback()
    {
        Vector3 knockbackDirection = (transform.position - RL_Player.Instance.transform.position).normalized;
        float knockbackForce = 10f;
        float knockbackDuration = 0.5f;

        float elapsedTime1 = 0f;
        while (elapsedTime1 < knockbackDuration)
        {
            transform.position += knockbackDirection * knockbackForce * Time.deltaTime;
            elapsedTime1 += Time.deltaTime;
            yield return null;
        }
    }



    #endregion
}
