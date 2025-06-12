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

        [Header("Patrol Points")]
        public Transform patrolPointA;
        public Transform patrolPointD;
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
    [SerializeField] private float spawnInterval = 0.1f; // Reduced for faster spawning
    [SerializeField] private int maxSpawnAttempts = 50; // Increased attempts
    [SerializeField] private LayerMask obstacleLayerMask = ~0;
    [SerializeField] private float minSpawnDistance = 1.5f; // Minimum distance between spawned enemies
    [SerializeField] private float patrolPointSpawnRadius = 4f; // Radius around patrol points for spawning
    [SerializeField] private bool debugSpawning = true;

    // Fixed: Separate lists for each arena to prevent cross-arena interference
    private Dictionary<int, List<GameObject>> arenaEnemies = new Dictionary<int, List<GameObject>>();
    private Dictionary<int, List<GameObject>> arenaPlayers = new Dictionary<int, List<GameObject>>();

    private void Start()
    {
        InitializeArenaDictionaries();
        StartCoroutine(InitializeAllArenasSequentially());
    }

    private void InitializeArenaDictionaries()
    {
        for (int i = 0; i < arenas.Length; i++)
        {
            arenaEnemies[i] = new List<GameObject>();
            arenaPlayers[i] = new List<GameObject>();
        }
    }

    private IEnumerator InitializeAllArenasSequentially()
    {
        for (int i = 0; i < arenas.Length; i++)
        {
            SpawnPlayerInArena(arenas[i], i);
            yield return StartCoroutine(SpawnEnemiesInArena(arenas[i], i));
            yield return new WaitForSeconds(0.1f); // Small delay between arenas
        }
        
        if (debugSpawning)
        {
            LogSpawningResults();
        }
    }

    public void RespawnAllArenas()
    {
        StopAllCoroutines();
        DestroyAllSpawnedObjects();
        StartCoroutine(InitializeAllArenasSequentially());
    }

    public void RespawnSpecificArena(int arenaIndex)
    {
        if (arenaIndex < 0 || arenaIndex >= arenas.Length) return;

        DestroyArenaObjects(arenaIndex);
        SpawnPlayerInArena(arenas[arenaIndex], arenaIndex);
        StartCoroutine(SpawnEnemiesInArena(arenas[arenaIndex], arenaIndex));
    }

    public void RespawnPlayer(GameObject playerToRespawn)
    {
        if (playerToRespawn == null) return;

        int arenaIndex = FindPlayerArenaIndex(playerToRespawn);
        if (arenaIndex >= 0)
        {
            RespawnPlayerInArena(playerToRespawn, arenas[arenaIndex]);
        }
    }

    private void SpawnPlayerInArena(ArenaConfiguration arena, int arenaIndex)
    {
        Vector3 centerPosition = CalculateArenaCenter(arena);
        GameObject player = Instantiate(playerPrefab, centerPosition, Quaternion.identity);
        arenaPlayers[arenaIndex].Add(player);
        
        if (debugSpawning)
        {
            Debug.Log($"Spawned player in Arena {arenaIndex} at {centerPosition}");
        }
    }

    private IEnumerator SpawnEnemiesInArena(ArenaConfiguration arena, int arenaIndex)
    {
        ArenaBounds bounds = CalculateArenaBounds(arena);

        // Spawn enemies sequentially to ensure exact counts
        if (arena.creepCount > 0)
        {
            yield return StartCoroutine(SpawnEnemyTypeWithGridPattern(creepPrefab, arena.creepCount, bounds, arena, arenaIndex));
        }
        
        if (arena.humanoidCount > 0)
        {
            yield return StartCoroutine(SpawnEnemyTypeWithGridPattern(humanoidPrefab, arena.humanoidCount, bounds, arena, arenaIndex));
        }
        
        if (arena.bullCount > 0)
        {
            yield return StartCoroutine(SpawnEnemyTypeWithGridPattern(bullPrefab, arena.bullCount, bounds, arena, arenaIndex));
        }
    }

    private IEnumerator SpawnEnemyTypeWithGridPattern(GameObject prefab, int count, ArenaBounds bounds, ArenaConfiguration arena, int arenaIndex)
    {
        if (prefab == null || count <= 0) yield break;

        List<Vector3> spawnPositions = GenerateGridSpawnPositions(arena, count, arenaIndex);
        int successfulSpawns = 0;

        for (int i = 0; i < spawnPositions.Count && successfulSpawns < count; i++)
        {
            Vector3 spawnPosition = spawnPositions[i];
            
            // Double-check position validity
            if (IsPositionValidInArena(spawnPosition, bounds, arenaIndex))
            {
                GameObject enemy = Instantiate(prefab, spawnPosition, Quaternion.identity, arena.spawnParent);
                arenaEnemies[arenaIndex].Add(enemy);
                successfulSpawns++;
                
                if (debugSpawning)
                {
                    Debug.Log($"Spawned {prefab.name} #{successfulSpawns} in Arena {arenaIndex} at {spawnPosition}");
                }
                
                yield return new WaitForSeconds(spawnInterval);
            }
            else
            {
                if (debugSpawning)
                {
                    Debug.LogWarning($"Position {spawnPosition} was invalid for {prefab.name} in Arena {arenaIndex}");
                }
            }
        }

        // Fallback for any remaining enemies
        if (successfulSpawns < count)
        {
            yield return StartCoroutine(SpawnRemainingEnemiesRandomly(prefab, count - successfulSpawns, bounds, arena, arenaIndex));
        }
    }

    private List<Vector3> GenerateGridSpawnPositions(ArenaConfiguration arena, int count, int arenaIndex)
    {
        List<Vector3> positions = new List<Vector3>();
        List<Vector3> patrolPoints = GetPatrolPoints(arena);

        if (patrolPoints.Count == 0)
        {
            Debug.LogError($"No patrol points found for Arena {arenaIndex}");
            return positions;
        }

        int enemiesPerPatrol = Mathf.CeilToInt((float)count / patrolPoints.Count);
        
        foreach (Vector3 patrolPoint in patrolPoints)
        {
            List<Vector3> patrolPositions = GeneratePositionsAroundPatrolPoint(patrolPoint, enemiesPerPatrol, arenaIndex);
            positions.AddRange(patrolPositions);
            
            if (positions.Count >= count) break;
        }

        // Trim to exact count needed
        if (positions.Count > count)
        {
            positions = positions.GetRange(0, count);
        }

        return positions;
    }

    private List<Vector3> GeneratePositionsAroundPatrolPoint(Vector3 patrolPoint, int maxEnemies, int arenaIndex)
    {
        List<Vector3> positions = new List<Vector3>();
        
        // Create a grid pattern around the patrol point
        int gridSize = Mathf.CeilToInt(Mathf.Sqrt(maxEnemies));
        float spacing = minSpawnDistance * 1.2f; // Add some extra spacing
        Vector3 startOffset = new Vector3(-(gridSize - 1) * spacing * 0.5f, 0, -(gridSize - 1) * spacing * 0.5f);

        for (int x = 0; x < gridSize && positions.Count < maxEnemies; x++)
        {
            for (int z = 0; z < gridSize && positions.Count < maxEnemies; z++)
            {
                Vector3 gridOffset = new Vector3(x * spacing, 0, z * spacing) + startOffset;
                Vector3 candidatePosition = patrolPoint + gridOffset;
                
                // Add small random variation to avoid perfect grid
                candidatePosition += new Vector3(
                    Random.Range(-0.3f, 0.3f),
                    0,
                    Random.Range(-0.3f, 0.3f)
                );

                // Ensure position is within patrol point spawn radius
                Vector3 directionFromPatrol = (candidatePosition - patrolPoint);
                if (directionFromPatrol.magnitude > patrolPointSpawnRadius)
                {
                    candidatePosition = patrolPoint + directionFromPatrol.normalized * patrolPointSpawnRadius;
                }

                positions.Add(candidatePosition);
            }
        }

        return positions;
    }

    private IEnumerator SpawnRemainingEnemiesRandomly(GameObject prefab, int remainingCount, ArenaBounds bounds, ArenaConfiguration arena, int arenaIndex)
    {
        int successfulSpawns = 0;
        int attempts = 0;
        int maxTotalAttempts = remainingCount * maxSpawnAttempts;

        while (successfulSpawns < remainingCount && attempts < maxTotalAttempts)
        {
            Vector3 spawnPosition = FindValidSpawnPositionNearPatrol(arena, bounds, arenaIndex);
            
            if (spawnPosition != Vector3.positiveInfinity)
            {
                GameObject enemy = Instantiate(prefab, spawnPosition, Quaternion.identity, arena.spawnParent);
                arenaEnemies[arenaIndex].Add(enemy);
                successfulSpawns++;
                
                if (debugSpawning)
                {
                    Debug.Log($"Spawned remaining {prefab.name} #{successfulSpawns} in Arena {arenaIndex} at {spawnPosition}");
                }
                
                yield return new WaitForSeconds(spawnInterval);
            }
            
            attempts++;
        }

        if (successfulSpawns < remainingCount)
        {
            Debug.LogError($"Failed to spawn {remainingCount - successfulSpawns} remaining {prefab.name} in Arena {arenaIndex} after {attempts} attempts");
        }
    }

    private List<Vector3> GetPatrolPoints(ArenaConfiguration arena)
    {
        List<Vector3> patrolPoints = new List<Vector3>();
        
        if (arena.patrolPointA != null)
            patrolPoints.Add(arena.patrolPointA.position);
        if (arena.patrolPointD != null)
            patrolPoints.Add(arena.patrolPointD.position);
        
        // Fallback to corners if no patrol points
        if (patrolPoints.Count == 0)
        {
            if (arena.corner1 != null) patrolPoints.Add(arena.corner1.position);
            if (arena.corner4 != null) patrolPoints.Add(arena.corner4.position);
        }
        
        return patrolPoints;
    }

    private Vector3 FindValidSpawnPositionNearPatrol(ArenaConfiguration arena, ArenaBounds bounds, int arenaIndex)
    {
        List<Vector3> patrolPositions = GetPatrolPoints(arena);

        // Try spawning near patrol points first
        foreach (Vector3 patrolPos in patrolPositions)
        {
            for (int attempt = 0; attempt < maxSpawnAttempts / 2; attempt++)
            {
                Vector2 randomCircle = Random.insideUnitCircle * patrolPointSpawnRadius;
                Vector3 candidatePosition = new Vector3(
                    patrolPos.x + randomCircle.x,
                    patrolPos.y,
                    patrolPos.z + randomCircle.y
                );
                
                if (IsPositionValidInArena(candidatePosition, bounds, arenaIndex))
                {
                    return candidatePosition;
                }
            }
        }

        // Fallback to random position in arena
        return FindValidSpawnPosition(bounds, arenaIndex);
    }

    private Vector3 FindValidSpawnPosition(ArenaBounds bounds, int arenaIndex)
    {
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            Vector3 candidatePosition = GenerateRandomPosition(bounds);
            
            if (IsPositionValidInArena(candidatePosition, bounds, arenaIndex))
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

    private bool IsPositionValidInArena(Vector3 position, ArenaBounds bounds, int arenaIndex)
    {
        // Check bounds
        if (position.x < bounds.minX || position.x > bounds.maxX || 
            position.z < bounds.minZ || position.z > bounds.maxZ)
            return false;

        // Check for obstacles using a slightly larger radius
        if (Physics.CheckSphere(position, 0.7f, obstacleLayerMask))
            return false;

        // Check distance from existing enemies in this arena
        if (arenaEnemies.ContainsKey(arenaIndex))
        {
            foreach (GameObject enemy in arenaEnemies[arenaIndex])
            {
                if (enemy != null && Vector3.Distance(position, enemy.transform.position) < minSpawnDistance)
                {
                    return false;
                }
            }
        }

        // Check distance from players in this arena
        if (arenaPlayers.ContainsKey(arenaIndex))
        {
            foreach (GameObject player in arenaPlayers[arenaIndex])
            {
                if (player != null && Vector3.Distance(position, player.transform.position) < minSpawnDistance * 2f)
                {
                    return false;
                }
            }
        }

        return true;
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
        foreach (var kvp in arenaEnemies)
        {
            DestroyObjectList(kvp.Value);
        }
        
        foreach (var kvp in arenaPlayers)
        {
            DestroyObjectList(kvp.Value);
        }
    }

    private void DestroyArenaObjects(int arenaIndex)
    {
        if (arenaEnemies.ContainsKey(arenaIndex))
        {
            DestroyObjectList(arenaEnemies[arenaIndex]);
        }
        
        if (arenaPlayers.ContainsKey(arenaIndex))
        {
            DestroyObjectList(arenaPlayers[arenaIndex]);
        }
    }

    private void DestroyObjectList(List<GameObject> objects)
    {
        foreach (var obj in objects)
        {
            if (obj != null) DestroyImmediate(obj);
        }
        objects.Clear();
    }

    private int FindPlayerArenaIndex(GameObject player)
    {
        for (int i = 0; i < arenas.Length; i++)
        {
            if (arenaPlayers.ContainsKey(i) && arenaPlayers[i].Contains(player))
            {
                return i;
            }
        }
        
        // Fallback: find closest arena
        float closestDistance = float.MaxValue;
        int closestArena = 0;
        
        for (int i = 0; i < arenas.Length; i++)
        {
            Vector3 center = CalculateArenaCenter(arenas[i]);
            float distance = Vector3.Distance(player.transform.position, center);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestArena = i;
            }
        }
        
        return closestArena;
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

    private void LogSpawningResults()
    {
        for (int i = 0; i < arenas.Length; i++)
        {
            int enemyCount = arenaEnemies.ContainsKey(i) ? arenaEnemies[i].Count : 0;
            int expectedCount = arenas[i].creepCount + arenas[i].humanoidCount + arenas[i].bullCount;
            
            Debug.Log($"Arena {i}: Spawned {enemyCount}/{expectedCount} enemies");
            
            if (enemyCount != expectedCount)
            {
                Debug.LogWarning($"Arena {i} spawn count mismatch! Expected: {expectedCount}, Actual: {enemyCount}");
            }
        }
    }

    // Debug visualization
    private void OnDrawGizmosSelected()
    {
        if (arenas == null) return;

        for (int i = 0; i < arenas.Length; i++)
        {
            var arena = arenas[i];
            
            // Draw arena boundaries
            Gizmos.color = Color.blue;
            Vector3 center = CalculateArenaCenter(arena);
            Gizmos.DrawWireCube(center, new Vector3(
                Mathf.Abs(arena.corner1.position.x - arena.corner3.position.x),
                2f,
                Mathf.Abs(arena.corner1.position.z - arena.corner3.position.z)
            ));
            
            // Draw patrol points and spawn areas
            if (arena.patrolPointA != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(arena.patrolPointA.position, 1f);
                Gizmos.color = new Color(0, 1, 0, 0.2f);
                Gizmos.DrawSphere(arena.patrolPointA.position, patrolPointSpawnRadius);
            }
            
            if (arena.patrolPointD != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(arena.patrolPointD.position, 1f);
                Gizmos.color = new Color(1, 0, 0, 0.2f);
                Gizmos.DrawSphere(arena.patrolPointD.position, patrolPointSpawnRadius);
            }
        }
    }

    private struct ArenaBounds
    {
        public float minX, maxX, minZ, maxZ;
    }
}