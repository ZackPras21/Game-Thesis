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

    #region Unity Lifecycle
    private void Awake()
    {
        cameraController = FindCameraController();
        gameProgressionManager = GameProgression.Instance;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayerHitbox(other))
        {
            ActivateCombatMode();
            if (!hasTriggeredSpawn)
                StartSpawningSequence();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayerHitbox(other))
            DeactivateCombatMode();
    }
    #endregion

    #region Player Detection
    private bool IsPlayerHitbox(Collider collider) => 
        collider.CompareTag("Player") && collider.gameObject.layer == LayerMask.NameToLayer("Hitbox");

    private void ActivateCombatMode()
    {
        if (cameraController != null)
            cameraController.CombatMode();
        else
            Debug.LogError("CameraFollow reference not found in RL_EnemySpawner");
    }

    private void DeactivateCombatMode() => cameraController?.NormalMode();
    #endregion

    #region Spawning System
    private void StartSpawningSequence()
    {
        AudioManager.instance?.PlaySFX(AudioManager.instance.gateClose);
        StartCoroutine(SpawnEnemiesCoroutine());
        hasTriggeredSpawn = true;
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
        var enemyPrefab = SelectEnemyType();
        var spawnPosition = GetValidSpawnPosition();
        
        if (spawnPosition != Vector3.zero)
        {
            CreateAndConfigureEnemy(enemyPrefab, spawnPosition);
            gameProgressionManager?.EnemySpawn();
        }
    }

    private GameObject SelectEnemyType()
    {
        float progressionRatio = CalculateProgressionRatio();
        float randomValue = Random.value;

        if (randomValue < 0.5f * progressionRatio)
            return enemyPrefabs[MEDIUM_ENEMY_INDEX];
        else if (randomValue < progressionRatio)
            return enemyPrefabs[NORMAL_ENEMY_INDEX];
        else
            return enemyPrefabs[CREEP_ENEMY_INDEX];
    }

    private float CalculateProgressionRatio()
    {
        if (gameProgressionManager == null) return 0f;
        return (float)gameProgressionManager.EnemyTotalSpawnCount / gameProgressionManager.EnemyTotalCount;
    }

    private void CreateAndConfigureEnemy(GameObject prefab, Vector3 position)
    {
        var newEnemy = Instantiate(prefab, position, Quaternion.identity);
        var enemyController = newEnemy.GetComponent<EnemyController>();
        
        if (enemyController != null && enemyWaypoints != null)
        {
            for (int i = 0; i < enemyWaypoints.Length && i < enemyController.waypoints.Length; i++)
                enemyController.waypoints[i] = enemyWaypoints[i];
        }
    }
    #endregion

    #region Spawn Position Validation
    private Vector3 GetValidSpawnPosition()
    {
        if (spawnAreaCorner1 == null || spawnAreaCorner2 == null)
            return Vector3.zero;

        var minBounds = Vector3.Min(spawnAreaCorner1.transform.position, spawnAreaCorner2.transform.position);
        var maxBounds = Vector3.Max(spawnAreaCorner1.transform.position, spawnAreaCorner2.transform.position);

        for (int attempt = 0; attempt < MAX_SPAWN_ATTEMPTS; attempt++)
        {
            var candidatePosition = GenerateRandomPosition(minBounds, maxBounds);
            if (IsPositionValid(candidatePosition))
                return candidatePosition;
        }

        return Vector3.zero;
    }

    private Vector3 GenerateRandomPosition(Vector3 minBounds, Vector3 maxBounds)
    {
        float randomX = Random.Range(minBounds.x, maxBounds.x);
        float randomZ = Random.Range(minBounds.z, maxBounds.z);
        return new Vector3(randomX, SPAWN_HEIGHT, randomZ);
    }

    private bool IsPositionValid(Vector3 position) => 
        !Physics.CheckSphere(position, OBSTACLE_CHECK_RADIUS, LayerMask.GetMask("Obstacle"));
    #endregion

    #region Utility Methods
    private CameraFollow FindCameraController()
    {
        var mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
        return mainCamera?.GetComponent<CameraFollow>();
    }
    #endregion
}