using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RL_Player : MonoBehaviour
{
    public static RL_Player Instance;

    [Header("Training Target")]
    [Tooltip("Set to true if this RL_Player is being used as a training target")]
    public bool isTrainingTarget = false;
    
    private Vector3 playerVelocity;
    private float gravityValue = -9.81f;
    [SerializeField] private Transform ParticleSpawnPoint;

    private bool canDash = true;

    [Header("Player Stats (Health)")]
    [Tooltip("Maximum health for the player.")]
    [SerializeField] private float maxHealth = 100f;
    // Internally track current health
    private float currentHealth;
    public float CurrentHealth => currentHealth;
    public float turnSpeed = 4f;
    public static event System.Action OnPlayerDestroyed;

    [Header("UI Health Bar (Optional)")]
    [Tooltip("If you want a world‑space health bar above the player, assign a Slider here.")]
    [SerializeField] private Slider healthBarSlider;

    [Header("Animation")]
    [Tooltip("Drag the player's Animator component here (for Idle, Walking, GetHit, Death).")]
    [SerializeField] private Animator animator;

    [Header("Particles (Hurt + Death)")]
    [Tooltip("Particle system that plays when the player is hurt (health > 0).")]
    [SerializeField] private ParticleSystem hurtParticle;
    [Tooltip("Particle system that plays when the player dies (health ≤ 0).")]
    [SerializeField] private ParticleSystem deathParticle;

    [Header("Invincibility")]
    [Tooltip("Seconds of invincibility after getting hit.")]
    [SerializeField] private float invincibilityDuration = 0.5f;

    [Header("Player Attack Settings")]
    [Tooltip("Does the player auto‐attack the target every attackInterval seconds?")]
    [SerializeField]
    private bool attackEnabled = false;

    [Tooltip("Number of seconds between each automatic attack on the target.")]
    [SerializeField]
    private float attackInterval = 2.0f;

    [Tooltip("Damage dealt per attack.")]
    [SerializeField]
    private float attackDamage = 30f;
    [SerializeField] private float attackRange = 5f;

    private Coroutine _attackCoroutine;

    private bool isInvincible = false;

    // Track whether the player is still alive
    private bool isAlive = true;

    // Cache all Colliders (so we can disable them on death)
    private Collider[] colliders;

    // If you want to reset the player’s position when respawning, store the initial position:
    private Vector3 initialPosition;

    private void Start()
    {
        // Store initial spawn position (for Respawn)
        initialPosition = transform.position;

        if (attackEnabled)
        {
            _attackCoroutine = StartCoroutine(AutomaticAttackRoutine());
        }

        if (animator == null)
        {
            Debug.LogWarning("[RL_Player] Animator is not assigned. GetHit/Death animations won't play.");
        }
        else
        {
            // Reset all Animator parameters to Idle at start
            animator.SetBool("isIdle", true);
            animator.SetBool("isWalking", false);
            animator.ResetTrigger("AttackTrigger");
            animator.ResetTrigger("getHit");
            animator.SetBool("isDead", false);
        }

        // Initialize current health & health bar
        currentHealth = maxHealth;
        if (healthBarSlider != null)
        {
            healthBarSlider.maxValue = maxHealth;
            healthBarSlider.value = currentHealth;
        }

        // Warn if any particle systems are missing
        if (hurtParticle == null)
            Debug.LogWarning("[RL_Player] HurtParticle is not assigned. No hurt VFX will appear.");
        if (deathParticle == null)
            Debug.LogWarning("[RL_Player] DeathParticle is not assigned. No death VFX will appear.");
    }

    public void DamagePlayer(float damageAmount)
    {
        if (!isAlive) return;
        if (isInvincible) return;

        // Subtract health
        currentHealth -= damageAmount;

        // Update UI health bar
        if (healthBarSlider != null)
        {
            healthBarSlider.value = Mathf.Max(currentHealth, 0f);
        }

        if (currentHealth > 0f)
        {
            // Survived the hit → play Hurt animation & particle
            if (animator != null)
            {
                animator.SetTrigger("getHit");
            }
            if (hurtParticle != null)
            {
                hurtParticle.Play();
            }

            // Begin invincibility frames
            StartCoroutine(InvincibilityRoutine());
        }
        else
        {
            // Health reached zero or below → die
            Die();
        }
    }

    public void SetAutoAttackEnabled(bool enabled)
    {
        if (enabled && _attackCoroutine == null)
        {
            attackEnabled = true;
            _attackCoroutine = StartCoroutine(AutomaticAttackRoutine());
        }
        else if (!enabled && _attackCoroutine != null)
        {
            attackEnabled = false;
            StopCoroutine(_attackCoroutine);
            _attackCoroutine = null;
        }
    }

    private IEnumerator AutomaticAttackRoutine()
    {
        Debug.Log($"[RL_Player] Starting attack routine with interval {attackInterval}s");
        while (attackEnabled)
        {
            yield return new WaitForSeconds(attackInterval);
            Debug.Log($"[RL_Player] Attack interval reached, checking for enemies");

            // Get all colliders in attack range
            Collider[] hits = Physics.OverlapSphere(
                transform.position,
                attackRange
            );

            // Filter for enemies by tag
            List<Transform> enemies = new List<Transform>();
            foreach (var hit in hits)
            {
                if (hit != null && hit.CompareTag("Enemy"))
                {
                    enemies.Add(hit.transform);
                }
            }

            // Debug visualization
            Debug.DrawRay(transform.position, transform.forward * attackRange, Color.red, attackInterval);
            Debug.Log($"[RL_Player] Found {enemies.Count} enemies in {attackRange}m range");
            
            foreach (var enemy in enemies)
            {
                Debug.DrawLine(transform.position, enemy.position, Color.yellow, attackInterval);
                Debug.Log($"- Enemy: {enemy.gameObject.name}");
            }

            if (enemies.Count > 0)
            {
                // Attack the first detected enemy
                PerformAttack(enemies[0]);
            }
        }
    }

    private void PerformAttack(Transform enemy)
    {
        if (enemy == null)
        {
            Debug.LogWarning("[RL_Player] PerformAttack called with null enemy");
            return;
        }

        // ─── 1) Face the enemy ───
        Vector3 directionToEnemy = enemy.position - transform.position;
        directionToEnemy.y = 0f; 
        if (directionToEnemy.sqrMagnitude > 0.001f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(directionToEnemy);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * turnSpeed);
            Debug.Log($"[RL_Player] Rotated to face enemy {enemy.name}");
        }

        // ─── 2) Trigger attack animation ───
        if (animator != null)
        {
            animator.SetTrigger("AttackTrigger");
            Debug.Log("[RL_Player] Triggered Attack animation");
        }
        else
        {
            Debug.LogWarning("[RL_Player] Animator is null; cannot play Attack animation");
        }

        // ─── 3) Deal damage via GetComponentInParent ───
        NormalEnemyAgent enemyAgent = enemy.GetComponentInParent<NormalEnemyAgent>();
        RL_EnemyController enemyController = enemy.GetComponentInParent<RL_EnemyController>();

        if (enemyAgent != null)
        {
            Debug.Log($"[RL_Player] Dealing {attackDamage} to NormalEnemyAgent (GetComponentInParent)");
            enemyAgent.TakeDamage(attackDamage);
        }
        else if (enemyController != null)
        {
            Debug.Log($"[RL_Player] Dealing {attackDamage} to RL_EnemyController (GetComponentInParent)");
            enemyController.TakeDamage((int)attackDamage);
        }
        else
        {
            Debug.LogWarning($"[RL_Player] Enemy '{enemy.name}' has no NormalEnemyAgent or RL_EnemyController in parent chain");
        }
    }



    private IEnumerator InvincibilityRoutine()
    {
        isInvincible = true;
        yield return new WaitForSeconds(invincibilityDuration);
        isInvincible = false;
    }

    private void Die()
    {
        isAlive = false;

        // Trigger Death animation
        if (animator != null)
        {
            animator.SetBool("isDead", true);
        }

        // Play death VFX
        if (deathParticle != null)
        {
            deathParticle.Play();
        }

        // Disable **all** colliders on the player so enemies cannot hit a corpse
        foreach (var col in colliders)
        {
            if (col != null)
                col.enabled = false;
        }

        // Notify training target system before destruction
        var target = GetComponent<TrainingTarget>();
        if (target != null)
        {
            target.ForceNotifyDestruction();
        }
        
        OnPlayerDestroyed?.Invoke();
        // Finally, destroy this GameObject
        Destroy(gameObject);
        return;

    }

    public void Respawn()
    {
        isAlive = true;
        currentHealth = maxHealth;

        // Re‑enable colliders
        foreach (var col in colliders)
        {
            if (col != null)
                col.enabled = true;
        }

        // Reset Animator
        if (animator != null)
        {
            animator.SetBool("isDead", false);
            animator.SetBool("isWalking", false);
            animator.SetBool("isIdle", true);
            animator.ResetTrigger("getHit");
        }

        // Reset health bar
        if (healthBarSlider != null)
        {
            healthBarSlider.value = currentHealth;
        }

        // Move back to initial spawn point
        transform.position = initialPosition;
    }

    private void OnDrawGizmosSelected()
    {
        // Optionally visualize dash direction or attack range here:
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.2f); // small marker
    }
}
