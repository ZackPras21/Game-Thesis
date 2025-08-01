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
                Gate.GetComponent<GateInteraction>().CloseGate();
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
            GameObject enemy = Instantiate(enemyToSpawn, spawnPosition, Quaternion.identity);
            EnemyController enemyController = enemy.GetComponent<EnemyController>();
            
            // Only assign waypoints up to the enemy's waypoints array capacity
            int minLength = Mathf.Min(waypoints.Length, enemyController.waypoints.Length);
            for (int i = 0; i < minLength; i++)
            {
                enemyController.waypoints[i] = waypoints[i];
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

        float randomX = Random.Range(minCorner.x, maxCorner.x);
        float randomZ = Random.Range(minCorner.z, maxCorner.z);

        return new Vector3(randomX, 1f, randomZ);
    }
}
