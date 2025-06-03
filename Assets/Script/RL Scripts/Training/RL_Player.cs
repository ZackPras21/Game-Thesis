using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class RL_Player : MonoBehaviour
{
    #region Singleton
    public static RL_Player Instance;
    private void Awake()
    {
        // Standard singleton pattern: only one RL_Player can exist at a time.
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Cache all the child colliders once
        colliders = GetComponentsInChildren<Collider>();

        // Initialize health immediately
        currentHealth = maxHealth;
    }
    #endregion

    [Header("Training Target")]
    [Tooltip("Set to true if this RL_Player is being used as a training target")]
    public bool isTrainingTarget = false;
    
    private Vector3 playerVelocity;
    private bool groundedPlayer;
    private float gravityValue = -9.81f;

    [Header("Dash Settings")]
    [Tooltip("How long a dash lasts (in seconds).")]
    [SerializeField] private float playerDashTime = 0.3f;
    [Tooltip("How fast the dash moves the player.")]
    [SerializeField] private float playerDashSpeed = 20f;
    [Tooltip("Cooldown (in seconds) before you can dash again.")]
    [SerializeField] private float dashCooldown = 2f;
    [Tooltip("Particle system prefab to instantiate when performing a dash.")]
    [SerializeField] private ParticleSystem DashParticle;
    [Tooltip("Transform used as the spawn point for the DashParticle.")]
    [SerializeField] private Transform ParticleSpawnPoint;

    private bool canDash = true;

    [Header("Player Stats (Health)")]
    [Tooltip("Maximum health for the player.")]
    [SerializeField] private float maxHealth = 100f;
    // Internally track current health
    private float currentHealth;
    public float CurrentHealth => currentHealth;

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


        if (animator == null)
        {
            Debug.LogWarning("[RL_Player] Animator is not assigned. GetHit/Death animations won't play.");
        }
        else
        {
            // Reset all Animator parameters to Idle at start
            animator.SetBool("isIdle", true);
            animator.SetBool("isWalking", false);
            animator.SetBool("isAttacking", false);
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
        if (DashParticle == null)
            Debug.LogWarning("[RL_Player] DashParticle is not assigned. No dash VFX will appear.");
        if (hurtParticle == null)
            Debug.LogWarning("[RL_Player] HurtParticle is not assigned. No hurt VFX will appear.");
        if (deathParticle == null)
            Debug.LogWarning("[RL_Player] DeathParticle is not assigned. No death VFX will appear.");
    }

    private void Update()
    {
        if (!isAlive)
        {
            // If dead, do not process movement or dash
            return;
        }

        // 1) Standard movement (WASD / arrow keys)

        Vector3 move = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
        if (move.magnitude > 0.1f)
        {
            // Walk Animation
            if (animator != null)
            {
                animator.SetBool("isWalking", true);
                animator.SetBool("isIdle", false);
            }
            // Move the character
        }
        else
        {
            // Idle Animation
            if (animator != null)
            {
                animator.SetBool("isWalking", false);
                animator.SetBool("isIdle", true);
            }
        }

        // Apply gravity
        playerVelocity.y += gravityValue * Time.deltaTime;

        // 2) Dash Input (Left Shift)
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash)
        {
            StartCoroutine(Dash());
        }
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

        // (Optional) Disable any movement or other scripts here,
        // or call a GameOver manager, etc.
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

    private IEnumerator Dash()
    {
        canDash = false;
        float dashStartTime = Time.time;

        // Instantiate the dash particle effect exactly once at the start
        if (DashParticle != null && ParticleSpawnPoint != null)
        {
            Instantiate(DashParticle, ParticleSpawnPoint.position, Quaternion.identity);
        }

        // Until playerDashTime has elapsed, move forward at dash speed
        while (Time.time < dashStartTime + playerDashTime)
        {
            // Note: We do not adjust y here, so gravity still applies if you hold forward in the air.
            Vector3 forward = transform.forward;
            yield return null;
        }

        // After finishing the dash, start cooldown
        StartCoroutine(DashCooldownRoutine());
    }

    private IEnumerator DashCooldownRoutine()
    {
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    private void OnDrawGizmosSelected()
    {
        // Optionally visualize dash direction or attack range here:
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.2f); // small marker
    }
}
