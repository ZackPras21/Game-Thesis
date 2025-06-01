using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RL_TrainingEnemySpawner : MonoBehaviour
{
    [System.Serializable]
    public struct ArenaInfo
    {
        [Tooltip("Four corner Transforms defining this arena’s rectangular spawn area.")]
        public Transform corner1;
        public Transform corner2;
        public Transform corner3;
        public Transform corner4;

        [Tooltip("How many enemies to spawn in this arena.")]
        public int maxEnemyCount;

        [Tooltip("Optional parent under which to organize the spawned enemies for this arena.")]
        public Transform spawnParent;
    }

    [Header("Enemy Prefab")]
    [Tooltip("The Normal Enemy prefab (must already have NavMeshAgent + NormalEnemyAgent).")]
    public GameObject enemyPrefab;

    [Header("Per-Arena Configuration")]
    [Tooltip("For each arena, assign its 4 corners and how many enemies it should get.")]
    public ArenaInfo[] arenas;

    [Tooltip("How frequently (in seconds) to attempt spawning each enemy in an arena.")]
    public float spawnInterval = 0.5f;

    [Tooltip("How many random tries to find an unblocked spot per enemy.")]
    public int maxSpawnAttempts = 20;

    [Tooltip("Which layers count as ‘obstacle’ when checking spawn collisions.")]
    public LayerMask obstacleLayerMask = ~0;

    // Keep track of all spawned enemies so we can clear or respawn later.
    private readonly List<GameObject> _spawnedEnemies = new List<GameObject>();

    private void Start()
    {
        // Launch one coroutine per arena
        for (int i = 0; i < arenas.Length; i++)
        {
            StartCoroutine(SpawnInArenaCoroutine(arenas[i]));
        }
    }

    private IEnumerator SpawnInArenaCoroutine(ArenaInfo arena)
    {
        // Precompute min/max X and Z from the four corners
        float minX = Mathf.Min(
            arena.corner1.position.x,
            arena.corner2.position.x,
            arena.corner3.position.x,
            arena.corner4.position.x
        );
        float maxX = Mathf.Max(
            arena.corner1.position.x,
            arena.corner2.position.x,
            arena.corner3.position.x,
            arena.corner4.position.x
        );
        float minZ = Mathf.Min(
            arena.corner1.position.z,
            arena.corner2.position.z,
            arena.corner3.position.z,
            arena.corner4.position.z
        );
        float maxZ = Mathf.Max(
            arena.corner1.position.z,
            arena.corner2.position.z,
            arena.corner3.position.z,
            arena.corner4.position.z
        );

        int spawnedCount = 0;
        while (spawnedCount < arena.maxEnemyCount)
        {
            yield return new WaitForSeconds(spawnInterval);

            Vector3 chosenPos = Vector3.zero;
            bool foundSpot = false;

            // Try up to maxSpawnAttempts to find a free spot
            for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
            {
                float x = Random.Range(minX, maxX);
                float z = Random.Range(minZ, maxZ);
                Vector3 test = new Vector3(x, 1f, z);

                // If no obstacle (wall/floor) within 0.5m, accept it
                if (!Physics.CheckSphere(test, 0.5f, obstacleLayerMask))
                {
                    chosenPos = test;
                    foundSpot = true;
                    break;
                }
            }

            if (foundSpot)
            {
                GameObject go = Instantiate(
                    enemyPrefab,
                    chosenPos,
                    Quaternion.identity,
                    arena.spawnParent
                );
                _spawnedEnemies.Add(go);
                spawnedCount++;
            }
            else
            {
                Debug.LogWarning(
                    $"[RL_TrainingEnemySpawner] Couldn’t find free spot in arena “" +
                    $"{arena.corner1.name}…{arena.corner4.name}” after {maxSpawnAttempts} tries."
                );
                // Optionally: break; to stop attempting further spawns in this arena
            }
        }
    }

    public void RespawnAllArenas()
    {
        foreach (var e in _spawnedEnemies)
        {
            Destroy(e);
        }
        _spawnedEnemies.Clear();

        // Restart one coroutine per arena
        for (int i = 0; i < arenas.Length; i++)
        {
            StartCoroutine(SpawnInArenaCoroutine(arenas[i]));
        }
    }
}
