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

        [Header("How Many of Each Enemy Type")]
        [Tooltip("How many Creeps to spawn in this arena.")]
        public int creepCount;

        [Tooltip("How many Humanoids to spawn in this arena.")]
        public int humanoidCount;

        [Tooltip("How many Bulls to spawn in this arena.")]
        public int bullCount;

        [Tooltip("Optional parent under which to organize all spawned enemies in this arena.")]
        public Transform spawnParent;
    }

    [Header("Enemy Prefabs (assign exactly one prefab per type)")]
    [Tooltip("The Creep‐type enemy prefab (must have NavMeshAgent + NormalEnemyAgent component).")]
    public GameObject creepPrefab;

    [Tooltip("The Humanoid‐type enemy prefab (must have NavMeshAgent + NormalEnemyAgent component).")]
    public GameObject humanoidPrefab;

    [Tooltip("The Bull‐type enemy prefab (must have NavMeshAgent + NormalEnemyAgent component).")]
    public GameObject bullPrefab;

    [Header("Player Settings")]
    [Tooltip("Player prefab to spawn in each arena")]
    public GameObject playerPrefab;

    [Header("Per‐Arena Configuration")]
    [Tooltip("Configure each arena’s corners and how many of each enemy‐type it should spawn.")]
    public ArenaInfo[] arenas;

    [Header("General Spawn Settings")]
    [Tooltip("Time (in seconds) between individual spawn attempts.")]
    public float spawnInterval = 0.5f;

    [Tooltip("How many tries to find a free spot for each individual enemy before giving up.")]
    public int maxSpawnAttempts = 20;

    [Tooltip("Which layers count as “obstacle” when we check if a position is free.")]
    public LayerMask obstacleLayerMask = ~0;

    // Keep a running list of all spawned enemies (so you can clear or respawn later).
    private readonly List<GameObject> _spawnedEnemies = new List<GameObject>();

    private void Start()
    {
        // Start one coroutine per arena
        for (int i = 0; i < arenas.Length; i++)
        {
            StartCoroutine(SpawnInArenaCoroutine(arenas[i]));
        }
    }

    private IEnumerator SpawnInArenaCoroutine(ArenaInfo arena)
    {
        // Precompute the X/Z min‐max from the four corners
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

        // Helper local function: try to find a random free spot within those bounds
        Vector3 FindFreeSpot()
        {
            for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
            {
                float x = Random.Range(minX, maxX);
                float z = Random.Range(minZ, maxZ);
                // We choose a Y of 1.0f (assuming your ground is roughly at y=0)
                Vector3 testPos = new Vector3(x, 1f, z);

                // If nothing in that 0.5m radius (using obstacleLayerMask), accept it
                if (!Physics.CheckSphere(testPos, 0.5f, obstacleLayerMask))
                {
                    return testPos;
                }
            }
            return Vector3.positiveInfinity;
        }

        // 1) Spawn exactly `arena.creepCount` creep enemies:
        for (int i = 0; i < arena.creepCount; i++)
        {
            yield return new WaitForSeconds(spawnInterval);
            Vector3 spawnPos = FindFreeSpot();
            if (spawnPos == Vector3.positiveInfinity)
            {
                Debug.LogWarning(
                    $"[Spawner] Could not find free spot for Creep #{i+1} in arena {arena.corner1.name}…{arena.corner4.name}"
                );
                continue; // skip this creep and move on to the next
            }

            if (creepPrefab == null)
            {
                Debug.LogError("[Spawner] creepPrefab is not assigned in the Inspector!");
                continue;
            }

            GameObject go = Instantiate(
                creepPrefab,
                spawnPos,
                Quaternion.identity,
                arena.spawnParent
            );
            _spawnedEnemies.Add(go);
        }

        // 2) Spawn exactly `arena.humanoidCount` humanoid enemies:
        for (int i = 0; i < arena.humanoidCount; i++)
        {
            yield return new WaitForSeconds(spawnInterval);
            Vector3 spawnPos = FindFreeSpot();
            if (spawnPos == Vector3.positiveInfinity)
            {
                Debug.LogWarning(
                    $"[Spawner] Could not find free spot for Humanoid #{i+1} in arena {arena.corner1.name}…{arena.corner4.name}"
                );
                continue;
            }

            if (humanoidPrefab == null)
            {
                Debug.LogError("[Spawner] humanoidPrefab is not assigned in the Inspector!");
                continue;
            }

            GameObject go = Instantiate(
                humanoidPrefab,
                spawnPos,
                Quaternion.identity,
                arena.spawnParent
            );
            _spawnedEnemies.Add(go);
        }

        // 3) Spawn exactly `arena.bullCount` bull enemies:
        for (int i = 0; i < arena.bullCount; i++)
        {
            yield return new WaitForSeconds(spawnInterval);
            Vector3 spawnPos = FindFreeSpot();
            if (spawnPos == Vector3.positiveInfinity)
            {
                Debug.LogWarning(
                    $"[Spawner] Could not find free spot for Bull #{i+1} in arena {arena.corner1.name}…{arena.corner4.name}"
                );
                continue;
            }

            if (bullPrefab == null)
            {
                Debug.LogError("[Spawner] bullPrefab is not assigned in the Inspector!");
                continue;
            }

            GameObject go = Instantiate(
                bullPrefab,
                spawnPos,
                Quaternion.identity,
                arena.spawnParent
            );
            _spawnedEnemies.Add(go);
        }

        // Once all three loops finish, this coroutine is done.
    }

    private List<GameObject> _spawnedPlayers = new List<GameObject>();

    public void RespawnAllArenas()
    {
        // Destroy every spawned enemy
        foreach (var e in _spawnedEnemies)
        {
            if (e != null) Destroy(e);
        }
        _spawnedEnemies.Clear();

        // Destroy every spawned player
        foreach (var p in _spawnedPlayers)
        {
            if (p != null) Destroy(p);
        }
        _spawnedPlayers.Clear();

        // Restart coroutines for each arena
        for (int i = 0; i < arenas.Length; i++)
        {
            // Spawn player at center of arena
            Vector3 center = (arenas[i].corner1.position + arenas[i].corner2.position +
                            arenas[i].corner3.position + arenas[i].corner4.position) / 4f;
            GameObject player = Instantiate(playerPrefab, center, Quaternion.identity);
            _spawnedPlayers.Add(player);

            StartCoroutine(SpawnInArenaCoroutine(arenas[i]));
        }
    }

    public void RespawnPlayer(GameObject playerToRespawn)
    {
        if (playerToRespawn == null) return;

        // Find which arena this player was in
        foreach (var arena in arenas)
        {
            Vector3 center = (arena.corner1.position + arena.corner2.position +
                            arena.corner3.position + arena.corner4.position) / 4f;
            
            if (Vector3.Distance(playerToRespawn.transform.position, center) < 50f)
            {
                playerToRespawn.transform.position = center;
                playerToRespawn.SetActive(true);
                // Reset player health/components if needed
                var player = playerToRespawn.GetComponent<RL_Player>();
                if (player != null)
                {
                    player.gameObject.SetActive(true);
                    // Add any other player reset logic here
                }
                break;
            }
        }
    }
}
