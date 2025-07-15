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
    [SerializeField] private float attackInterval = 2.0f;
    [SerializeField] private float attackDamage = 80f;
    [SerializeField] private float attackRange = 5f;
    [SerializeField] private float invincibilityDuration = 0.5f;

    [Header("UI Components")]
    [SerializeField] private Slider healthBarSlider;

    [Header("Animation & Effects")]
    [SerializeField] private Animator animator;
    [SerializeField] private ParticleSystem hurtParticle;
    [SerializeField] private ParticleSystem deathParticle;

    public float CurrentHealth => currentHealth;

    private float currentHealth;
    private bool isInvincible = false;
    private bool isAlive = true;
    private bool attackEnabled = true;
    private Vector3 initialPosition;
    private Collider[] colliders;
    private Coroutine attackCoroutine;

    private void Awake()
    {
        Instance = this;
        initialPosition = transform.position;
        colliders = GetComponentsInChildren<Collider>();
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        currentHealth = maxHealth;
        InitializeHealthBar();
        InitializeAnimationState();
        SetAutoAttackEnabled(true);
    }

    public bool DamagePlayer(float damageAmount)
    {
        if (!CanTakeDamage()) return false;

        currentHealth = Mathf.Clamp(currentHealth - damageAmount, 0f, maxHealth);
        UpdateHealthBar();

        if (currentHealth > 0f)
        {
            HandleNonFatalDamage();
            return false;
        }
        else
        {
            Die();
            return true;
        }
    }

    public void SetAutoAttackEnabled(bool enabled)
    {
        attackEnabled = enabled;
        
        if (enabled && attackCoroutine == null)
        {
            attackCoroutine = StartCoroutine(AutomaticAttackRoutine());
        }
        else if (!enabled && attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }
    }

    public void Respawn()
    {
        isAlive = true;
        currentHealth = maxHealth;
        transform.position = initialPosition;
        
        SetCollidersEnabled(true);
        UpdateHealthBar();
        InitializeAnimationState();
        
        if (attackEnabled && attackCoroutine == null)
        {
            attackCoroutine = StartCoroutine(AutomaticAttackRoutine());
        }
    }

    private bool CanTakeDamage() => isAlive && !isInvincible;

    private void HandleNonFatalDamage()
    {
        PlayAnimationTrigger("getHit");
        PlayParticleEffect(hurtParticle);
        StartCoroutine(InvincibilityRoutine());
    }

    private void Die()
    {
        isAlive = false;
        attackEnabled = false;
        
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }
        
        PlayAnimationBool("isDead", true);
        PlayParticleEffect(deathParticle);
        SetCollidersEnabled(false);
        NotifyDestruction();
        OnPlayerDestroyed?.Invoke();
        Destroy(gameObject);
    }

    private void PlayAnimationTrigger(string triggerName)
    {
        if (animator != null)
            animator.SetTrigger(triggerName);
    }

    private void PlayAnimationBool(string parameterName, bool value)
    {
        if (animator != null)
            animator.SetBool(parameterName, value);
    }

    private void PlayParticleEffect(ParticleSystem particle)
    {
        if (particle != null)
        {
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particle.Play();
        }
    }

    private void SetCollidersEnabled(bool enabled)
    {
        foreach (var collider in colliders)
        {
            if (collider != null)
                collider.enabled = enabled;
        }
    }

    private void NotifyDestruction()
    {
        var target = GetComponent<RL_TrainingTarget>();
        target?.ForceNotifyDestruction();
    }

    private void UpdateHealthBar()
    {
        if (healthBarSlider != null)
            healthBarSlider.value = currentHealth;
    }

    private IEnumerator AutomaticAttackRoutine()
    {
        while (attackEnabled && isAlive)
        {
            yield return new WaitForSeconds(attackInterval);
            
            var enemiesInRange = FindEnemiesInRange();
            if (enemiesInRange.Count > 0)
                PerformAttack(enemiesInRange[0]);
        }
        attackCoroutine = null;
    }

    private List<Transform> FindEnemiesInRange()
    {
        var hits = Physics.OverlapSphere(transform.position, attackRange);
        var enemies = new List<Transform>();
        
        foreach (var hit in hits)
        {
            if (hit != null && hit.CompareTag("Enemy"))
                enemies.Add(hit.transform);
        }
        
        return enemies;
    }

    private void PerformAttack(Transform enemy)
    {
        if (enemy == null || !isAlive) return;

        StartCoroutine(AttackSequence(enemy));
    }

    private IEnumerator AttackSequence(Transform enemy)
    {
        if (enemy == null) yield break;

        FaceTarget(enemy);
        
        yield return new WaitForSeconds(0.1f);
        
        PlayAttackAnimation();
        
        yield return new WaitForSeconds(0.2f);
        
        if (enemy != null)
            DealDamageToEnemy(enemy);
    }

    private void FaceTarget(Transform target)
    {
        if (target == null) return;

        var directionToTarget = target.position - transform.position;
        directionToTarget.y = 0f;
        
        if (directionToTarget.sqrMagnitude > 0.001f)
        {
            var lookRotation = Quaternion.LookRotation(directionToTarget);
            StartCoroutine(RotateToTarget(lookRotation));
        }
    }

    private IEnumerator RotateToTarget(Quaternion targetRotation)
    {
        var startRotation = transform.rotation;
        var elapsedTime = 0f;
        var rotationDuration = 0.3f;

        while (elapsedTime < rotationDuration)
        {
            elapsedTime += Time.deltaTime;
            var t = elapsedTime / rotationDuration;
            transform.rotation = Quaternion.Lerp(startRotation, targetRotation, t);
            yield return null;
        }

        transform.rotation = targetRotation;
    }

    private void PlayAttackAnimation()
    {
        if (animator != null)
        {
            animator.SetTrigger("AttackTrigger");
            StartCoroutine(ResetAttackTriggerAfterFrame());
        }
    }

    private void DealDamageToEnemy(Transform enemy)
    {
        var enemyAgent = enemy.GetComponentInParent<NormalEnemyAgent>();
        var enemyController = enemy.GetComponentInParent<RL_EnemyController>();

        if (enemyAgent != null)
            enemyAgent.TakeDamage(attackDamage);
        else if (enemyController != null)
            enemyController.TakeDamage((int)attackDamage);
    }

    private IEnumerator ResetAttackTriggerAfterFrame()
    {
        yield return null;
        if (animator != null)
            animator.ResetTrigger("AttackTrigger");
    }

    private IEnumerator InvincibilityRoutine()
    {
        isInvincible = true;
        yield return new WaitForSeconds(invincibilityDuration);
        isInvincible = false;
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
            animator.SetBool("isDead", false);
            animator.ResetTrigger("getHit");
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.2f);
    }
}