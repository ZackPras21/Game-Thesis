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
    [SerializeField] private ArenaConfiguration[] arenas;
    [SerializeField] private int maxSpawnAttempts = 10;
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private LayerMask spawnCollisionLayers;
    [SerializeField] private bool enableContinuousSpawning = false;
    [SerializeField] private float minimumSpawnDistance = 2f;

    [Header("Visual Effects")]
    [SerializeField] private GameObject spawnParticlePrefab;
    [SerializeField] private GameObject episodeStartPrefab;
    [SerializeField] private Light arenaLight;
    [SerializeField] private Color activeColor = Color.green;
    [SerializeField] private Color inactiveColor = Color.red;

    [Header("Debug")]
    [SerializeField] private bool debugSpawning = true;

    private Dictionary<int, List<GameObject>> arenaTargets = new Dictionary<int, List<GameObject>>();
    private Dictionary<int, ArenaBounds> arenaBounds = new Dictionary<int, ArenaBounds>();
    private float lastSpawnTime;
    private bool episodeActive = false;
    private bool spawningEnabled = true;
    private bool initialized = false;

    public static System.Action<float> OnRewardAdded;

    private struct ArenaBounds
    {
        public float minX, maxX, minZ, maxZ;
        public Vector3 center;
    }

    private void Start()
    {
        Initialize();
    }

    private void Update()
    {
        if (ShouldHandleContinuousSpawning())
        {
            HandleContinuousSpawning();
        }
    }

    private void Initialize()
    {
        LogInitialization();
        InitializeArenaData();
        ResetTimingAndState();
        UpdateVisuals();
        initialized = true;
        LogArenaConfiguration();
    }

    private void InitializeArenaData()
    {
        for (int i = 0; i < arenas.Length; i++)
        {
            arenaTargets[i] = new List<GameObject>();
            arenaBounds[i] = CalculateArenaBounds(arenas[i]);
        }
    }

    private void ResetTimingAndState()
    {
        lastSpawnTime = Time.time;
        episodeActive = false;
    }

    public void ResetArena()
    {
        LogArenaReset();
        DestroyAllTargets();
        PlayEpisodeStartEffect();
        StartNewEpisode();
        
        if (spawningEnabled)
        {
            SpawnInitialTargetsInAllArenas();
        }
    }

    public void SetSpawningEnabled(bool enabled)
    {
        bool previousState = spawningEnabled;
        spawningEnabled = enabled;
        LogSpawningStateChange(previousState, enabled);

        if (!enabled && previousState)
        {
            DestroyAllTargets();
            episodeActive = false;
        }

        UpdateVisuals();
    }

    public void SetMaxTargetsForArena(int arenaIndex, int newMax)
    {
        if (!IsValidArenaIndex(arenaIndex)) return;

        int oldMax = arenas[arenaIndex].maxTargets;
        arenas[arenaIndex].maxTargets = Mathf.Max(0, newMax);
        LogMaxTargetsChange(arenaIndex, oldMax, newMax);

        RemoveExcessTargetsFromArena(arenaIndex, newMax);
    }

    public void SpawnTargetsManuallyInArena(int arenaIndex, int count)
    {
        if (!CanSpawnInArena(arenaIndex))
        {
            LogManualSpawnFailure(arenaIndex);
            return;
        }

        var arena = arenas[arenaIndex];
        if (arena.maxTargets <= 0) return;

        count = Mathf.Clamp(count, 0, arena.maxTargets - arenaTargets[arenaIndex].Count);
        LogManualSpawn(arenaIndex, count);

        for (int i = 0; i < count; i++)
        {
            SpawnSingleTargetInArena(arenaIndex);
        }
    }

    public void OnTargetDestroyed(GameObject target)
    {
        int arenaIndex = FindTargetArena(target);
        if (arenaIndex == -1) return;

        LogTargetDestroyed(arenaIndex);
        RemoveTargetFromArena(target, arenaIndex);
        UpdateVisuals();

        if (ShouldSpawnReplacement(arenaIndex))
        {
            SpawnSingleTargetInArena(arenaIndex);
        }

        CheckForEpisodeEnd();
    }

    private bool ShouldHandleContinuousSpawning()
    {
        return initialized && enableContinuousSpawning && episodeActive && spawningEnabled;
    }

    private void HandleContinuousSpawning()
    {
        if (Time.time < lastSpawnTime + spawnInterval) return;

        for (int i = 0; i < arenas.Length; i++)
        {
            if (CanSpawnMoreInArena(i) && SpawnSingleTargetInArena(i))
            {
                lastSpawnTime = Time.time;
                break;
            }
        }
    }

    private void StartNewEpisode()
    {
        episodeActive = true;
        lastSpawnTime = Time.time - spawnInterval;
        UpdateVisuals();
        LogEpisodeStart();
    }

    private void SpawnInitialTargetsInAllArenas()
    {
        LogInitialTargetSpawning();

        for (int arenaIndex = 0; arenaIndex < arenas.Length; arenaIndex++)
        {
            SpawnInitialTargetsInArena(arenaIndex);
        }
    }

    private void SpawnInitialTargetsInArena(int arenaIndex)
    {
        var arena = arenas[arenaIndex];
        if (arena.maxTargets <= 0) return;

        int spawnedCount = 0;
        for (int i = 0; i < arena.maxTargets; i++)
        {
            if (SpawnSingleTargetInArena(arenaIndex))
            {
                spawnedCount++;
            }
            else
            {
                LogSpawnFailure(arenaIndex, i + 1, arena.maxTargets);
                break;
            }
        }

        LogArenaSpawnResults(arenaIndex, spawnedCount, arena.maxTargets);
    }

    private bool SpawnSingleTargetInArena(int arenaIndex)
    {
        if (!CanSpawnMoreInArena(arenaIndex)) return false;

        Vector3 spawnPosition = GetValidSpawnPosition(arenaIndex);
        if (spawnPosition == Vector3.zero) return false;

        GameObject newTarget = CreateTargetAtPosition(spawnPosition, arenaIndex);
        ConfigureTarget(newTarget);
        PlaySpawnEffect(spawnPosition);
        UpdateVisuals();

        LogTargetSpawned(arenaIndex, spawnPosition);
        return true;
    }

    private bool CanSpawnMoreInArena(int arenaIndex)
    {
        if (!IsValidArenaIndex(arenaIndex)) return false;
        
        var arena = arenas[arenaIndex];
        return initialized &&
               spawningEnabled &&
               episodeActive &&
               arena.maxTargets > 0 &&
               arenaTargets[arenaIndex].Count < arena.maxTargets &&
               trainingTargetPrefab != null;
    }

    private bool CanSpawnInArena(int arenaIndex)
    {
        return spawningEnabled && IsValidArenaIndex(arenaIndex);
    }

    private bool ShouldSpawnReplacement(int arenaIndex)
    {
        return enableContinuousSpawning && CanSpawnMoreInArena(arenaIndex);
    }

    private Vector3 GetValidSpawnPosition(int arenaIndex)
    {
        var arena = arenas[arenaIndex];
        var bounds = arenaBounds[arenaIndex];

        Vector3 position = TryCustomSpawnPoints(arena, arenaIndex);
        if (position != Vector3.zero) return position;

        return TryRandomPositions(bounds, arenaIndex);
    }

    private Vector3 TryCustomSpawnPoints(ArenaConfiguration arena, int arenaIndex)
    {
        if (arena.customSpawnPoints == null || arena.customSpawnPoints.Length == 0)
            return Vector3.zero;

        foreach (var spawnPoint in arena.customSpawnPoints)
        {
            if (spawnPoint != null && IsValidPosition(spawnPoint.position, arenaIndex))
            {
                return spawnPoint.position;
            }
        }
        return Vector3.zero;
    }

    private Vector3 TryRandomPositions(ArenaBounds bounds, int arenaIndex)
    {
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            Vector3 candidatePosition = GenerateRandomPosition(bounds);
            if (IsValidPosition(candidatePosition, arenaIndex))
            {
                if (debugSpawning) Debug.Log($"[{gameObject.name}] Found valid position at {candidatePosition} on attempt {attempt + 1}");
                return candidatePosition;
            }
            else if (debugSpawning)
            {
                Debug.LogWarning($"[{gameObject.name}] Invalid position at {candidatePosition} - " +
                    $"Bounds: {IsWithinArenaBounds(candidatePosition, arenaIndex)}, " +
                    $"Collision: {!HasCollisionAtPosition(candidatePosition)}, " +
                    $"Distance: {HasSufficientDistanceFromTargets(candidatePosition, arenaIndex)}");
            }
        }
        if (debugSpawning) Debug.LogError($"[{gameObject.name}] Failed to find valid position after {maxSpawnAttempts} attempts");
        return Vector3.zero;
    }

    private Vector3 GenerateRandomPosition(ArenaBounds bounds)
    {
        float x = Random.Range(bounds.minX, bounds.maxX);
        float z = Random.Range(bounds.minZ, bounds.maxZ);
        return new Vector3(x, bounds.center.y, z);
    }

    private bool IsValidPosition(Vector3 position, int arenaIndex)
    {
        bool inBounds = IsWithinArenaBounds(position, arenaIndex);
        bool noCollision = !HasCollisionAtPosition(position);
        bool sufficientDistance = HasSufficientDistanceFromTargets(position, arenaIndex);
        
        if (debugSpawning && !inBounds)
            Debug.LogWarning($"[{gameObject.name}] Position {position} out of bounds for arena {arenaIndex}");
        if (debugSpawning && !noCollision)
            Debug.LogWarning($"[{gameObject.name}] Collision detected at {position}");
        if (debugSpawning && !sufficientDistance)
            Debug.LogWarning($"[{gameObject.name}] Insufficient distance from other targets at {position}");

        return inBounds && noCollision && sufficientDistance;
    }

    private bool IsWithinArenaBounds(Vector3 position, int arenaIndex)
    {
        var bounds = arenaBounds[arenaIndex];
        return position.x >= bounds.minX && position.x <= bounds.maxX &&
               position.z >= bounds.minZ && position.z <= bounds.maxZ;
    }

    private bool HasCollisionAtPosition(Vector3 position)
    {
        // Use OverlapSphere with a small radius to avoid false positives
        Collider[] colliders = Physics.OverlapSphere(position, 0.5f, spawnCollisionLayers);
        foreach (var collider in colliders)
        {
            // Ignore triggers and the player's own collider
            if (!collider.isTrigger && collider.gameObject != gameObject)
            {
                return true;
            }
        }
        return false;
    }

    private bool HasSufficientDistanceFromTargets(Vector3 position, int arenaIndex)
    {
        foreach (GameObject target in arenaTargets[arenaIndex])
        {
            if (target != null && Vector3.Distance(position, target.transform.position) < minimumSpawnDistance)
            {
                return false;
            }
        }
        return true;
    }

    private GameObject CreateTargetAtPosition(Vector3 position, int arenaIndex)
    {
        if (this == null || !gameObject.scene.isLoaded)
        {
            Debug.LogWarning("[Spawner] Cannot spawn target - spawner destroyed or scene unloading");
            return null;
        }

        var arena = arenas[arenaIndex];
        Transform parent = null;
        if (arena.spawnParent != null && arena.spawnParent.gameObject.scene.isLoaded)
        {
            parent = arena.spawnParent;
        }

        GameObject newTarget = Instantiate(trainingTargetPrefab, position, Quaternion.identity, parent);
        if (newTarget != null)
        {
            arenaTargets[arenaIndex].Add(newTarget);
            newTarget.hideFlags = HideFlags.DontSave; // Prevent scene persistence
        }
        return newTarget;
    }

    private void ConfigureTarget(GameObject target)
    {
        ConfigurePlayerComponent(target);
        ConfigureLifeTracker(target);
    }

    private void ConfigurePlayerComponent(GameObject target)
    {
        var playerComponent = target.GetComponent<RL_Player>();
        if (playerComponent != null)
        {
            playerComponent.isRL_TrainingTarget = true;
            playerComponent.spawner = this;
        }
    }

    private void ConfigureLifeTracker(GameObject target)
    {
        var lifeTracker = target.GetComponent<RL_TrainingTarget>();
        if (lifeTracker == null)
        {
            lifeTracker = target.AddComponent<RL_TrainingTarget>();
        }
        lifeTracker.Initialize(this);
    }

    private void PlaySpawnEffect(Vector3 position)
    {
        if (spawnParticlePrefab != null && gameObject.scene.isLoaded)
        {
            var effect = Instantiate(spawnParticlePrefab, position, Quaternion.identity);
            if (effect != null)
            {
                var ps = effect.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    var main = ps.main;
                    main.stopAction = ParticleSystemStopAction.Destroy;
                }
                effect.hideFlags = HideFlags.DontSave;
            }
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
        if (HasValidCorners(arena))
        {
            return CalculateBoundsFromCorners(arena);
        }
        
        return CreateFallbackBounds();
    }

    private bool HasValidCorners(ArenaConfiguration arena)
    {
        return arena.corner1 != null && arena.corner2 != null && 
               arena.corner3 != null && arena.corner4 != null;
    }

    private ArenaBounds CalculateBoundsFromCorners(ArenaConfiguration arena)
    {
        float minX = Mathf.Min(arena.corner1.position.x, arena.corner2.position.x, 
                              arena.corner3.position.x, arena.corner4.position.x);
        float maxX = Mathf.Max(arena.corner1.position.x, arena.corner2.position.x, 
                              arena.corner3.position.x, arena.corner4.position.x);
        float minZ = Mathf.Min(arena.corner1.position.z, arena.corner2.position.z, 
                              arena.corner3.position.z, arena.corner4.position.z);
        float maxZ = Mathf.Max(arena.corner1.position.z, arena.corner2.position.z, 
                              arena.corner3.position.z, arena.corner4.position.z);

        return new ArenaBounds
        {
            minX = minX,
            maxX = maxX,
            minZ = minZ,
            maxZ = maxZ,
            center = new Vector3((minX + maxX) / 2f, arena.corner1.position.y, (minZ + maxZ) / 2f)
        };
    }

    private ArenaBounds CreateFallbackBounds()
    {
        return new ArenaBounds
        {
            minX = transform.position.x - 5f,
            maxX = transform.position.x + 5f,
            minZ = transform.position.z - 5f,
            maxZ = transform.position.z + 5f,
            center = transform.position
        };
    }

    private int FindTargetArena(GameObject target)
    {
        if (target == null || arenaTargets == null || arenas == null)
            return -1;

        for (int i = 0; i < arenas.Length; i++)
        {
            if (arenaTargets.ContainsKey(i) && arenaTargets[i].Contains(target))
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

    private void RemoveExcessTargetsFromArena(int arenaIndex, int newMax)
    {
        if (arenaTargets[arenaIndex].Count > newMax)
        {
            int excessCount = arenaTargets[arenaIndex].Count - newMax;
            for (int i = 0; i < excessCount; i++)
            {
                DestroyOldestTargetInArena(arenaIndex);
            }
        }
    }

    private void DestroyAllTargets()
    {
        LogTotalTargetDestruction();

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
            DestroyTarget(oldest);
        }
    }

    private void DestroyTargetList(List<GameObject> targets)
    {
        if (targets == null || targets.Count == 0) return;
        
        // Iterate backwards to avoid index shifting when removing elements
        for (int i = targets.Count - 1; i >= 0; i--)
        {
            DestroyTarget(targets[i]);
        }
        targets.Clear();
    }

    private void DestroyTarget(GameObject target)
    {
        if (target == null) return;

        try
        {
            // Disable components before destruction
            var targetComponent = target.GetComponent<RL_TrainingTarget>();
            if (targetComponent != null)
            {
                targetComponent.enabled = false;
            }

            var playerComponent = target.GetComponent<RL_Player>();
            if (playerComponent != null)
            {
                playerComponent.enabled = false;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Spawner] Error destroying target: {e.Message}");
        }
    }

    private void OnDestroy()
    {
        if (arenaTargets == null) return;

        foreach (var kvp in arenaTargets)
        {
            if (kvp.Value == null) continue;
            
            // Destroy targets in reverse order
            for (int i = kvp.Value.Count - 1; i >= 0; i--)
            {
                if (kvp.Value[i] != null)
                {
                    DestroyTarget(kvp.Value[i]);
                }
            }
            kvp.Value.Clear();
        }
    }

    private void CheckForEpisodeEnd()
    {
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
        UpdateVisuals();
        LogEpisodeEnd();
    }

    private void UpdateVisuals()
    {
        if (arenaLight != null)
        {
            bool hasAnyTargets = HasAnyActiveTargets();
            arenaLight.color = (episodeActive && hasAnyTargets) ? activeColor : inactiveColor;
        }
    }

    private bool HasAnyActiveTargets()
    {
        if (arenaTargets == null) return false;

        foreach (var kvp in arenaTargets)
        {
            if (kvp.Value != null && kvp.Value.Count > 0)
            {
                return true;
            }
        }
        return false;
    }

    private bool IsValidArenaIndex(int arenaIndex)
    {
        return arenaIndex >= 0 && arenaIndex < arenas.Length;
    }

    public static void AddReward(float reward)
    {
        OnRewardAdded?.Invoke(reward);
    }

    public int GetTotalActiveTargetCount()
    {
        if (arenaTargets == null) return 0;

        int total = 0;
        foreach (var kvp in arenaTargets)
        {
            if (kvp.Value != null)
            {
                kvp.Value.RemoveAll(target => target == null);
                total += kvp.Value.Count;
            }
        }
        return total;
    }

    public int GetActiveTargetCountInArena(int arenaIndex)
    {
        if (!IsValidArenaIndex(arenaIndex)) return 0;
        
        arenaTargets[arenaIndex].RemoveAll(target => target == null);
        return arenaTargets[arenaIndex].Count;
    }

    public bool IsSpawningEnabled() => spawningEnabled;
    public bool IsEpisodeActive() => episodeActive;
    public bool IsInitialized() => initialized;

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
        return arenas[0].maxTargets;
    }

    public int GetActiveTargetCount() => GetTotalActiveTargetCount();

    #region Debug Logging

    private void LogInitialization()
    {
        if (debugSpawning)
        {
            Debug.Log($"[{gameObject.name}] Initializing spawner with {arenas.Length} arenas");
        }
    }

    private void LogArenaConfiguration()
    {
        if (!debugSpawning) return;

        for (int i = 0; i < arenas.Length; i++)
        {
            Debug.Log($"Arena {i}: MaxTargets={arenas[i].maxTargets}, Bounds={arenaBounds[i].center}");
        }
    }

    private void LogArenaReset()
    {
        if (debugSpawning)
        {
            Debug.Log($"[{gameObject.name}] ResetArena called - Resetting all {arenas.Length} arenas");
        }
    }

    private void LogSpawningStateChange(bool previousState, bool newState)
    {
        if (debugSpawning)
        {
            Debug.Log($"[{gameObject.name}] Spawning enabled changed: {previousState} -> {newState}");
        }
    }

    private void LogMaxTargetsChange(int arenaIndex, int oldMax, int newMax)
    {
        if (debugSpawning)
        {
            Debug.Log($"[{gameObject.name}] Arena {arenaIndex} max targets changed: {oldMax} -> {newMax}");
        }
    }

    private void LogManualSpawn(int arenaIndex, int count)
    {
        if (debugSpawning)
        {
            Debug.Log($"[{gameObject.name}] Manual spawn in Arena {arenaIndex}: {count} targets");
        }
    }

    private void LogManualSpawnFailure(int arenaIndex)
    {
        if (debugSpawning)
        {
            Debug.LogWarning($"[{gameObject.name}] Cannot spawn manually in arena {arenaIndex}");
        }
    }

    private void LogTargetDestroyed(int arenaIndex)
    {
        if (debugSpawning)
        {
            Debug.Log($"[{gameObject.name}] Target destroyed in Arena {arenaIndex}");
        }
    }

    private void LogEpisodeStart()
    {
        if (debugSpawning)
        {
            Debug.Log($"[{gameObject.name}] Episode started");
        }
    }

    private void LogEpisodeEnd()
    {
        if (debugSpawning)
        {
            Debug.Log($"[{gameObject.name}] Episode ended");
        }
    }

    private void LogInitialTargetSpawning()
    {
        if (debugSpawning)
        {
            Debug.Log($"[{gameObject.name}] Spawning initial targets in all arenas");
        }
    }

    private void LogSpawnFailure(int arenaIndex, int current, int max)
    {
        if (debugSpawning)
        {
            Debug.LogWarning($"[{gameObject.name}] Failed to spawn target {current}/{max} in Arena {arenaIndex}");
        }
    }

    private void LogArenaSpawnResults(int arenaIndex, int spawned, int max)
    {
        if (debugSpawning)
        {
            Debug.Log($"[{gameObject.name}] Arena {arenaIndex}: Spawned {spawned}/{max} targets");
        }
    }

    private void LogTargetSpawned(int arenaIndex, Vector3 position)
    {
        if (debugSpawning)
        {
            Debug.Log($"[{gameObject.name}] Target spawned in Arena {arenaIndex} at {position}");
        }
    }

    private void LogTotalTargetDestruction()
    {
        if (!debugSpawning) return;

        int totalCount = 0;
        foreach (var kvp in arenaTargets)
        {
            totalCount += kvp.Value.Count;
        }
        Debug.Log($"[{gameObject.name}] Destroying {totalCount} total targets across all arenas");
    }

    #endregion

    #region Debug Visualization

    private void OnDrawGizmosSelected()
    {
        if (arenas == null) return;

        for (int i = 0; i < arenas.Length; i++)
        {
            DrawArenaGizmos(i);
        }
    }

    private void DrawArenaGizmos(int arenaIndex)
    {
        var arena = arenas[arenaIndex];
        
        DrawArenaBoundaries(arenaIndex);
        DrawCustomSpawnPoints(arena);
        DrawActiveTargets(arenaIndex);
    }

    private void DrawArenaBoundaries(int arenaIndex)
    {
        Gizmos.color = Color.cyan;
        if (arenaBounds.ContainsKey(arenaIndex))
        {
            var bounds = arenaBounds[arenaIndex];
            Vector3 size = new Vector3(bounds.maxX - bounds.minX, 2f, bounds.maxZ - bounds.minZ);
            Gizmos.DrawWireCube(bounds.center, size);
            
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(bounds.center + Vector3.up * 3f, 0.3f);
        }
    }

    private void DrawCustomSpawnPoints(ArenaConfiguration arena)
    {
        if (arena.customSpawnPoints == null) return;

        Gizmos.color = Color.yellow;
        foreach (var spawnPoint in arena.customSpawnPoints)
        {
            if (spawnPoint != null)
            {
                Gizmos.DrawWireSphere(spawnPoint.position, 0.5f);
            }
        }
    }

    private void DrawActiveTargets(int arenaIndex)
    {
        if (!Application.isPlaying || !arenaTargets.ContainsKey(arenaIndex)) return;

        arenaTargets[arenaIndex].RemoveAll(target => target == null);
        Gizmos.color = Color.green;
        foreach (var target in arenaTargets[arenaIndex])
        {
            if (target != null)
            {
                Gizmos.DrawLine(arenaBounds[arenaIndex].center, target.transform.position);
            }
        }
    }

    #endregion
}