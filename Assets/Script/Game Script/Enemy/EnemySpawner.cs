using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public GameObject[] enemyPrefab;
    public GameObject corner1;
    public GameObject corner2;
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
    }
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && other.gameObject.layer == LayerMask.NameToLayer("Hitbox"))
        {
            cameraFollow.CombatMode();
            if (!spawnTriggered)
            {
                AudioManager.instance.PlaySFX(AudioManager.instance.gateClose);
                StartCoroutine(SpawnEnemy());
                //Gate.GetComponent<GateInteraction>().CloseGate();
                spawnTriggered = true;
            }
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && other.gameObject.layer == LayerMask.NameToLayer("Hitbox"))
        {
            cameraFollow.NormalMode();
        }
    }

    IEnumerator SpawnEnemy()
    {
        while (enemyCount < maxEnemyCount)
        {
            float spawnChance = (float)gameProgression.EnemyTotalSpawnCount / gameProgression.EnemyTotalCount;
            float randomValue = Random.value;

            GameObject enemyToSpawn;

            if (randomValue < 0.5f * spawnChance)
            {
                enemyToSpawn = enemyPrefab[2]; // Medium enemy
            }
            else if (randomValue < spawnChance)
            {
                enemyToSpawn = enemyPrefab[1]; // Normal enemy
            }
            else
            {
                enemyToSpawn = enemyPrefab[0]; // Creep enemy
            }

            Vector3 spawnPosition = GetRandomPosition();
            GameObject Enemy = Instantiate(enemyToSpawn, spawnPosition, Quaternion.identity);
            for (int i = 0; i < waypoints.Length; i++)
            {
                Enemy.GetComponent<EnemyController>().waypoints[i] = waypoints[i];
            }
            yield return new WaitForSeconds(spawnTime);
            enemyCount++;
            gameProgression.EnemySpawn();
        }
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
