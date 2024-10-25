using UnityEngine;
using System.Collections;
using UnityEngine.AI;
using UnityEngine.UI;

public enum Boss3State
{
    NotSpawned,
    Chase,
    Attack,
    Idle,
    Skill,
    Defeated
}

public class Boss3Controller : MonoBehaviour
{
    public string playerTag = "Player";
    // public GameObject canvasHealthBar;
    // public Canvas canvas;
    // public Slider healthBar;
    // P_Hud_Manager hudScript;
    public static Boss3Controller Instance;
    public float attackRange = 3f;
    private float normalSpeed;
    private float skillCooldown = 5.0f;
    private float skillDuration = 1.6f;
    private float phaseDuration = 1.5f; // Durasi tiap attack phase
    private float idleDuration = 2f; // Durasi idle sebelum kembali ke chase
    private float bossHP;
    public GameObject stompPrefab; // Prefab damage stomp
    public GameObject stompWarningPrefab; // Prefab warning stomp
    public GameObject chargePrefab; // Prefab damage charge
    public GameObject tailPrefab; // Prefab damage spin
    public GameObject skillPrefab; // Prefab damage skill

    public Collider leftHand; // Collider tangan kiri (untuk dinonaktifkan saat ingin stomp)
    public Collider rightHand; // Collider tangan kanan

    private Animator animator;
    private Transform player;
    private NavMeshAgent agent;
    public Boss3State currentState;
    private float phaseTimer = 0f; // Untuk perhitungan waktu tiap phase
    private int currentPhase = 1; // Phase attack, defaultnya 1
    private bool attacking = false; // Untuk mencegah attack berulang
    private bool canInterrupted = false; // Mengatur apakah animasi hurt dapat dijalankan

    public bool isDefeated = false;

    public bool triggerBossSpawn = true; //Ganti jadi false kalo mau diimplement beneran
    private AudioManager audioManager;
    private void Awake()
    {
        if (Instance == null) Instance = this;
        audioManager = AudioManager.instance;
    }
    void Start()
    {
        animator = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
        // hudScript = canvasHealthBar.GetComponent<P_Hud_Manager>();

        normalSpeed = BossEnemyData.Instance.enemySpeed;
        agent.speed = normalSpeed;
        FindPlayer();
        phaseTimer = 0f;

        bossHP = BossEnemyData.Instance.enemyHealth;
        // Debug.Log("Initial boss HP: " + bossHP);
    }

    void Update()
    {
        // Debug.Log("Current state: " + currentState);
        switch (currentState)
        {
            case Boss3State.NotSpawned:
                mapCheck();
                break;
            case Boss3State.Chase:
                agent.speed = normalSpeed;
                ChasePlayer();
                break;
            case Boss3State.Attack:
                Attack();
                break;
            case Boss3State.Idle:
                Idle();
                break;
            case Boss3State.Skill:
                UseSkill();
                break;
            case Boss3State.Defeated:
                Defeat();
                break;
        }
    }

    void FindPlayer()
    {
        if (PlayerController.Instance.isAlive)
        {
            player = GameObject.FindGameObjectWithTag(playerTag).transform;
            // Debug.Log("Player found: " + player.name);
        }
    }

    public void TakeDamage(int damageAmount)
    {
        bossHP -= damageAmount;

        // hudScript.updateBossHealth(bossHP);

        if (bossHP > 0)
        {
            // Debug.Log("Boss HP: " + bossHP);

            if (canInterrupted) // Menjalankan animasi hurt di kondisi tertentu
            {
                animator.SetTrigger("getHit");
                audioManager.PlayBossHurtSound();
                if (currentState == Boss3State.Attack)
                {
                    phaseTimer = 0f;
                    currentPhase = 1;
                    currentState = Boss3State.Idle;
                    attacking = false;
                    animator.SetTrigger("backToIdle");
                }
                else if (currentState == Boss3State.Idle)
                {
                    animator.SetTrigger("backToIdle");
                }
            }
            // vfxManager.EnemyGettingHit(positionParticles);
        }
        else
        {
            Defeat();
            GetComponentInChildren<Collider>().enabled = false;
            // hudScript.bossActive(false);
        }
    }

    void ChasePlayer()
    {
        if (player == null)
        {
            FindPlayer();
            return;
        }
        audioManager.PlayBossMovement();

        agent.speed = normalSpeed;

        phaseTimer += Time.deltaTime;

        Vector3 targetPosition = player.position + (transform.position - player.position).normalized * (attackRange - 0.2f);
        agent.SetDestination(targetPosition);

        // attack jika player berada dalam jarak attack
        if (Vector3.Distance(transform.position, player.position) <= attackRange)
        {
            currentState = Boss3State.Attack;
            Stomp(stompWarningPrefab, 1.4f);
            phaseTimer = 0f;
        }

        // melakukan skill jika terlalu lama chase player
        if (phaseTimer >= skillCooldown)
        {
            currentState = Boss3State.Skill;
            phaseTimer = 0f;
        }
    }

    void Attack()
    {
        if (currentPhase == 1) // Attack stomp
        {
            animator.SetInteger("attPhase", 1);

            // Menonaktifkan collider tangan sebelum stomp
            if (rightHand != null)
            {
                rightHand.enabled = false;
            }
            if (leftHand != null)
            {
                leftHand.enabled = false;
            }

            if (!attacking)
            {
                StartCoroutine(PerformStomp());
            }
        }

        else if (currentPhase == 2) // attack charge
        {
            animator.SetInteger("attPhase", 2);
            if (!attacking)
            {
                StartCoroutine(PerformCharge());
            }
        }

        else if (currentPhase == 3) // attack spin
        {
            animator.SetInteger("attPhase", 3);
            if (!attacking)
            {
                StartCoroutine(PerformSpin());
            }
        }

        phaseTimer += Time.deltaTime;

        // ganti attack phase
        if (phaseTimer >= phaseDuration)
        {
            phaseTimer = 0f;
            currentPhase++;

            attacking = false;
            if (currentPhase > 3)
            {
                currentPhase = 1;
                currentState = Boss3State.Idle;
            }
        }
    }


    void Idle()
    {
        if (player == null)
        {
            FindPlayer();
            return;
        }

        animator.SetTrigger("backToIdle");
        canInterrupted = true;
        phaseTimer += Time.deltaTime;

        // attack jika player berada dalam jarak attack
        if (Vector3.Distance(transform.position, player.position) <= attackRange && phaseTimer >= 1.8f)
        {
            currentState = Boss3State.Attack;
            Stomp(stompWarningPrefab, 1.4f);
            phaseTimer = 0f;
        }

        // Masuk ke chase bila sudah selesai durasi idle
        if (phaseTimer >= idleDuration)
        {
            canInterrupted = false;
            // agent.isStopped = false;
            animator.SetBool("isWalking", true);
            currentState = Boss3State.Chase;
            phaseTimer = 0f;
        }
    }
    bool isRoll = false;

    void UseSkill()
    {
        animator.SetBool("isRoll", true);
        phaseTimer += Time.deltaTime;

        // kembali ke chase setelah selesai skill
        if (phaseTimer >= skillDuration + 0.4f)
        {
            animator.SetBool("isRoll", false);
            isRoll = false;
            agent.speed = normalSpeed;
            currentState = Boss3State.Chase;
            phaseTimer = 0f;
            return;
        }

        // masuk mode rolling
        if (phaseTimer >= 0.2f && phaseTimer <= skillDuration + 0.2f)
        {
            Rolling();
        }
    }
    void Rolling()
    {
        if (player == null)
        {
            FindPlayer();
            return;
        }
        agent.speed = normalSpeed * 3f;
        Vector3 targetPosition = player.position;
        agent.SetDestination(targetPosition);
        if (!isRoll)
        {
            audioManager.PlayBossSound(audioManager.bossUltimateAttackClip);
            isRoll = true;
        }
        // bertabrakan dengan player
        if (Vector3.Distance(transform.position, player.position) <= attackRange)
        {
            isRoll = false;
            skillHit();
            phaseTimer = skillDuration + 0.4f;
            phaseTimer = 0f;
            // animator.SetBool("isRoll", false);
            currentState = Boss3State.Attack;
        }
    }

    void skillHit()
    {
        float yOffset = 3f;
        Quaternion spawnRotation = transform.rotation;

        GameObject skill = Instantiate(skillPrefab, transform.position + transform.forward * yOffset, spawnRotation);

        Destroy(skill, 0.25f);
    }

    private IEnumerator PerformStomp()
    {
        attacking = true;
        yield return new WaitForSeconds(1.4f);
        rightHand.enabled = true;
        leftHand.enabled = true;
        Stomp(stompPrefab, 0.25f);
        audioManager.PlayBossSound(audioManager.bossGroundAttackAClip);
    }

    private IEnumerator PerformCharge()
    {
        attacking = true;
        yield return new WaitForSeconds(0.5f);
        Charge(chargePrefab, 0.25f);
        canInterrupted = true;
    }

    private IEnumerator PerformSpin()
    {
        attacking = true;
        yield return new WaitForSeconds(1.3f);
        canInterrupted = false;
        Spin();
    }


    void Stomp(GameObject area, float duration)
    {
        float xOffset1 = -2.0f;
        float xOffset2 = 2.0f;

        GameObject stomp1 = Instantiate(area, transform.position + transform.forward * 1.7f + transform.right * xOffset1, Quaternion.identity);
        GameObject stomp2 = Instantiate(area, transform.position + transform.forward * 1.7f + transform.right * xOffset2, Quaternion.identity);

        Destroy(stomp1, duration);
        Destroy(stomp2, duration);
    }

    void Charge(GameObject area, float duration)
    {
        audioManager.PlayBossSound(audioManager.bossGroundAttackBClip);

        float yOffset = 2f;
        Quaternion spawnRotation = transform.rotation;

        GameObject charge = Instantiate(area, transform.position + transform.forward * yOffset, spawnRotation);

        Destroy(charge, duration);
    }

    void Spin()
    {
        audioManager.PlayBossSound(audioManager.bossSpinAttackClip);

        float yOffset = -3f;
        Quaternion spawnRotation = transform.rotation;

        GameObject tail = Instantiate(tailPrefab, transform.position + transform.forward * yOffset, spawnRotation);

        tail.transform.SetParent(transform);
        StartCoroutine(RotateTail(tail));

        Destroy(tail, 0.6f);
    }
    private IEnumerator RotateTail(GameObject tail)
    {
        float duration = 0.6f;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            if (tail == null) break;
            float rotationSpeed = -720f;
            tail.transform.RotateAround(transform.position, Vector3.up, rotationSpeed * Time.deltaTime);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }

    public void Defeat()
    {
        if (!isDefeated)
            StartCoroutine(BossDead());
    }

    private IEnumerator BossDead()
    {
        isDefeated = true;
        animator.SetTrigger("getHit");
        animator.SetTrigger("dead");
        audioManager.PlayBossSound(audioManager.bossDeathClip);
        agent.isStopped = true;
        currentState = Boss3State.Defeated;
        yield return new WaitForSeconds(5f);
        GameProgression.Instance.IsBossDead = true;
        Destroy(gameObject, 5f);
        yield return null;
    }

    public void mapCheck()
    {
        if (triggerBossSpawn)
        {
            currentState = Boss3State.Idle;
            phaseTimer = 0f;
            // hudScript.bossActive(true);
            // hudScript.SetMaxBossHealth(bossHP);
        }
    }

    public float getBossHP()
    {
        return bossHP;
    }
}
