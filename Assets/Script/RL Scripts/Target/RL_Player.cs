using UnityEngine;

public class RL_Player : MonoBehaviour
{
    [Header("Player Stats")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackCooldown = 1f;
    private float lastAttackTime;

    [Header("Spawn Settings")]
    [SerializeField] private LayerMask spawnCollisionLayers;
    [SerializeField] private float spawnRadiusCheck = 1f;
    [SerializeField] public bool isTrainingTarget = false;

    private bool isDead = false;
    private Collider[] colliders;
    private Vector3 initialPosition;

    private void Awake()
    {
        currentHealth = maxHealth;
        colliders = GetComponentsInChildren<Collider>();
        initialPosition = transform.position;
    }

    public void SpawnAsTrainingTarget(Vector3 spawnPosition)
    {
        isTrainingTarget = true;
        transform.position = spawnPosition;
        Respawn();
    }

    public void SpawnRandomly(Vector3 spawnAreaCenter, Vector3 spawnAreaSize)
    {
        Vector3 randomPosition;
        bool positionFound = false;
        int attempts = 0;
        int maxAttempts = 20;

        do
        {
            randomPosition = new Vector3(
                Random.Range(spawnAreaCenter.x - spawnAreaSize.x / 2, spawnAreaCenter.x + spawnAreaSize.x / 2),
                spawnAreaCenter.y,
                Random.Range(spawnAreaCenter.z - spawnAreaSize.z / 2, spawnAreaCenter.z + spawnAreaSize.z / 2)
            );

            if (!Physics.CheckSphere(randomPosition, spawnRadiusCheck, spawnCollisionLayers))
            {
                positionFound = true;
                transform.position = randomPosition;
            }
            attempts++;
        } while (!positionFound && attempts < maxAttempts);

        if (!positionFound)
        {
            Debug.LogWarning("Failed to find valid spawn position after " + maxAttempts + " attempts");
        }
    }

    private void Update()
    {
        if (isDead) return;

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, attackRange);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Enemy") && Time.time > lastAttackTime + attackCooldown)
            {
                Attack(hitCollider.GetComponent<EnemyController>());
                lastAttackTime = Time.time;
                break;
            }
        }
    }

    private void Attack(EnemyController enemy)
    {
        if (enemy != null)
        {
            enemy.TakeDamage((int)attackDamage);
        }
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        Debug.Log("Player took " + damage + " damage. Current health: " + currentHealth);

        if (currentHealth <= 0)
        {
            if (isTrainingTarget)
            {
                Respawn();
            }
            else
            {
                Die();
            }
        }
    }

    private void Die()
    {
        isDead = true;
        
        if (isTrainingTarget)
        {
            gameObject.SetActive(false);
            FindObjectOfType<TrainingTargetSpawner>()?.TargetDestroyed();
        }
        else
        {
            Debug.Log("Player has died!");
            
            var enemies = FindObjectsOfType<EnemyController>();
            foreach (var enemy in enemies)
            {
                if (enemy != null)
                {
                    enemy.gameObject.SetActive(false);
                }
            }

            foreach (var collider in colliders)
            {
                collider.enabled = false;
            }
        }
    }

    public void Respawn()
    {
        isDead = false;
        currentHealth = maxHealth;

        foreach (var collider in colliders)
        {
            collider.enabled = true;
        }

        if (isTrainingTarget)
        {
            transform.position = initialPosition;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}