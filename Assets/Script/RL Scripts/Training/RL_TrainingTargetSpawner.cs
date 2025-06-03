using UnityEngine;

public class RL_TrainingTargetSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject trainingTargetPrefab;
    public int maxTargets = 1;
    public float spawnRadius = 10f;
    public float spawnInterval = 2f;
    public LayerMask spawnCollisionLayers;

    [Header("Visual Cues")]
    public ParticleSystem spawnParticle;
    public ParticleSystem episodeEndParticle;
    public Light arenaLight;
    public Color activeColor = Color.green;
    public Color inactiveColor = Color.red;

    private int currentTargetCount = 0;
    private float lastSpawnTime;

    void Start()
    {
        lastSpawnTime = Time.time;
        UpdateArenaVisuals();
    }

    void Update()
    {
        if (currentTargetCount < maxTargets && Time.time > lastSpawnTime + spawnInterval)
        {
            SpawnTarget();
            lastSpawnTime = Time.time;
        }
    }

    void SpawnTarget()
    {
        Vector3 spawnPos = GetValidSpawnPosition();
        if (spawnPos != Vector3.zero)
        {
            // Create new target
            GameObject newTarget = Instantiate(trainingTargetPrefab, spawnPos, Quaternion.identity);
            var targetPlayer = newTarget.GetComponent<RL_Player>();
            targetPlayer.isTrainingTarget = true;
            
            // Subscribe to destruction event
            System.Action destroyCallback = null;
            destroyCallback = () => {
                RL_Player.OnPlayerDestroyed -= destroyCallback;
                currentTargetCount--;
                UpdateArenaVisuals();
                
                // Play episode end effect if no targets left
                if (currentTargetCount <= 0 && episodeEndParticle != null)
                {
                    episodeEndParticle.Play();
                }
                
                // Respawn if below max targets
                if (currentTargetCount < maxTargets)
                {
                    Invoke("SpawnTarget", spawnInterval);
                }
            };
            RL_Player.OnPlayerDestroyed += destroyCallback;

            currentTargetCount++;
            UpdateArenaVisuals();
            
            // Play spawn effect
            if (spawnParticle != null)
            {
                Instantiate(spawnParticle, spawnPos, Quaternion.identity);
            }
        }
    }

    Vector3 GetValidSpawnPosition()
    {
        for (int i = 0; i < 10; i++)
        {
            Vector3 randomPos = transform.position + Random.insideUnitSphere * spawnRadius;
            randomPos.y = transform.position.y;
            if (!Physics.CheckSphere(randomPos, 1f, spawnCollisionLayers))
            {
                return randomPos;
            }
        }
        return Vector3.zero;
    }

    void UpdateArenaVisuals()
    {
        if (arenaLight != null)
        {
            arenaLight.color = currentTargetCount > 0 ? activeColor : inactiveColor;
        }
    }

    public void TargetDestroyed()
    {
        currentTargetCount--;
        UpdateArenaVisuals();
    }
}