using UnityEngine;
using UnityEngine.AI;

public enum Boss1State
{
    NotSpawned,
    Chase,
    Attack,
    Idle,
    Skill,
    Defeated
}

public class Boss1Controller : MonoBehaviour
{
    public string playerTag = "Player";
    public float attackRange = 2f;
    public float skillCooldown = 5f;
    public float phaseDuration = 1f;
    public GameObject cubePrefab;

    private Transform player; 
    private NavMeshAgent agent; 
    private Boss1State currentState = Boss1State.NotSpawned;
    private float skillTimer = 0f;
    private float phaseTimer = 0f;
    private int currentPhase = 1;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        FindPlayer();
    }

    void Update()
    {
        switch (currentState)
        {
            case Boss1State.NotSpawned:
                break;
            case Boss1State.Chase:
                ChasePlayer();
                break;
            case Boss1State.Attack:
                Attack();
                break;
            case Boss1State.Idle:
                Idle();
                break;
            case Boss1State.Skill:
                UseSkill();
                break;
            case Boss1State.Defeated:
                // Defeated
                break;
        }
    }

    void FindPlayer()
    {
        player = GameObject.FindGameObjectWithTag(playerTag).transform;
    }

    void ChasePlayer()
    {
        if (player == null)
        {
            FindPlayer();
            return;
        }

        agent.SetDestination(player.position);

        if (Vector3.Distance(transform.position, player.position) <= attackRange)
        {
            currentState = Boss1State.Attack;
        }
    }

    void Attack()
    {
        if (currentPhase == 1)
        {
            SpawnCube(true);
            // Logic
        }
        else if (currentPhase == 2)
        {
            SpawnCube(false);
            // Logic
        }
        else if (currentPhase == 3)
        {
            // Charge
        }

        phaseTimer += Time.deltaTime;
        if (phaseTimer >= phaseDuration)
        {
            phaseTimer = 0f;
            currentPhase++;
            if (currentPhase > 3)
            {
                currentPhase = 1;
                currentState = Boss1State.Idle;
            }
        }
    }

    void Idle()
    {
        phaseTimer += Time.deltaTime;
        if (phaseTimer >= 3f)
        {
            phaseTimer = 0f;
            currentState = Boss1State.Chase;
        }
    }

    void UseSkill()
    {
        if (skillTimer <= 0)
        {
            // Skill
            skillTimer = skillCooldown;
        }
        else
        {
            skillTimer -= Time.deltaTime;
        }
    }

    void SpawnCube(bool isLeftHand)
    {
        float xOffset = isLeftHand ? -0.5f : 0.5f;

        GameObject cube = Instantiate(cubePrefab, transform.position + transform.forward * 2f + transform.right * xOffset, Quaternion.identity);
        Destroy(cube, 1f); // Destroy the cube after 1 second
    }

    public void Spawn()
    {
        currentState = Boss1State.Chase;
    }

    public void Defeat()
    {
        currentState = Boss1State.Defeated;
        agent.isStopped = true; 
    }
}
