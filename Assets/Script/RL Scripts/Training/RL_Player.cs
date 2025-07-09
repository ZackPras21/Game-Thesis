using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RL_Player : MonoBehaviour
{
    public static RL_Player Instance;
    public static event System.Action OnPlayerDestroyed;

    [Header("Training Configuration")]
    [SerializeField] public bool isRL_TrainingTarget = false;

    [Header("Combat Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float turnSpeed = 4f;
    [SerializeField] private bool attackEnabled = false;
    [SerializeField] private float attackInterval = 2.0f;
    [SerializeField] private float attackDamage = 30f;
    [SerializeField] private float attackRange = 5f;

    [Header("Invincibility")]
    [SerializeField] private float invincibilityDuration = 0.5f;

    [Header("UI Components")]
    [SerializeField] private Slider healthBarSlider;

    [Header("Animation & Effects")]
    [SerializeField] private Animator animator;
    [SerializeField] private ParticleSystem hurtParticle;
    [SerializeField] private ParticleSystem deathParticle;

    public float CurrentHealth => currentHealth;
    public RL_TrainingTargetSpawner spawner;

    private float currentHealth;
    private bool isInvincible = false;
    private bool isAlive = true;
    private Vector3 initialPosition;
    private Collider[] colliders;
    private Coroutine attackCoroutine;

    private void Awake()
    {
        InitializePlayer();
    }

    private void Start()
    {
        SetupInitialState();
        // Enable attack for RL training targets
        attackEnabled = true;
        StartAttackRoutineIfEnabled();
        ValidateComponents();
    }


    public bool DamagePlayer(float damageAmount)
    {
        if (!CanTakeDamage()) return false;

        ApplyDamage(damageAmount);
        UpdateHealthBar();

        if (IsPlayerAlive())
        {
            // Always trigger hit effects when taking damage
            HandleNonFatalDamage(damageAmount);
            return false; // Player survived
        }
        else
        {
            Die();
            return true; // Player died
        }
    }

    public void SetAutoAttackEnabled(bool enabled)
    {
        if (enabled && attackCoroutine == null)
        {
            EnableAutoAttack();
        }
        else if (!enabled && attackCoroutine != null)
        {
            DisableAutoAttack();
        }
    }

    public void Respawn()
    {
        ResetPlayerState();
        EnableColliders();
        ResetAnimationState();
        UpdateHealthBar();
        ReturnToSpawnPoint();
    }

    private void InitializePlayer()
    {
        Instance = this;
        initialPosition = transform.position;
        colliders = GetComponentsInChildren<Collider>();
        animator = GetComponent<Animator>();
    }

    private void SetupInitialState()
    {
        initialPosition = transform.position;
        currentHealth = maxHealth;
        
        InitializeHealthBar();
        InitializeAnimationState();
    }

    private void StartAttackRoutineIfEnabled()
    {
        if (attackEnabled)
        {
            attackCoroutine = StartCoroutine(AutomaticAttackRoutine());
        }
    }

    private void ValidateComponents()
    {
        if (animator == null)
            Debug.LogWarning("Animator is not assigned. Animations won't play.");
        if (hurtParticle == null)
            Debug.LogWarning("Hurt particle is not assigned. No hurt VFX will appear.");
        if (deathParticle == null)
            Debug.LogWarning("Death particle is not assigned. No death VFX will appear.");
    }

    private bool CanTakeDamage()
    {
        return isAlive && !isInvincible;
    }

    private void ApplyDamage(float damageAmount)
    {
        currentHealth = Mathf.Clamp(currentHealth - damageAmount, 0f, maxHealth);
    }

    private void UpdateHealthBar()
    {
        if (healthBarSlider != null)
        {
            healthBarSlider.value = currentHealth;
        }
    }

    private bool IsPlayerAlive()
    {
        return currentHealth > 0f;
    }

    private void HandleNonFatalDamage(float damageAmount)
    {
        PlayHurtAnimation();
        PlayHurtEffect();
        StartCoroutine(InvincibilityRoutine());
        
        // Debug log to verify damage handling
        Debug.Log($"Player took {damageAmount} damage. Current health: {currentHealth}");
    }

    private void PlayHurtAnimation()
    {
        if (animator != null)
        {
            animator.SetTrigger("getHit");
        }
    }

    private void PlayHurtEffect()
    {
        if (hurtParticle != null)
        {
            // Ensure particle system is properly reset before playing
            hurtParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            hurtParticle.Play();
        }
        else
        {
            Debug.LogWarning("Hurt particle system is missing!");
        }
    }

    public void Die()
    {
        isAlive = false;
        PlayDeathAnimation();
        PlayDeathEffect();
        DisableColliders();
        NotifyDestruction();
        OnPlayerDestroyed?.Invoke();
        Destroy(gameObject);
    }

    private void PlayDeathAnimation()
    {
        if (animator != null)
        {
            animator.SetBool("isDead", true);
        }
    }

    private void PlayDeathEffect()
    {
        if (deathParticle != null)
        {
            deathParticle.Play();
        }
    }

    private void DisableColliders()
    {
        foreach (var collider in colliders)
        {
            if (collider != null)
                collider.enabled = false;
        }
    }

    private void NotifyDestruction()
    {
        var target = GetComponent<RL_TrainingTarget>();
        if (target != null)
        {
            target.ForceNotifyDestruction();
        }
    }

    private void EnableAutoAttack()
    {
        attackEnabled = true;
        attackCoroutine = StartCoroutine(AutomaticAttackRoutine());
    }

    private void DisableAutoAttack()
    {
        attackEnabled = false;
        StopCoroutine(attackCoroutine);
        attackCoroutine = null;
    }

    private IEnumerator AutomaticAttackRoutine()
    {
        while (attackEnabled)
        {
            yield return new WaitForSeconds(attackInterval);
            
            List<Transform> enemiesInRange = FindEnemiesInRange();
            
            if (enemiesInRange.Count > 0)
            {
                PerformAttack(enemiesInRange[0]);
            }
        }
    }

    private List<Transform> FindEnemiesInRange()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, attackRange);
        List<Transform> enemies = new List<Transform>();
        
        foreach (var hit in hits)
        {
            if (hit != null && hit.CompareTag("Enemy"))
            {
                enemies.Add(hit.transform);
            }
        }
        
        return enemies;
    }

    private void PerformAttack(Transform enemy)
    {
        if (enemy == null) return;

        FaceTarget(enemy);
        PlayAttackAnimation();
        DealDamageToEnemy(enemy);
    }

    private void FaceTarget(Transform target)
    {
        Vector3 directionToTarget = target.position - transform.position;
        directionToTarget.y = 0f;
        
        if (directionToTarget.sqrMagnitude > 0.001f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(directionToTarget);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, lookRotation, turnSpeed * 100 * Time.deltaTime);
        }
    }

    private void PlayAttackAnimation()
    {
        if (animator != null)
        {
            // Trigger attack animation with proper timing
            animator.SetTrigger("AttackTrigger");
            StartCoroutine(ResetAttackTriggerAfterFrame());
        }
    }

    private void DealDamageToEnemy(Transform enemy)
    {
        NormalEnemyAgent enemyAgent = enemy.GetComponentInParent<NormalEnemyAgent>();
        RL_EnemyController enemyController = enemy.GetComponentInParent<RL_EnemyController>();

        if (enemyAgent != null)
        {
            enemyAgent.TakeDamage(attackDamage);
            
            // Add hit reaction if not dead
            if (!enemyAgent.IsDead)
            {
                enemyAgent.TriggerHitAnimation();
            }
        }
        else if (enemyController != null)
        {
            enemyController.TakeDamage((int)attackDamage);
        }
    }

    private IEnumerator ResetAttackTriggerAfterFrame()
    {
        yield return null;
        animator.ResetTrigger("AttackTrigger");
    }

    private IEnumerator InvincibilityRoutine()
    {
        isInvincible = true;
        yield return new WaitForSeconds(invincibilityDuration);
        isInvincible = false;
    }

    private void ResetPlayerState()
    {
        isAlive = true;
        currentHealth = maxHealth;
    }

    private void EnableColliders()
    {
        foreach (var collider in colliders)
        {
            if (collider != null)
                collider.enabled = true;
        }
    }

    private void ResetAnimationState()
    {
        if (animator != null)
        {
            animator.SetBool("isDead", false);
            animator.SetBool("isWalking", false);
            animator.SetBool("isIdle", true);
            animator.ResetTrigger("getHit");
        }
    }

    private void ReturnToSpawnPoint()
    {
        transform.position = initialPosition;
    }

    private void InitializeHealthBar()
    {
        if (healthBarSlider != null)
        {
            healthBarSlider.minValue = 0;
            healthBarSlider.maxValue = maxHealth;
            healthBarSlider.value = maxHealth;
        }
    }

    private void InitializeAnimationState()
    {
        if (animator != null)
        {
            animator.SetBool("isIdle", true);
            animator.SetBool("isWalking", false);
            animator.SetBool("AttackTrigger", false);
            animator.ResetTrigger("getHit");
            animator.SetBool("isDead", false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.2f);
    }
}