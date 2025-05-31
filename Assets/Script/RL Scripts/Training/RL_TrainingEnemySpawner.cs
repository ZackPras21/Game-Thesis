using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RL_TrainingEnemySpawner : MonoBehaviour
{
    public GameObject[] enemyPrefab;
    public GameObject corner1;
    public GameObject corner2;
    public GameObject corner3;
    public GameObject corner4;
    public Transform[] waypoints;
    private int enemyCount = 0;
    public int maxEnemyCount = 10;
    public float spawnTime = 0.5f;
    private bool spawnTriggered = false;
    private CameraFollow cameraFollow;
    private GameProgression gameProgression;
    public GameObject Gate;
    private void Start()
    {
        gameProgression = GameProgression.Instance;
        cameraFollow = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<CameraFollow>();
        cameraFollow.CombatMode();
        AudioManager.instance.PlaySFX(AudioManager.instance.gateClose);
        StartCoroutine(SpawnEnemy());
        spawnTriggered = true;
    }

    [Header("Spawn Settings")]
    public bool spawnBeforeTargeting = true;
    public Transform playerTarget;

    IEnumerator SpawnEnemy()
    {
        while (enemyCount < maxEnemyCount)
        {
            float spawnChance = (float)gameProgression.EnemyTotalSpawnCount / gameProgression.EnemyTotalCount;
            float randomValue = Random.value;

            GameObject enemyToSpawn = GetEnemyToSpawn(randomValue, spawnChance);
            Vector3 spawnPosition = GetRandomPosition();
            GameObject enemy = Instantiate(enemyToSpawn, spawnPosition, Quaternion.identity);

            if (spawnBeforeTargeting && playerTarget != null)
            {
                enemy.GetComponent<RL_EnemyController>().SetTarget(playerTarget);
            }

            // Safely assign waypoints
            if (waypoints != null && waypoints.Length > 0)
            {
                var enemyController = enemy.GetComponent<RL_EnemyController>();
                if (enemyController != null)
                {
                    enemyController.waypoints = new Transform[waypoints.Length];
                    for (int i = 0; i < waypoints.Length; i++)
                    {
                        enemyController.waypoints[i] = waypoints[i];
                    }
                }
            }
            else
            {
                Debug.LogError("No waypoints assigned to RL_TrainingEnemySpawner!");
            }

            yield return new WaitForSeconds(spawnTime);
            enemyCount++;
            gameProgression.EnemySpawn();
        }
    }

    GameObject GetEnemyToSpawn(float randomValue, float spawnChance)
    {
        if (randomValue < 0.8f * spawnChance)
            return enemyPrefab[2];
        if (randomValue < spawnChance)
            return enemyPrefab[1];
        return enemyPrefab[0];
    }
    Vector3 GetRandomPosition()
    {
        if (corner1 == null || corner2 == null)
        {
            // Debug.Log("One or both corners are not assigned.");
            return Vector3.zero; // Or any default position you want to return in case of error.
        }
        
        Vector3 minCorner = Vector3.Min(corner1.transform.position, corner2.transform.position);
        Vector3 maxCorner = Vector3.Max(corner1.transform.position, corner2.transform.position);
        
        int maxAttempts = 10;
        for (int i = 0; i < maxAttempts; i++)
        {
            float randomX = Random.Range(minCorner.x, maxCorner.x);
            float randomZ = Random.Range(minCorner.z, maxCorner.z);
            Vector3 testPos = new Vector3(randomX, 1f, randomZ);
            
            // Check if position is clear of obstacles
            if (!Physics.CheckSphere(testPos, 0.5f, LayerMask.GetMask("Obstacle")))
            {
                return testPos;
            }
        }
        
        // If no valid position found after max attempts
        return Vector3.zero;
    }
}
