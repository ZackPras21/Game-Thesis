using System.Collections;
using UnityEngine;

public class RL_EnemySpawner : MonoBehaviour
{
    [Header("Enemy Configuration")]
    [SerializeField] private GameObject[] enemyPrefabs;
    [SerializeField] private int maxEnemyCount = 10;
    [SerializeField] private float timeBetweenSpawns = 0.5f;

    [Header("Spawn Area")]
    [SerializeField] private GameObject spawnAreaCorner1;
    [SerializeField] private GameObject spawnAreaCorner2;

    [Header("Waypoint System")]
    [SerializeField] private Transform[] enemyWaypoints;

    [Header("Audio & UI")]
    [SerializeField] private GameObject gate;

    private int currentEnemyCount = 0;
    private bool hasTriggeredSpawn = false;
    
    private CameraFollow cameraController;
    private GameProgression gameProgressionManager;

    private const int CREEP_ENEMY_INDEX = 0;
    private const int NORMAL_ENEMY_INDEX = 1;
    private const int MEDIUM_ENEMY_INDEX = 2;
    private const int MAX_SPAWN_ATTEMPTS = 10;
    private const float SPAWN_HEIGHT = 1f;
    private const float OBSTACLE_CHECK_RADIUS = 0.5f;

    private void Awake()
    {
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        cameraController = FindCameraController();
        gameProgressionManager = GameProgression.Instance;
    }

    private CameraFollow FindCameraController()
    {
        GameObject mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
        return mainCamera?.GetComponent<CameraFollow>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayerHitbox(other))
        {
            HandlePlayerEntered();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayerHitbox(other))
        {
            HandlePlayerExited();
        }
    }

    private bool IsPlayerHitbox(Collider collider)
    {
        return collider.CompareTag("Player") && 
               collider.gameObject.layer == LayerMask.NameToLayer("Hitbox");
    }

    private void HandlePlayerEntered()
    {
        ActivateCombatMode();
        
        if (!hasTriggeredSpawn)
        {
            StartSpawningSequence();
        }
    }

    private void HandlePlayerExited()
    {
        DeactivateCombatMode();
    }

    private void ActivateCombatMode()
    {
        if (cameraController != null)
        {
            cameraController.CombatMode();
        }
        else
        {
            Debug.LogError("CameraFollow reference not found in RL_EnemySpawner");
        }
    }

    private void DeactivateCombatMode()
    {
        if (cameraController != null)
        {
            cameraController.NormalMode();
        }
    }

    private void StartSpawningSequence()
    {
        PlayGateCloseSound();
        StartCoroutine(SpawnEnemiesCoroutine());
        hasTriggeredSpawn = true;
    }

    private void PlayGateCloseSound()
    {
        if (AudioManager.instance != null)
        {
            AudioManager.instance.PlaySFX(AudioManager.instance.gateClose);
        }
    }

    private IEnumerator SpawnEnemiesCoroutine()
    {
        while (currentEnemyCount < maxEnemyCount)
        {
            SpawnSingleEnemy();
            yield return new WaitForSeconds(timeBetweenSpawns);
            currentEnemyCount++;
        }
    }

    private void SpawnSingleEnemy()
    {
        GameObject enemyPrefab = SelectEnemyType();
        Vector3 spawnPosition = GetValidSpawnPosition();
        
        if (spawnPosition != Vector3.zero)
        {
            CreateAndConfigureEnemy(enemyPrefab, spawnPosition);
            NotifyGameProgression();
        }
    }

    private GameObject SelectEnemyType()
    {
        float progressionRatio = CalculateProgressionRatio();
        float randomValue = Random.value;

        if (randomValue < 0.5f * progressionRatio)
        {
            return enemyPrefabs[MEDIUM_ENEMY_INDEX];
        }
        else if (randomValue < progressionRatio)
        {
            return enemyPrefabs[NORMAL_ENEMY_INDEX];
        }
        else
        {
            return enemyPrefabs[CREEP_ENEMY_INDEX];
        }
    }

    private float CalculateProgressionRatio()
    {
        if (gameProgressionManager == null) return 0f;
        
        return (float)gameProgressionManager.EnemyTotalSpawnCount / 
               gameProgressionManager.EnemyTotalCount;
    }

    private void CreateAndConfigureEnemy(GameObject prefab, Vector3 position)
    {
        GameObject newEnemy = Instantiate(prefab, position, Quaternion.identity);
        AssignWaypointsToEnemy(newEnemy);
    }

    private void AssignWaypointsToEnemy(GameObject enemy)
    {
        EnemyController enemyController = enemy.GetComponent<EnemyController>();
        if (enemyController != null && enemyWaypoints != null)
        {
            for (int i = 0; i < enemyWaypoints.Length; i++)
            {
                enemyController.waypoints[i] = enemyWaypoints[i];
            }
        }
    }

    private void NotifyGameProgression()
    {
        if (gameProgressionManager != null)
        {
            gameProgressionManager.EnemySpawn();
        }
    }

    private Vector3 GetValidSpawnPosition()
    {
        if (!AreSpawnCornersValid())
        {
            return Vector3.zero;
        }

        Vector3 minBounds = GetMinimumBounds();
        Vector3 maxBounds = GetMaximumBounds();

        for (int attempt = 0; attempt < MAX_SPAWN_ATTEMPTS; attempt++)
        {
            Vector3 candidatePosition = GenerateRandomPosition(minBounds, maxBounds);
            
            if (IsPositionValid(candidatePosition))
            {
                return candidatePosition;
            }
        }

        return Vector3.zero;
    }

    private bool AreSpawnCornersValid()
    {
        return spawnAreaCorner1 != null && spawnAreaCorner2 != null;
    }

    private Vector3 GetMinimumBounds()
    {
        return Vector3.Min(spawnAreaCorner1.transform.position, 
        spawnAreaCorner2.transform.position);
    }

    private Vector3 GetMaximumBounds()
    {
        return Vector3.Max(spawnAreaCorner1.transform.position, 
        spawnAreaCorner2.transform.position);
    }

    private Vector3 GenerateRandomPosition(Vector3 minBounds, Vector3 maxBounds)
    {
        float randomX = Random.Range(minBounds.x, maxBounds.x);
        float randomZ = Random.Range(minBounds.z, maxBounds.z);
        
        return new Vector3(randomX, SPAWN_HEIGHT, randomZ);
    }

    private bool IsPositionValid(Vector3 position)
    {
        return !Physics.CheckSphere(position, OBSTACLE_CHECK_RADIUS, 
        LayerMask.GetMask("Obstacle"));
    }
}