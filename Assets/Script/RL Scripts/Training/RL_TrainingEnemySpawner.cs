using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RL_TrainingEnemySpawner : MonoBehaviour
{
    [System.Serializable]
    public struct ArenaConfiguration
    {
        [Header("Arena Boundaries")]
        public Transform corner1;
        public Transform corner2;
        public Transform corner3;
        public Transform corner4;

        [Header("Enemy Counts")]
        public int creepCount;
        public int humanoidCount;
        public int bullCount;

        [Header("Organization")]
        public Transform spawnParent;
    }

    [Header("Enemy Prefabs")]
    [SerializeField] private GameObject creepPrefab;
    [SerializeField] private GameObject humanoidPrefab;
    [SerializeField] private GameObject bullPrefab;

    [Header("Player Configuration")]
    [SerializeField] private GameObject playerPrefab;

    [Header("Arena Setup")]
    [SerializeField] private ArenaConfiguration[] arenas;

    [Header("Spawn Settings")]
    [SerializeField] private float spawnInterval = 0.5f;
    [SerializeField] private int maxSpawnAttempts = 20;
    [SerializeField] private LayerMask obstacleLayerMask = ~0;

    private readonly List<GameObject> spawnedEnemies = new List<GameObject>();
    private readonly List<GameObject> spawnedPlayers = new List<GameObject>();

    private void Start()
    {
        InitializeAllArenas();
    }

    public void RespawnAllArenas()
    {
        DestroyAllSpawnedObjects();
        InitializeAllArenas();
    }

    public void RespawnPlayer(GameObject playerToRespawn)
    {
        if (playerToRespawn == null) return;

        ArenaConfiguration targetArena = FindPlayerArena(playerToRespawn);
        RespawnPlayerInArena(playerToRespawn, targetArena);
    }

    private void InitializeAllArenas()
    {
        for (int i = 0; i < arenas.Length; i++)
        {
            SpawnPlayerInArena(arenas[i]);
            StartCoroutine(SpawnEnemiesInArena(arenas[i]));
        }
    }

    private void SpawnPlayerInArena(ArenaConfiguration arena)
    {
        Vector3 centerPosition = CalculateArenaCenter(arena);
        GameObject player = Instantiate(playerPrefab, centerPosition, Quaternion.identity);
        spawnedPlayers.Add(player);
    }

    private IEnumerator SpawnEnemiesInArena(ArenaConfiguration arena)
    {
        ArenaBounds bounds = CalculateArenaBounds(arena);

        yield return StartCoroutine(SpawnEnemyType(creepPrefab, arena.creepCount, bounds, arena.spawnParent));
        yield return StartCoroutine(SpawnEnemyType(humanoidPrefab, arena.humanoidCount, bounds, arena.spawnParent));
        yield return StartCoroutine(SpawnEnemyType(bullPrefab, arena.bullCount, bounds, arena.spawnParent));
    }

    private IEnumerator SpawnEnemyType(GameObject prefab, int count, ArenaBounds bounds, Transform parent)
    {
        if (prefab == null) yield break;

        for (int i = 0; i < count; i++)
        {
            yield return new WaitForSeconds(spawnInterval);
            
            Vector3 spawnPosition = FindValidSpawnPosition(bounds);
            if (spawnPosition != Vector3.positiveInfinity)
            {
                GameObject enemy = Instantiate(prefab, spawnPosition, Quaternion.identity, parent);
                spawnedEnemies.Add(enemy);
            }
        }
    }

    private Vector3 FindValidSpawnPosition(ArenaBounds bounds)
    {
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            Vector3 candidatePosition = GenerateRandomPosition(bounds);
            
            if (IsPositionValid(candidatePosition))
            {
                return candidatePosition;
            }
        }
        return Vector3.positiveInfinity;
    }

    private Vector3 GenerateRandomPosition(ArenaBounds bounds)
    {
        float x = Random.Range(bounds.minX, bounds.maxX);
        float z = Random.Range(bounds.minZ, bounds.maxZ);
        return new Vector3(x, 1f, z);
    }

    private bool IsPositionValid(Vector3 position)
    {
        return !Physics.CheckSphere(position, 0.5f, obstacleLayerMask);
    }

    private ArenaBounds CalculateArenaBounds(ArenaConfiguration arena)
    {
        return new ArenaBounds
        {
            minX = Mathf.Min(arena.corner1.position.x, arena.corner2.position.x, arena.corner3.position.x, arena.corner4.position.x),
            maxX = Mathf.Max(arena.corner1.position.x, arena.corner2.position.x, arena.corner3.position.x, arena.corner4.position.x),
            minZ = Mathf.Min(arena.corner1.position.z, arena.corner2.position.z, arena.corner3.position.z, arena.corner4.position.z),
            maxZ = Mathf.Max(arena.corner1.position.z, arena.corner2.position.z, arena.corner3.position.z, arena.corner4.position.z)
        };
    }

    private Vector3 CalculateArenaCenter(ArenaConfiguration arena)
    {
        return (arena.corner1.position + arena.corner2.position + arena.corner3.position + arena.corner4.position) / 4f;
    }

    private void DestroyAllSpawnedObjects()
    {
        DestroyObjectList(spawnedEnemies);
        DestroyObjectList(spawnedPlayers);
    }

    private void DestroyObjectList(List<GameObject> objects)
    {
        foreach (var obj in objects)
        {
            if (obj != null) Destroy(obj);
        }
        objects.Clear();
    }

    private ArenaConfiguration FindPlayerArena(GameObject player)
    {
        foreach (var arena in arenas)
        {
            Vector3 center = CalculateArenaCenter(arena);
            if (Vector3.Distance(player.transform.position, center) < 50f)
            {
                return arena;
            }
        }
        return arenas[0];
    }

    private void RespawnPlayerInArena(GameObject player, ArenaConfiguration arena)
    {
        Vector3 center = CalculateArenaCenter(arena);
        player.transform.position = center;
        player.SetActive(true);
        
        var playerComponent = player.GetComponent<RL_Player>();
        if (playerComponent != null)
        {
            playerComponent.gameObject.SetActive(true);
        }
    }

    private struct ArenaBounds
    {
        public float minX, maxX, minZ, maxZ;
    }
}