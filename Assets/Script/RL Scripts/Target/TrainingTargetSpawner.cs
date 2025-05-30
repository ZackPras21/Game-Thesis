using UnityEngine;

public class TrainingTargetSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject trainingTargetPrefab;
    public int maxTargets = 1;
    public float spawnRadius = 10f;
    public float spawnInterval = 2f;
    public LayerMask spawnCollisionLayers;

    private int currentTargetCount = 0;
    private float lastSpawnTime;

    void Start()
    {
        lastSpawnTime = Time.time;
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
            GameObject target = Instantiate(trainingTargetPrefab, spawnPos, Quaternion.identity);
            var targetScript = target.GetComponent<RL_Player>();
            targetScript.isTrainingTarget = true;
            currentTargetCount++;
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

    public void TargetDestroyed()
    {
        currentTargetCount--;
    }
}