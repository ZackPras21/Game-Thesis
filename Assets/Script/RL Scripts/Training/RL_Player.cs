using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RL_Player : MonoBehaviour
{
    #region Singleton & Events
    public static RL_Player Instance;
    public static event System.Action OnPlayerDestroyed;
    #endregion

    #region Serialized Fields
    [Header("Training Configuration")]
    [SerializeField] public bool isRL_TrainingTarget = false;

    [Header("Combat Settings")]
    [SerializeField] private float maxHealth = 100f;
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
    #endregion

    #region Properties
    public float CurrentHealth => currentHealth;
    #endregion

    #region Private Fields
    private float currentHealth;
    private bool isInvincible = false;
    private bool isAlive = true;
    private bool attackEnabled = true;
    private Vector3 initialPosition;
    private Collider[] colliders;
    private Coroutine attackCoroutine;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        Instance = this;
        initialPosition = transform.position;
        colliders = GetComponentsInChildren<Collider>();
        animator = animator ?? GetComponent<Animator>();
    }

    private void Start() => InitializePlayer();
    #endregion

    #region Public Methods
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

        Die();
        return true;
    }

    public void SetAutoAttackEnabled(bool enabled)
    {
        attackEnabled = enabled;
        if (enabled && attackCoroutine == null && isAlive)
            attackCoroutine = StartCoroutine(AutomaticAttackRoutine());
        else if (!enabled && attackCoroutine != null)
            StopAttackCoroutine();
            
    }

    public void Respawn()
    {
        isAlive = true;
        currentHealth = maxHealth;
        transform.position = initialPosition;
        
        SetCollidersEnabled(true);
        UpdateHealthBar();
        ResetAnimationState();
        
        if (attackEnabled)
            SetAutoAttackEnabled(true);
    }
    #endregion

    #region Private Methods - Initialization
    private void InitializePlayer()
    {
        currentHealth = maxHealth;
        InitializeHealthBar();
        ResetAnimationState();
        SetAutoAttackEnabled(true);
    }

    private void InitializeHealthBar()
    {
        if (healthBarSlider == null) return;
        
        healthBarSlider.minValue = 0;
        healthBarSlider.maxValue = maxHealth;
        healthBarSlider.value = maxHealth;
    }

    private void ResetAnimationState()
    {
        if (animator == null) return;
        animator.ResetTrigger("getHit");
        animator.SetBool("isIdle", true);
        animator.SetBool("isWalking", false);
        animator.SetBool("isDead", false);
        animator.SetBool("isAttacking", false);
        
        animator.Update(0f);
        
        StartCoroutine(DelayedAnimatorReady());
    }

    private IEnumerator DelayedAnimatorReady()
    {
        yield return new WaitForEndOfFrame();
    }
    #endregion

    #region Private Methods - Combat System
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
        StopAttackCoroutine();
        
        PlayAnimationBool("isDead", true);
        PlayParticleEffect(deathParticle);
        SetCollidersEnabled(false);
        
        GetComponent<RL_TrainingTarget>()?.ForceNotifyDestruction();
        OnPlayerDestroyed?.Invoke();
        Destroy(gameObject);
    }

    private void StopAttackCoroutine()
    {
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }
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
        
        // Start attack animation
        if (animator != null)
        {
            animator.SetBool("isAttacking", true);
            yield return new WaitForSeconds(0.3f); // Animation duration
            animator.SetBool("isAttacking", false);
        }
        
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
        const float rotationDuration = 0.3f;

        while (elapsedTime < rotationDuration)
        {
            elapsedTime += Time.deltaTime;
            transform.rotation = Quaternion.Lerp(startRotation, targetRotation, elapsedTime / rotationDuration);
            yield return null;
        }

        transform.rotation = targetRotation;
    }

    private void DealDamageToEnemy(Transform enemy)
    {
        var enemyController = enemy.GetComponentInParent<RL_EnemyController>();
        var enemyAgent = enemy.GetComponentInParent<NormalEnemyAgent>();

        if (enemyController != null)
            enemyController.TakeDamage((int)attackDamage);
        else if (enemyAgent != null)
            enemyController?.TakeDamage((int)attackDamage);
    }

    private IEnumerator InvincibilityRoutine()
    {
        isInvincible = true;
        yield return new WaitForSeconds(invincibilityDuration);
        isInvincible = false;
    }
    #endregion

    #region Private Methods - Animation & Effects
    private void PlayAnimation(string parameterName, bool isTrigger, bool value = false)
    {
        if (animator == null) return;

        if (isTrigger)
            animator.SetTrigger(parameterName);
        else
            animator.SetBool(parameterName, value);
    }

    private void PlayAnimationBool(string parameterName, bool value)
    {
        if (animator != null)
            animator.SetBool(parameterName, value);
    }

    private void PlayAnimationTrigger(string triggerName)
    {
        if (animator != null)
            animator.SetTrigger(triggerName);
    }

    private void PlayParticleEffect(ParticleSystem particle)
    {
        if (particle != null)
        {
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particle.Play();
        }
    }
    #endregion

    #region Private Methods - Utility
    private void UpdateHealthBar()
    {
        if (healthBarSlider != null)
            healthBarSlider.value = currentHealth;
    }

    private void SetCollidersEnabled(bool enabled)
    {
        foreach (var collider in colliders)
        {
            if (collider != null)
                collider.enabled = enabled;
        }
    }
    #endregion

    #region Debug
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.2f);
    }
    #endregion
}