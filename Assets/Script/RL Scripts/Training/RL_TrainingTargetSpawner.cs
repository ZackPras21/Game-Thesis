using System.Collections.Generic;
using UnityEngine;

public class RL_TrainingTargetSpawner : MonoBehaviour
{
    [System.Serializable]
    public struct ArenaConfiguration
    {
        [Header("Arena Boundaries")]
        public Transform corner1;
        public Transform corner2;
        public Transform corner3;
        public Transform corner4;

        [Header("Target Configuration")]
        public int maxTargets;
        
        [Header("Organization")]
        public Transform spawnParent;

        [Header("Spawn Points (Optional)")]
        public Transform[] customSpawnPoints;
    }

    [Header("Spawn Configuration")]
    [SerializeField] private GameObject trainingTargetPrefab;
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private ArenaConfiguration[] arenas;
    [SerializeField] private int maxSpawnAttempts = 10;
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private LayerMask spawnCollisionLayers;
    [SerializeField] private bool enableContinuousSpawning = false;
    [SerializeField] private float minSpawnDistance = 2f;

    [Header("Visual Effects")]
    [SerializeField] private GameObject spawnParticlePrefab;
    [SerializeField] private GameObject episodeStartPrefab;
    [SerializeField] private GameObject episodeEndPrefab;
    [SerializeField] private Light arenaLight;
    [SerializeField] private Color activeColor = Color.green;
    [SerializeField] private Color inactiveColor = Color.red;

    [Header("Debug")]
    [SerializeField] private bool debugSpawning = true;

    // Separate tracking for each arena
    private Dictionary<int, List<GameObject>> arenaTargets = new Dictionary<int, List<GameObject>>();
    private Dictionary<int, ArenaBounds> arenaBounds = new Dictionary<int, ArenaBounds>();
    private GameObject activePlayer;
    private float lastSpawnTime;
    private bool episodeActive = false;
    private bool isSpawningEnabled = true;
    private bool isInitialized = false;

    // Events for reward tracking
    public static System.Action<float> OnRewardAdded;

    private struct ArenaBounds
    {
        public float minX, maxX, minZ, maxZ;
        public Vector3 center;
    }

    private void Start()
    {
        InitializeSpawner();
    }

    private void Update()
    {
        if (isInitialized && enableContinuousSpawning && episodeActive && isSpawningEnabled)
        {
            HandleContinuousSpawning();
        }
    }

    #region Initialization

    private void InitializeSpawner()
    {
        if (debugSpawning)
        {
            Debug.Log($"[{gameObject.name}] Initializing spawner with {arenas.Length} arenas");
        }

        // Initialize arena dictionaries
        for (int i = 0; i < arenas.Length; i++)
        {
            arenaTargets[i] = new List<GameObject>();
            arenaBounds[i] = CalculateArenaBounds(arenas[i]);
        }

        lastSpawnTime = Time.time;
        episodeActive = false;
        UpdateArenaVisuals();
        isInitialized = true;

        if (debugSpawning)
        {
            LogArenaConfiguration();
        }
    }

    private void LogArenaConfiguration()
    {
        for (int i = 0; i < arenas.Length; i++)
        {
            Debug.Log($"Arena {i}: MaxTargets={arenas[i].maxTargets}, Bounds={arenaBounds[i].center}");
        }
    }

    #endregion

    #region Public API Methods

    public void ResetArena()
    {
        if (debugSpawning)
        {
            Debug.Log($"[{gameObject.name}] ResetArena called - Resetting all {arenas.Length} arenas");
        }

        // Destroy all existing targets
        DestroyAllTargets();

        // Spawn player
        SpawnPlayer();

        // Play effects
        PlayEpisodeStartEffect();

        // Start new episode
        StartNewEpisode();

        // Spawn initial targets in all arenas if enabled
        if (isSpawningEnabled)
        {
            SpawnInitialTargetsInAllArenas();
        }
    }

    public void SetSpawningEnabled(bool enabled)
    {
        bool wasEnabled = isSpawningEnabled;
        isSpawningEnabled = enabled;

        if (debugSpawning)
        {
            Debug.Log($"[{gameObject.name}] Spawning enabled changed: {wasEnabled} -> {enabled}");
        }

        if (!enabled && wasEnabled)
        {
            DestroyAllTargets();
            episodeActive = false;
        }

        UpdateArenaVisuals();
    }

    public void SetMaxTargetsForArena(int arenaIndex, int newMax)
    {
        if (arenaIndex < 0 || arenaIndex >= arenas.Length) return;

        int oldMax = arenas[arenaIndex].maxTargets;
        arenas[arenaIndex].maxTargets = Mathf.Max(0, newMax);

        if (debugSpawning)
        {
            Debug.Log($"[{gameObject.name}] Arena {arenaIndex} max targets changed: {oldMax} -> {newMax}");
        }

        // Remove excess targets if needed
        if (arenaTargets[arenaIndex].Count > newMax)
        {
            int excessCount = arenaTargets[arenaIndex].Count - newMax;
            for (int i = 0; i < excessCount; i++)
            {
                DestroyOldestTargetInArena(arenaIndex);
            }
        }
    }

    public void SpawnTargetsManuallyInArena(int arenaIndex, int count)
    {
        if (!isSpawningEnabled || arenaIndex < 0 || arenaIndex >= arenas.Length)
        {
            if (debugSpawning)
            {
                Debug.LogWarning($"[{gameObject.name}] Cannot spawn manually in arena {arenaIndex}");
            }
            return;
        }

        var arena = arenas[arenaIndex];
        if (arena.maxTargets <= 0) return;

        count = Mathf.Clamp(count, 0, arena.maxTargets - arenaTargets[arenaIndex].Count);

        if (debugSpawning)
        {
            Debug.Log($"[{gameObject.name}] Manual spawn in Arena {arenaIndex}: {count} targets");
        }

        for (int i = 0; i < count; i++)
        {
            SpawnSingleTargetInArena(arenaIndex);
        }
    }

    #endregion

    #region Target Lifecycle Events

    public void OnTargetDestroyed(GameObject target)
    {
        int arenaIndex = FindTargetArena(target);
        if (arenaIndex == -1) return;

        if (debugSpawning)
        {
            Debug.Log($"[{gameObject.name}] Target destroyed in Arena {arenaIndex}");
        }

        RemoveTargetFromArena(target, arenaIndex);
        UpdateArenaVisuals();

        // Spawn replacement if needed
        if (ShouldSpawnReplacementInArena(arenaIndex))
        {
            SpawnSingleTargetInArena(arenaIndex);
        }

        CheckForEpisodeEnd();
    }

    #endregion

    #region Private Methods

    private void SpawnPlayer()
    {
        if (playerPrefab == null) return;

        if (activePlayer != null)
        {
            DestroyImmediate(activePlayer);
        }

        // Spawn player at the center of the first arena or at spawner position
        Vector3 spawnPos = arenas.Length > 0 ? arenaBounds[0].center : transform.position;
        activePlayer = Instantiate(playerPrefab, spawnPos, Quaternion.identity);

        if (debugSpawning)
        {
            Debug.Log($"[{gameObject.name}] Spawned player at {spawnPos}");
        }
    }

    public void RespawnPlayer(GameObject playerToRespawn = null)
    {
        if (playerToRespawn != null)
        {
            // Respawn the specified player at spawner position
            playerToRespawn.transform.position = transform.position;
            playerToRespawn.SetActive(true);
            
            var playerComponent = playerToRespawn.GetComponent<RL_Player>();
            if (playerComponent != null)
            {
                playerComponent.gameObject.SetActive(true);
            }
        }
        else if (activePlayer != null)
        {
            // Respawn the tracked player if no specific player provided
            activePlayer.transform.position = transform.position;
            activePlayer.SetActive(true);
            
            var playerComponent = activePlayer.GetComponent<RL_Player>();
            if (playerComponent != null)
            {
                playerComponent.gameObject.SetActive(true);
            }
        }
    }

    private void HandleContinuousSpawning()
    {
        if (Time.time < lastSpawnTime + spawnInterval) return;

        // Try to spawn in arenas that need more targets
        for (int i = 0; i < arenas.Length; i++)
        {
            if (CanSpawnMoreInArena(i))
            {
                if (SpawnSingleTargetInArena(i))
                {
                    lastSpawnTime = Time.time;
                    break; // Only spawn one per interval
                }
            }
        }
    }

    private void StartNewEpisode()
    {
        episodeActive = true;
        lastSpawnTime = Time.time - spawnInterval;
        UpdateArenaVisuals();

        if (debugSpawning)
        {
            Debug.Log($"[{gameObject.name}] Episode started");
        }
    }

    private void SpawnInitialTargetsInAllArenas()
    {
        if (debugSpawning)
        {
            Debug.Log($"[{gameObject.name}] Spawning initial targets in all arenas");
        }

        for (int arenaIndex = 0; arenaIndex < arenas.Length; arenaIndex++)
        {
            var arena = arenas[arenaIndex];
            if (arena.maxTargets <= 0) continue;

            int spawnedCount = 0;
            for (int i = 0; i < arena.maxTargets; i++)
            {
                if (SpawnSingleTargetInArena(arenaIndex))
                {
                    spawnedCount++;
                }
                else
                {
                    if (debugSpawning)
                    {
                        Debug.LogWarning($"[{gameObject.name}] Failed to spawn target {i + 1}/{arena.maxTargets} in Arena {arenaIndex}");
                    }
                    break;
                }
            }

            if (debugSpawning)
            {
                Debug.Log($"[{gameObject.name}] Arena {arenaIndex}: Spawned {spawnedCount}/{arena.maxTargets} targets");
            }
        }
    }

    private bool SpawnSingleTargetInArena(int arenaIndex)
    {
        if (!CanSpawnMoreInArena(arenaIndex)) return false;

        Vector3 spawnPosition = GetValidSpawnPositionInArena(arenaIndex);
        if (spawnPosition == Vector3.zero) return false;

        GameObject newTarget = CreateTargetAtPosition(spawnPosition, arenaIndex);
        ConfigureNewTarget(newTarget);
        PlaySpawnEffect(spawnPosition);
        UpdateArenaVisuals();

        if (debugSpawning)
        {
            Debug.Log($"[{gameObject.name}] Target spawned in Arena {arenaIndex} at {spawnPosition}");
        }

        return true;
    }

    private bool CanSpawnMoreInArena(int arenaIndex)
    {
        if (arenaIndex < 0 || arenaIndex >= arenas.Length) return false;
        
        var arena = arenas[arenaIndex];
        return isInitialized &&
               isSpawningEnabled &&
               episodeActive &&
               arena.maxTargets > 0 &&
               arenaTargets[arenaIndex].Count < arena.maxTargets &&
               trainingTargetPrefab != null;
    }

    private bool ShouldSpawnReplacementInArena(int arenaIndex)
    {
        return enableContinuousSpawning && CanSpawnMoreInArena(arenaIndex);
    }

    private Vector3 GetValidSpawnPositionInArena(int arenaIndex)
    {
        var arena = arenas[arenaIndex];
        var bounds = arenaBounds[arenaIndex];

        // Try custom spawn points first
        if (arena.customSpawnPoints != null && arena.customSpawnPoints.Length > 0)
        {
            foreach (var spawnPoint in arena.customSpawnPoints)
            {
                if (spawnPoint != null && IsPositionValidInArena(spawnPoint.position, arenaIndex))
                {
                    return spawnPoint.position;
                }
            }
        }

        // Try random positions within arena bounds
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            Vector3 candidatePosition = GenerateRandomPositionInArena(bounds);
            if (IsPositionValidInArena(candidatePosition, arenaIndex))
            {
                return candidatePosition;
            }
        }

        return Vector3.zero;
    }

    private Vector3 GenerateRandomPositionInArena(ArenaBounds bounds)
    {
        float x = Random.Range(bounds.minX, bounds.maxX);
        float z = Random.Range(bounds.minZ, bounds.maxZ);
        return new Vector3(x, bounds.center.y, z);
    }

    private bool IsPositionValidInArena(Vector3 position, int arenaIndex)
    {
        var bounds = arenaBounds[arenaIndex];

        // Check arena bounds
        if (position.x < bounds.minX || position.x > bounds.maxX ||
            position.z < bounds.minZ || position.z > bounds.maxZ)
            return false;

        // Check for obstacles
        if (Physics.CheckSphere(position, 1f, spawnCollisionLayers))
            return false;

        // Check distance from existing targets in this arena
        foreach (GameObject target in arenaTargets[arenaIndex])
        {
            if (target != null && Vector3.Distance(position, target.transform.position) < minSpawnDistance)
            {
                return false;
            }
        }

        return true;
    }

    private GameObject CreateTargetAtPosition(Vector3 position, int arenaIndex)
    {
        var arena = arenas[arenaIndex];
        Transform parent = arena.spawnParent != null ? arena.spawnParent : transform;
        
        GameObject newTarget = Instantiate(trainingTargetPrefab, position, Quaternion.identity, parent);
        arenaTargets[arenaIndex].Add(newTarget);
        return newTarget;
    }

    private void ConfigureNewTarget(GameObject target)
    {
        var playerComponent = target.GetComponent<RL_Player>();
        if (playerComponent != null)
        {
            playerComponent.isRL_TrainingTarget = true;
            playerComponent.spawner = this;
        }

        var lifeTracker = target.GetComponent<RL_TrainingTarget>();
        if (lifeTracker == null)
        {
            lifeTracker = target.AddComponent<RL_TrainingTarget>();
        }
        lifeTracker.Initialize(this);
    }

    private void PlaySpawnEffect(Vector3 position)
    {
        if (spawnParticlePrefab != null)
        {
            Instantiate(spawnParticlePrefab, position, Quaternion.identity);
        }
    }

    private void PlayEpisodeStartEffect()
    {
        if (episodeStartPrefab != null)
        {
            Instantiate(episodeStartPrefab, transform.position, Quaternion.identity);
        }
    }

    private ArenaBounds CalculateArenaBounds(ArenaConfiguration arena)
    {
        if (arena.corner1 == null || arena.corner2 == null || arena.corner3 == null || arena.corner4 == null)
        {
            // Fallback to spawner position if corners not set
            return new ArenaBounds
            {
                minX = transform.position.x - 5f,
                maxX = transform.position.x + 5f,
                minZ = transform.position.z - 5f,
                maxZ = transform.position.z + 5f,
                center = transform.position
            };
        }

        float minX = Mathf.Min(arena.corner1.position.x, arena.corner2.position.x, arena.corner3.position.x, arena.corner4.position.x);
        float maxX = Mathf.Max(arena.corner1.position.x, arena.corner2.position.x, arena.corner3.position.x, arena.corner4.position.x);
        float minZ = Mathf.Min(arena.corner1.position.z, arena.corner2.position.z, arena.corner3.position.z, arena.corner4.position.z);
        float maxZ = Mathf.Max(arena.corner1.position.z, arena.corner2.position.z, arena.corner3.position.z, arena.corner4.position.z);

        return new ArenaBounds
        {
            minX = minX,
            maxX = maxX,
            minZ = minZ,
            maxZ = maxZ,
            center = new Vector3((minX + maxX) / 2f, arena.corner1.position.y, (minZ + maxZ) / 2f)
        };
    }

    private int FindTargetArena(GameObject target)
    {
        for (int i = 0; i < arenas.Length; i++)
        {
            if (arenaTargets[i].Contains(target))
            {
                return i;
            }
        }
        return -1;
    }

    private void RemoveTargetFromArena(GameObject target, int arenaIndex)
    {
        arenaTargets[arenaIndex].Remove(target);
    }

    private void DestroyAllTargets()
    {
        if (debugSpawning)
        {
            int totalCount = 0;
            foreach (var kvp in arenaTargets)
            {
                totalCount += kvp.Value.Count;
            }
            Debug.Log($"[{gameObject.name}] Destroying {totalCount} total targets across all arenas");
        }

        foreach (var kvp in arenaTargets)
        {
            DestroyTargetList(kvp.Value);
        }
    }

    private void DestroyOldestTargetInArena(int arenaIndex)
    {
        var targets = arenaTargets[arenaIndex];
        if (targets.Count > 0)
        {
            GameObject oldest = targets[0];
            targets.RemoveAt(0);

            if (oldest != null)
            {
                var targetComponent = oldest.GetComponent<RL_TrainingTarget>();
                if (targetComponent != null)
                {
                    targetComponent.enabled = false;
                }
                DestroyImmediate(oldest);
            }
        }
    }

    private void DestroyTargetList(List<GameObject> targets)
    {
        foreach (GameObject target in targets)
        {
            if (target != null)
            {
                var targetComponent = target.GetComponent<RL_TrainingTarget>();
                if (targetComponent != null)
                {
                    targetComponent.enabled = false;
                }
                DestroyImmediate(target);
            }
        }
        targets.Clear();
    }

    private void CheckForEpisodeEnd()
    {
        // Check if all arenas are empty when they should have targets
        bool hasAnyTargets = false;
        bool shouldHaveTargets = false;

        for (int i = 0; i < arenas.Length; i++)
        {
            if (arenas[i].maxTargets > 0)
            {
                shouldHaveTargets = true;
                if (arenaTargets[i].Count > 0)
                {
                    hasAnyTargets = true;
                    break;
                }
            }
        }

        if (!hasAnyTargets && shouldHaveTargets && episodeActive)
        {
            EndCurrentEpisode();
        }
    }

    private void EndCurrentEpisode()
    {
        episodeActive = false;
        UpdateArenaVisuals();

        if (debugSpawning)
        {
            Debug.Log($"[{gameObject.name}] Episode ended");
        }
    }

    private void UpdateArenaVisuals()
    {
        if (arenaLight != null)
        {
            bool hasAnyTargets = false;
            foreach (var kvp in arenaTargets)
            {
                if (kvp.Value.Count > 0)
                {
                    hasAnyTargets = true;
                    break;
                }
            }

            arenaLight.color = (episodeActive && hasAnyTargets) ? activeColor : inactiveColor;
        }
    }

    #endregion

    #region Public Utility Methods

    public static void AddReward(float reward)
    {
        OnRewardAdded?.Invoke(reward);
    }

    public int GetTotalActiveTargetCount()
    {
        int total = 0;
        foreach (var kvp in arenaTargets)
        {
            // Clean up null references
            kvp.Value.RemoveAll(target => target == null);
            total += kvp.Value.Count;
        }
        return total;
    }

    public int GetActiveTargetCountInArena(int arenaIndex)
    {
        if (arenaIndex < 0 || arenaIndex >= arenas.Length) return 0;
        
        arenaTargets[arenaIndex].RemoveAll(target => target == null);
        return arenaTargets[arenaIndex].Count;
    }

    public bool IsSpawningEnabled()
    {
        return isSpawningEnabled;
    }

    public bool IsEpisodeActive()
    {
        return episodeActive;
    }

    public bool IsInitialized()
    {
        return isInitialized;
    }

    public void SetMaxTargets(int newMax)
    {
        for (int i = 0; i < arenas.Length; i++)
        {
            SetMaxTargetsForArena(i, newMax);
        }
    }

    public int GetMaxTargets()
    {
        if (arenas.Length == 0) return 0;
        return arenas[0].maxTargets; // Assumes same max for all arenas
    }

    public int GetActiveTargetCount()
    {
        return GetTotalActiveTargetCount();
    }

    #endregion

    #region Debug Visualization

    private void OnDrawGizmosSelected()
    {
        if (arenas == null) return;

        for (int i = 0; i < arenas.Length; i++)
        {
            var arena = arenas[i];
            
            // Draw arena boundaries
            Gizmos.color = Color.cyan;
            if (arenaBounds.ContainsKey(i))
            {
                var bounds = arenaBounds[i];
                Vector3 size = new Vector3(bounds.maxX - bounds.minX, 2f, bounds.maxZ - bounds.minZ);
                Gizmos.DrawWireCube(bounds.center, size);
                
                // Draw arena index
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(bounds.center + Vector3.up * 3f, 0.3f);
            }

            // Draw custom spawn points
            if (arena.customSpawnPoints != null)
            {
                Gizmos.color = Color.yellow;
                foreach (var spawnPoint in arena.customSpawnPoints)
                {
                    if (spawnPoint != null)
                    {
                        Gizmos.DrawWireSphere(spawnPoint.position, 0.5f);
                    }
                }
            }

            // Draw active targets
            if (Application.isPlaying && arenaTargets.ContainsKey(i))
            {
                arenaTargets[i].RemoveAll(target => target == null);
                Gizmos.color = Color.green;
                foreach (var target in arenaTargets[i])
                {
                    if (target != null)
                    {
                        Gizmos.DrawLine(arenaBounds[i].center, target.transform.position);
                    }
                }
            }
        }
    }

    #endregion
}