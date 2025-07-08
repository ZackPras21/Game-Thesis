using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyController : MonoBehaviour
{
    #region Variables

    // Navigation
    [Header("Navigation")]
    public NavMeshAgent navMeshAgent;
    public float speedWalk = 6;
    public float speedRun = 9;
    public float rotationSpeed = 5f;
    public Transform[] waypoints;
    int m_CurrentWaypointIndex;
    Vector3 playerLastPosition = Vector3.zero;
    Vector3 m_PlayerPosition;
    float m_WaitTime;
    float m_TimeToRotate;
    public float patrolRadius = 5f;

    // Public accessors for RL state
    public Vector3 PlayerLastPosition => playerLastPosition;
    public float WaitTime => m_WaitTime;
    public float TimeToRotate => m_TimeToRotate;
    public bool IsCaughtPlayer => m_CaughtPlayer;

    // Detection
    [Header("Detection")]
    public float viewRadius = 15;
    public float viewAngle = 90;
    public LayerMask playerMask;
    public LayerMask obstacleMask;
    public float meshResolution = 1f;
    public int edgeInterations = 4;
    public float edgeDistance = 0.5f;

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
        SetupInitialValues();
        SetupNavMeshAgent();
        boxCollider = GetComponent<BoxCollider>();

        // Retrieve the EnemyStatDisplay component
        enemyStatDisplay = GetComponent<EnemyStatDisplay>();
    }

    void Update()
    {
        if (!isDead)
        {
            EnvironmentView();
            HandleAttacking();
            HandlePatrolingAndChasing();

            Vector3 separationVector = GetSeparationVector();
            navMeshAgent.SetDestination(navMeshAgent.destination + separationVector);
        }
    }
    void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            NavMeshObstacle obstacle = collision.gameObject.GetComponent<NavMeshObstacle>();
            if (obstacle == null)
            {
                obstacle = collision.gameObject.AddComponent<NavMeshObstacle>();
                obstacle.carving = true;
                obstacle.carveOnlyStationary = true;
                obstacle.carvingMoveThreshold = 0.1f;
                obstacle.shape = NavMeshObstacleShape.Box;
            }

            obstacle.center = Vector3.zero;
            obstacle.size = collision.gameObject.GetComponent<Collider>().bounds.size;
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
            if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
            {
                Move(speedWalk);
            }

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
            healthBar.SetHealth(enemyHP);
        }
    }

    public void SetupInitialValues()
    {
        m_PlayerPosition = Vector3.zero;
        m_IsPatrol = true;
        m_CaughtPlayer = false;
        m_WaitTime = startWaitTime;
        m_TimeToRotate = timeToRotate;
        m_CurrentWaypointIndex = Random.Range(0, waypoints.Length); // Randomize initial waypoint
    }

    private void SetupNavMeshAgent()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        navMeshAgent.isStopped = false;
        navMeshAgent.speed = speedWalk;
        navMeshAgent.SetDestination(GetRandomPointAroundWaypoint(waypoints[m_CurrentWaypointIndex])); // Use random point
        Move(speedWalk);
    }

    private void HandleAttacking()
    {
        if (m_IsAttacking)
        {
            StartCoroutine(StopNavMeshAgent());
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

    private void HandlePatrolingAndChasing()
    {
        if (!m_IsPatrol && !m_IsAttacking)
        {
            Chasing();
        }
        else if (!m_IsAttacking)
        {
            Patroling();
        }
    }

    private void RotateTowardsPlayer(Vector3 playerPosition, float rotationSpeed)
    {
        Vector3 directionToPlayer = (playerPosition - transform.position).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void EnvironmentView()
    {
        Collider[] playerInRange = Physics.OverlapSphere(transform.position, viewRadius, playerMask);

        for (int i = 0; i < playerInRange.Length; i++)
        {
            Transform player = playerInRange[i].transform;
            Vector3 dirToPlayer = (player.position - transform.position).normalized;
            if (Vector3.Angle(transform.forward, dirToPlayer) < viewAngle / 2)
            {
                float dstToPlayer = Vector3.Distance(transform.position, player.position);
                if (!Physics.Raycast(transform.position, dirToPlayer, dstToPlayer, obstacleMask))
                {
                    m_PlayerInRange = true;
                    m_IsPatrol = false;
                }
                else
                {
                    m_PlayerInRange = false;
                }
            }
            if (Vector3.Distance(transform.position, player.position) > viewRadius)
            {
                m_PlayerInRange = false;
            }
            if (m_PlayerInRange)
            {
                m_PlayerPosition = player.transform.position;
            }
            else
            {
                m_IsPatrol = true;
            }
        }
    }

    private void Move(float speed)
    {
        if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.isStopped = false;
            navMeshAgent.speed = speed;
            animator.SetBool("IsWalking", true);
        }
        else
        {
            // Debug.LogWarning("NavMeshAgent is not properly set up or not on a NavMesh.");
        }
    }

    private void Patroling()
    {
        if (m_PlayerNear)
        {
            if (m_TimeToRotate <= 0)
            {
                Move(speedWalk);
                LookingPlayer(playerLastPosition);
            }
            else
            {
                StartCoroutine(StopNavMeshAgent());
                m_TimeToRotate -= Time.deltaTime;
            }
        }
        else
        {
            m_PlayerNear = false;
            playerLastPosition = Vector3.zero;
            if (navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance)
            {
                if (m_WaitTime <= 0)
                {
                    NextPoint();
                    Move(speedWalk);
                    m_WaitTime = startWaitTime;
                }
                else
                {
                    StartCoroutine(StopNavMeshAgent());
                    m_WaitTime -= Time.deltaTime;
                }
            }
        }
    }

    private void NextPoint()
    {
        int nextWaypointIndex;
        do
        {
            nextWaypointIndex = Random.Range(0, waypoints.Length);
        } while (nextWaypointIndex == m_CurrentWaypointIndex);

        m_CurrentWaypointIndex = nextWaypointIndex;
        navMeshAgent.SetDestination(GetRandomPointAroundWaypoint(waypoints[m_CurrentWaypointIndex]));
    }

    private Vector3 GetRandomPointAroundWaypoint(Transform waypoint)
    {
        Vector3 randomDirection;
        NavMeshHit hit;
        do
        {
            randomDirection = Random.insideUnitSphere * patrolRadius;
            randomDirection += waypoint.position;
        }
        while (NavMesh.SamplePosition(randomDirection, out hit, patrolRadius, 1) && occupiedWaypoints.Contains(hit.position));

        occupiedWaypoints.Add(hit.position);
        StartCoroutine(RemoveOccupiedPointAfterDelay(hit.position, 2.0f));
        return hit.position;
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

    private void LookingPlayer(Vector3 player)
    {
        navMeshAgent.SetDestination(player);
        if (Vector3.Distance(transform.position, player) <= 0.3f)
        {
            if (m_WaitTime <= 0)
            {
                m_PlayerNear = false;
                Move(speedWalk);
                navMeshAgent.SetDestination(waypoints[m_CurrentWaypointIndex].position);
                m_WaitTime = startWaitTime;
                m_TimeToRotate = timeToRotate;
            }
            else
            {
                StartCoroutine(StopNavMeshAgent());
                m_WaitTime -= Time.deltaTime;
            }
        }
    }

    private void Chasing()
    {
        m_PlayerNear = false;
        playerLastPosition = Vector3.zero;

        if (!m_CaughtPlayer)
        {
            Move(speedRun);
            navMeshAgent.SetDestination(m_PlayerPosition);
        }
        if (navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance)
        {
            if (m_WaitTime <= 0 && !m_CaughtPlayer && Vector3.Distance(transform.position, GameObject.FindGameObjectWithTag("Player").transform.position) >= 6f)
            {
                m_IsPatrol = true;
                m_PlayerNear = false;
                Move(speedWalk);
                m_TimeToRotate = timeToRotate;
                m_WaitTime = startWaitTime;
                navMeshAgent.SetDestination(waypoints[m_CurrentWaypointIndex].position);
            }
            else
            {
                if (Vector3.Distance(transform.position, GameObject.FindGameObjectWithTag("Player").transform.position) >= 2.5f)
                {
                    StartCoroutine(StopNavMeshAgent());
                    m_WaitTime -= Time.deltaTime;
                }
            }
        }
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

            if (!m_IsAttacking)
            {
                Chasing();
            }
        }
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

        navMeshAgent.isStopped = true;
        navMeshAgent.enabled = false;

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
        StartCoroutine(StopNavMeshAgent());
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
            navMeshAgent.Move(knockbackDirection * knockbackForce * Time.deltaTime);
            elapsedTime1 += Time.deltaTime;
            yield return null;
        }

        Move(speedWalk);
    }

    private IEnumerator StopNavMeshAgent()
    {
        if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.isStopped = true;
            animator.SetBool("IsWalking", false);
            nonBossEnemyState = NonBossEnemyState.Idle;

            yield return new WaitForSeconds(startWaitTime);

            if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.isStopped = false;
                navMeshAgent.SetDestination(navMeshAgent.destination); // Resume navigation
                animator.SetBool("IsWalking", true);
            }
            else
            {
                // Debug.LogWarning("NavMeshAgent is not properly set up or disabled.");
            }
        }
        else
        {
            // Debug.LogWarning("NavMeshAgent is not properly set up or disabled.");
        }
    }


    #endregion
}
