using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine;
using System;
using Unity.VisualScripting;

public class PlayerController : MonoBehaviour
{
    GameManager gm;
    CharacterController controller; //Player Controller Game Object
    private Vector3 playerVelocity; // Kecepatan Movement Player
    private Vector3 move; // Variabel Wadah Input Untuk Movement
    private bool groundedPlayer; // Mengecek Player Grounded
    public PlayerData playerData; // Menampung Player Data Secara Temporary
    public WeaponData weaponData; // Menampung Weapon Data Secara Temporary
    private float gravityValue = -9.81f; // Gravitasi tanpa Rigidbody
    public PlayerState playerState; // State Untuk Mengatur Animasi Player
    public int Gear; // Menampung Gear Secara Temporary
    public int Data; // Menampung Data Secara Temporary
    public Animator Animator; // Animation Controller Pada Player
    public static PlayerController Instance; // Membuat Player Controller Menjadi Singleton Untuk Kelas Lain Dapat Mengakses Beberapa Fiturnya
    public ParticleSystem DashParticle;
    public ParticleSystem DeathParticle;
    public ParticleSystem HurtParticle;
    public ParticleSystem ParrySuccessParticle;
    public bool canAttack = true;
    public bool canParry = true;
    public bool CanSkillAttack = true;
    public bool canDash = true;
    public Transform ParticleSpawnPoint;
    public float velocity;
    [SerializeField]
    private float smoothInputSpeed = .1f;
    public Vector3 currentInputVector;
    private Vector3 smoothInputVelocity;
    public bool isAlive = true;
    private bool isEnemyDetected = false;
    private Transform enemyTransform = null;
    public float AssistRange;
    public int dashCount = 50;
    public int MaxHealth;
    public bool HasTeleported = false;
    public bool KILLPLAYER = false;
    public int SkillEnergy = 2;
    private void Awake()
    {
        if (Instance == null) Instance = this;
    }
    private void Start()
    {
        Data = 0;
        gm = GameManager.Instance;
        Gear = gm.LoadGear(); // Mengambil Data Gear dari Player Preference
        controller = GetComponent<CharacterController>();
        playerState = PlayerState.Idle;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        MaxHealth = playerData.playerHealth;
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Enemy") && other.gameObject.layer == LayerMask.NameToLayer("Default") && !isEnemyDetected && enemyTransform == null)
        {
            enemyTransform = other.gameObject.GetComponent<Transform>();
            isEnemyDetected = true;
        }
    }
    private void Update()
    {
        if (KILLPLAYER && playerData.playerHealth != 0)
        {
            playerData.playerHealth = 0;
        }
        playerVelocity.y += gravityValue * Time.deltaTime;
        controller.Move(playerVelocity * Time.deltaTime);

        // Debug.Log(dashCount);
        if (Input.GetKeyDown(KeyCode.E))
        {
            Cursor.visible = !Cursor.visible;
            if (Cursor.visible)
                Cursor.lockState = CursorLockMode.None;
            else
                Cursor.lockState = CursorLockMode.Locked;
        }
        Animator.SetBool("IsAlive", isAlive);
        if (isAlive && playerState != PlayerState.Interact && HasTeleported)
        {
            CombatAssists();
            move = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
            currentInputVector = Vector3.SmoothDamp(currentInputVector, move, ref smoothInputVelocity, smoothInputSpeed);

            if (playerState != PlayerState.Parry && playerState != PlayerState.SkillAttack && playerState != PlayerState.Hurt)
            {
                velocity = Mathf.Clamp01(Mathf.Abs(move.x) + Mathf.Abs(move.z));
                Animator.SetFloat("Speed", velocity);
                if (playerState != PlayerState.Attack1 && playerState != PlayerState.Attack2 && playerState != PlayerState.Attack3 && playerState != PlayerState.DashAttack && playerState != PlayerState.Dash)
                {
                    Movement();
                }
            }
            if (Input.GetKeyDown(KeyCode.LeftShift) && canDash && enemyTransform == null && !isEnemyDetected && playerState != PlayerState.SkillAttack)
            {
                Animator.SetTrigger("Dash");
                StartCoroutine(Dash());
                canDash = false;
            }
            if (Input.GetMouseButtonDown(0) && canAttack && playerState != PlayerState.Parry && !Cursor.visible)
            {
                StartCoroutine(Attack());
                canAttack = false;
                StartCoroutine(ResetAttack());
            }
            if (Input.GetKeyDown(KeyCode.Space) && CanSkillAttack && playerState != PlayerState.SkillAttack && SkillEnergy >= 2)
            {
                SkillAttack();
                SkillEnergy -= 2;
                CanSkillAttack = false;
            }
            if (Input.GetMouseButtonDown(1) && canParry && !Cursor.visible)
            {
                Animator.SetBool("Parry", true);
            }
            if (Input.GetMouseButtonUp(1) && !Cursor.visible)
            {
                Animator.SetBool("Parry", false);
            }
            if (playerData.playerHealth <= 0 && isAlive)
            {
                OnDeath();
            }
        }
    }
    #region Death
    private void OnDeath()
    {
        GameTimer.Instance.Stop();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        isAlive = false;
        Animator.SetTrigger("Death");
        Destroy(gameObject,6);
        // StartCoroutine(ReloadLevel());
    }
    private IEnumerator ReloadLevel()
    {
        yield return new WaitForSeconds(5f);
        AudioManager.instance.ResetPlayerController();
        SceneManager.LoadScene("NewLevelOne");
    }
    #endregion
    #region Combat Assists
    private void CombatAssists()
    {
        if (isEnemyDetected == true && enemyTransform != null)
        {
            if (enemyTransform.gameObject.GetComponent<EnemyController>().enemyHP <= 0)
            {
                enemyTransform = null;
                isEnemyDetected = false;
            }
            else
            {
                Vector3 direction = enemyTransform.position - transform.position;
                direction.y = 0;
                if (direction.magnitude <= AssistRange)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10);
                }
                else
                {
                    enemyTransform = null;
                    isEnemyDetected = false;
                }

            }
        }
    }
    #endregion
    #region Combat 
    private IEnumerator Attack()
    {
        Animator.SetTrigger("Attack");
        yield return new WaitForSeconds(0.2f);
    }
    private IEnumerator ResetAttack()
    {
        yield return new WaitForSeconds(0.2f);
        canAttack = true;
    }
    private IEnumerator ResetParry()
    {
        yield return new WaitForSeconds(2f);
        canParry = true;
    }
    private void SkillAttack()
    {
        Animator.SetTrigger("Skill");
    }
    #endregion
    #region Movement
    private void Movement()
    {
        groundedPlayer = controller.isGrounded;
        if (groundedPlayer && playerVelocity.y < 0) playerVelocity.y = 0f;

        if (move != Vector3.zero)
        {
            controller.Move(currentInputVector * Time.deltaTime * playerData.playerSpeed);
            Quaternion toRotation = Quaternion.LookRotation(move, Vector3.up);
            if (enemyTransform == null && !isEnemyDetected)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, toRotation, Time.deltaTime * 10);
            }
            playerState = PlayerState.Run;
        }
        else if (playerState != PlayerState.Attack1 && playerState != PlayerState.Attack2 && playerState != PlayerState.Attack3 && playerState != PlayerState.Dash)
        {
            playerState = PlayerState.Idle;
        }



    }
    IEnumerator DashDelay()
    {
        float startTime = Time.time;
        while (Time.time < startTime + 2)
        {
            float elapsedTime = Time.time - startTime;
            dashCount = Mathf.Clamp((int)(elapsedTime / 2 * 50), 0, 50);

            yield return null;
        }
        canDash = true;
    }
    IEnumerator Dash()
    {
        float startTime = Time.time;
        Instantiate(DashParticle, ParticleSpawnPoint.position, Quaternion.Euler(gameObject.transform.rotation.eulerAngles.x, gameObject.transform.rotation.eulerAngles.y + 180, gameObject.transform.rotation.eulerAngles.z), gameObject.transform);
        while (Time.time < startTime + playerData.playerDashTime)
        {
            dashCount = 0;
            if (enemyTransform == null && !isEnemyDetected)
            {

                controller.Move(playerData.playerDashSpeed * Time.deltaTime * transform.forward);
                yield return null;
            }
            else
            {
                StartCoroutine(DashDelay());
                yield break;
            }
        }
        StartCoroutine(DashDelay());
    }
    #endregion
    #region Hurt
    public void DamagePlayer(int damage, Func<IEnumerator> knockback, Vector3 position)
    {
        Vector3 directionToEnemy = position - transform.position;

        float angle = Vector3.Angle(transform.forward, directionToEnemy);

        float thresholdAngle = 90f;
        if (angle < thresholdAngle && playerState == PlayerState.Parry)
        {
            Animator.SetTrigger("ParrySuccess");
            canParry = false;
            ParrySuccessParticle.Play();
            if(knockback != null)
            StartCoroutine(knockback());
            StartCoroutine(ResetParry());
        }
        else
        {
            playerData.playerHealth -= damage;
            if (isAlive && !(playerState == PlayerState.SkillAttack || playerState == PlayerState.Dash || playerState == PlayerState.Attack1 || playerState == PlayerState.Attack2 || playerState == PlayerState.Attack3))
            {
                Animator.SetTrigger("Hurt");
            }
        }
    }
    #endregion
    #region Resource
    public void AddDataResource()
    {
        Data++;
        SkillEnergy++;
        // Debug.Log(Data);
    }
    public void AddGearResource()
    {
        Gear++;
        SkillEnergy++;
        // Debug.Log(Gear);
    }
    #endregion
    public void AddHealth()
    {
        playerData.playerHealth += 10;
        SkillEnergy++;
        if (playerData.playerHealth > MaxHealth)
        {
            playerData.playerHealth = MaxHealth;
        }
    }
}