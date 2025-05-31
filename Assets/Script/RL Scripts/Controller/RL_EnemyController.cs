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

    // Detection
    [Header("Detection")]
    public float viewRadius = 15;
    public float viewAngle = 90;
    public LayerMask playerMask;
    public LayerMask obstacleMask;
    public float meshResolution = 1f;
    public int edgeInterations = 4;
    public float edgeDistance = 0.5f;
    public Transform[] waypoints;

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
    }

    void Update()
    {
        if (!isDead)
        {
            EnvironmentView();
            
            if (!m_PlayerInRange && waypoints != null && waypoints.Length > 0)
            {
                MoveBetweenWaypoints();
            }
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
        if (m_IsAttacking)
        {
        }

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

    public void SetTarget(Transform target)
    {
        m_PlayerPosition = target.position;
        m_PlayerInRange = true;
        m_IsPatrol = false;
    }

    private void EnvironmentView()
    {
        // Always active behavior - no player checks needed
        m_PlayerInRange = true;
        m_IsPatrol = false;
        
        // Still track player position if player exists
        Collider[] playerInRange = Physics.OverlapSphere(transform.position, viewRadius, playerMask);
        if (playerInRange.Length > 0)
        {
            m_PlayerPosition = playerInRange[0].transform.position;
        }
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
            m_PlayerPosition = PlayerController.Instance.transform.position;

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
        
        // RL Observable: Distance to waypoint
        float distanceToWaypoint = Vector3.Distance(transform.position, targetPosition);
        
        // Move with RL policy in mind
        if (m_IsPatrol)
        {
            transform.position += direction * moveSpeed * Time.deltaTime;
            RotateTowardsPlayer(targetPosition, rotationSpeed);
            
            // RL Reward signal when reaching waypoint
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
        animator.SetTrigger("GetHit");
        if (AudioManager.instance != null)
        {
            AudioManager.instance.PlayEnemyGetHitSound(enemyType);
        }
    }

    private void Die()
    {
        animator.SetTrigger("Die");
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
        animator.SetBool("IsAttacking", true);
        yield return new WaitForSeconds(1);
        animator.SetBool("IsAttacking", false);
        animator.SetBool("IsWalking", false);
        yield return new WaitForSeconds(2);
        canAttack = true;
    }

    public void AttackEnd()
    {
        Collider[] colliders = Physics.OverlapBox(boxCollider.bounds.center, boxCollider.bounds.extents, boxCollider.transform.rotation);
        foreach (Collider collider in colliders)
        {
            if (collider.CompareTag("Player"))
            {
                PlayerController.Instance.DamagePlayer(enemyData.enemyAttack, knockback, transform.position);
                if (AudioManager.instance != null)
                {
                    AudioManager.instance.PlayEnemyAttackSound(enemyType);
                }
                break;
            }
        }
    }

    IEnumerator knockback()
    {
        Vector3 knockbackDirection = (transform.position - PlayerController.Instance.transform.position).normalized;
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
