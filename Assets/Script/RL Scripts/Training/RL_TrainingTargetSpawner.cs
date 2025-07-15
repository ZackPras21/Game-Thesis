using System.Collections.Generic;
using UnityEngine;

public class RL_TrainingTargetSpawner : MonoBehaviour
{
    [System.Serializable]
    public struct ArenaConfiguration
    {
        [Header("Arena Boundaries")]
        public Transform corner1, corner2, corner3, corner4;
        
        [Header("Target Configuration")]
        public int maxTargets;
        
        [Header("Organization")]
        public Transform spawnParent;
        
        [Header("Spawn Points (Optional)")]
        public Transform[] customSpawnPoints;
    }

    private struct ArenaBounds
    {
        public float minX, maxX, minZ, maxZ;
        public Vector3 center;
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

    // Public Properties
    public bool IsSpawningEnabled() => spawningEnabled;
    public bool IsEpisodeActive() => episodeActive;
    public bool IsInitialized() => initialized;
    public int GetTotalActiveTargetCount() => GetTargetCount();
    public int GetActiveTargetCount() => GetTotalActiveTargetCount();
    public int GetMaxTargets() => arenas.Length > 0 ? arenas[0].maxTargets : 0;

    private void Start() => Initialize();

    private void Update()
    {
        if (ShouldHandleContinuousSpawning())
            HandleContinuousSpawning();
    }

    private void Initialize()
    {
        LogAction("Initializing spawner", $"with {arenas.Length} arenas");
        
        for (int i = 0; i < arenas.Length; i++)
        {
            arenaTargets[i] = new List<GameObject>();
            arenaBounds[i] = CalculateArenaBounds(arenas[i]);
        }

        lastSpawnTime = Time.time;
        episodeActive = false;
        initialized = true;
        UpdateVisuals();
        LogArenaConfigurations();
    }

    public void ResetArena()
    {
        LogAction("ResetArena", $"Resetting all {arenas.Length} arenas");
        
        DestroyAllTargets();
        PlayEffect(episodeStartPrefab, transform.position);
        StartNewEpisode();
        
        if (spawningEnabled)
            SpawnInitialTargetsInAllArenas();
    }

    public void SetSpawningEnabled(bool enabled)
    {
        bool previousState = spawningEnabled;
        spawningEnabled = enabled;
        LogAction("Spawning state changed", $"{previousState} -> {enabled}");

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
        LogAction($"Arena {arenaIndex} max targets changed", $"{oldMax} -> {newMax}");
        
        RemoveExcessTargetsFromArena(arenaIndex, newMax);
    }

    public void SetMaxTargets(int newMax)
    {
        for (int i = 0; i < arenas.Length; i++)
            SetMaxTargetsForArena(i, newMax);
    }

    public void SpawnTargetsManuallyInArena(int arenaIndex, int count)
    {
        if (!CanSpawnInArena(arenaIndex))
        {
            LogAction($"Cannot spawn manually in arena {arenaIndex}", "spawning disabled or invalid arena");
            return;
        }

        var arena = arenas[arenaIndex];
        if (arena.maxTargets <= 0) return;

        count = Mathf.Clamp(count, 0, arena.maxTargets - arenaTargets[arenaIndex].Count);
        LogAction($"Manual spawn in Arena {arenaIndex}", $"{count} targets");

        for (int i = 0; i < count; i++)
            SpawnSingleTargetInArena(arenaIndex);
    }

    public void OnTargetDestroyed(GameObject target)
    {
        int arenaIndex = FindTargetArena(target);
        if (arenaIndex == -1) return;

        LogAction($"Target destroyed in Arena {arenaIndex}", "");
        RemoveTargetFromArena(target, arenaIndex);
        UpdateVisuals();

        if (ShouldSpawnReplacement(arenaIndex))
            SpawnSingleTargetInArena(arenaIndex);

        CheckForEpisodeEnd();
    }

    public int GetActiveTargetCountInArena(int arenaIndex)
    {
        if (!IsValidArenaIndex(arenaIndex)) return 0;
        
        arenaTargets[arenaIndex].RemoveAll(target => target == null);
        return arenaTargets[arenaIndex].Count;
    }

    public static void AddReward(float reward) => OnRewardAdded?.Invoke(reward);

    private bool ShouldHandleContinuousSpawning() => 
        initialized && enableContinuousSpawning && episodeActive && spawningEnabled;

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
        LogAction("Episode started", "");
    }

    private void SpawnInitialTargetsInAllArenas()
    {
        LogAction("Spawning initial targets", "in all arenas");

        for (int arenaIndex = 0; arenaIndex < arenas.Length; arenaIndex++)
        {
            var arena = arenas[arenaIndex];
            if (arena.maxTargets <= 0) continue;

            int spawnedCount = 0;
            int targetsToSpawn = Mathf.Min(arena.maxTargets, arena.customSpawnPoints?.Length ?? arena.maxTargets);
            
            for (int i = 0; i < targetsToSpawn; i++)
            {
                if (SpawnSingleTargetInArena(arenaIndex))
                    spawnedCount++;
                else
                {
                    LogAction($"Failed to spawn target {i + 1}/{targetsToSpawn}", $"in Arena {arenaIndex}");
                    break;
                }
            }

            LogAction($"Arena {arenaIndex}", $"Spawned {spawnedCount}/{targetsToSpawn} targets");
        }
    }

    private bool SpawnSingleTargetInArena(int arenaIndex)
    {
        if (!CanSpawnMoreInArena(arenaIndex)) return false;

        Vector3 spawnPosition = GetValidSpawnPosition(arenaIndex);
        if (spawnPosition == Vector3.zero) return false;

        GameObject newTarget = CreateTargetAtPosition(spawnPosition, arenaIndex);
        if (newTarget == null) return false;

        ConfigureTarget(newTarget);
        PlayEffect(spawnParticlePrefab, spawnPosition);
        UpdateVisuals();
        LogAction($"Target spawned in Arena {arenaIndex}", $"at {spawnPosition}");
        
        return true;
    }

    private bool CanSpawnMoreInArena(int arenaIndex)
    {
        if (!IsValidArenaIndex(arenaIndex)) return false;
        
        var arena = arenas[arenaIndex];
        return initialized && spawningEnabled && episodeActive && 
               arena.maxTargets > 0 && arenaTargets[arenaIndex].Count < arena.maxTargets && 
               trainingTargetPrefab != null;
    }

    private bool CanSpawnInArena(int arenaIndex) => spawningEnabled && IsValidArenaIndex(arenaIndex);

    private bool ShouldSpawnReplacement(int arenaIndex) => 
        enableContinuousSpawning && CanSpawnMoreInArena(arenaIndex);

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
                return spawnPoint.position;
        }
        return Vector3.zero;
    }

    private Vector3 TryRandomPositions(ArenaBounds bounds, int arenaIndex)
    {
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            Vector3 candidatePosition = new Vector3(
                Random.Range(bounds.minX, bounds.maxX),
                bounds.center.y,
                Random.Range(bounds.minZ, bounds.maxZ)
            );

            if (IsValidPosition(candidatePosition, arenaIndex))
            {
                if (debugSpawning) 
                    Debug.Log($"[{gameObject.name}] Found valid position at {candidatePosition} on attempt {attempt + 1}");
                return candidatePosition;
            }
        }
        
        if (debugSpawning) 
            Debug.LogError($"[{gameObject.name}] Failed to find valid position after {maxSpawnAttempts} attempts");
        return Vector3.zero;
    }

    private bool IsValidPosition(Vector3 position, int arenaIndex)
    {
        return IsWithinArenaBounds(position, arenaIndex) && 
               !HasCollisionAtPosition(position) && 
               HasSufficientDistanceFromTargets(position, arenaIndex);
    }

    private bool IsWithinArenaBounds(Vector3 position, int arenaIndex)
    {
        var bounds = arenaBounds[arenaIndex];
        return position.x >= bounds.minX && position.x <= bounds.maxX &&
               position.z >= bounds.minZ && position.z <= bounds.maxZ;
    }

    private bool HasCollisionAtPosition(Vector3 position)
    {
        var colliders = Physics.OverlapSphere(position, 0.5f, spawnCollisionLayers);
        foreach (var collider in colliders)
        {
            if (!collider.isTrigger && collider.gameObject != gameObject)
                return true;
        }
        return false;
    }

    private bool HasSufficientDistanceFromTargets(Vector3 position, int arenaIndex)
    {
        foreach (var target in arenaTargets[arenaIndex])
        {
            if (target != null && Vector3.Distance(position, target.transform.position) < minimumSpawnDistance)
                return false;
        }
        return true;
    }

    private GameObject CreateTargetAtPosition(Vector3 position, int arenaIndex)
    {
        if (this == null || !gameObject.scene.isLoaded) return null;

        var arena = arenas[arenaIndex];
        Transform parent = (arena.spawnParent != null && arena.spawnParent.gameObject.scene.isLoaded) 
            ? arena.spawnParent : null;

        GameObject newTarget = Instantiate(trainingTargetPrefab, position, Quaternion.identity, parent);
        if (newTarget != null)
        {
            arenaTargets[arenaIndex].Add(newTarget);
            newTarget.hideFlags = HideFlags.DontSave;
        }
        return newTarget;
    }

    private void ConfigureTarget(GameObject target)
    {
        var playerComponent = target.GetComponent<RL_Player>();
        if (playerComponent != null)
        {
            playerComponent.isRL_TrainingTarget = true;
        }

        var lifeTracker = target.GetComponent<RL_TrainingTarget>();
        if (lifeTracker == null)
            lifeTracker = target.AddComponent<RL_TrainingTarget>();
        lifeTracker.Initialize(this);
    }

    private void PlayEffect(GameObject prefab, Vector3 position)
    {
        if (prefab == null || !gameObject.scene.isLoaded) return;

        var effect = Instantiate(prefab, position, Quaternion.identity);
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

    private ArenaBounds CalculateArenaBounds(ArenaConfiguration arena)
    {
        if (arena.corner1 == null || arena.corner2 == null || 
            arena.corner3 == null || arena.corner4 == null)
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
            minX = minX, maxX = maxX, minZ = minZ, maxZ = maxZ,
            center = new Vector3((minX + maxX) / 2f, arena.corner1.position.y, (minZ + maxZ) / 2f)
        };
    }

    private int FindTargetArena(GameObject target)
    {
        if (target == null || arenaTargets == null || arenas == null) return -1;

        for (int i = 0; i < arenas.Length; i++)
        {
            if (arenaTargets.ContainsKey(i) && arenaTargets[i].Contains(target))
                return i;
        }
        return -1;
    }

    private void RemoveTargetFromArena(GameObject target, int arenaIndex) => 
        arenaTargets[arenaIndex].Remove(target);

    private void RemoveExcessTargetsFromArena(int arenaIndex, int newMax)
    {
        var targets = arenaTargets[arenaIndex];
        while (targets.Count > newMax)
        {
            DestroyTarget(targets[0]);
            targets.RemoveAt(0);
        }
    }

    private void DestroyAllTargets()
    {
        int totalCount = GetTargetCount();
        LogAction("Destroying targets", $"{totalCount} total targets across all arenas");

        foreach (var kvp in arenaTargets)
        {
            for (int i = kvp.Value.Count - 1; i >= 0; i--)
                DestroyTarget(kvp.Value[i]);
            kvp.Value.Clear();
        }
    }

    private void DestroyTarget(GameObject target)
    {
        if (target == null) return;

        try
        {
            var targetComponent = target.GetComponent<RL_TrainingTarget>();
            if (targetComponent != null) targetComponent.enabled = false;

            var playerComponent = target.GetComponent<RL_Player>();
            if (playerComponent != null) playerComponent.enabled = false;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Spawner] Error destroying target: {e.Message}");
        }
    }

    private void CheckForEpisodeEnd()
    {
        bool hasTargets = false;
        bool shouldHaveTargets = false;

        for (int i = 0; i < arenas.Length; i++)
        {
            if (arenas[i].maxTargets > 0)
            {
                shouldHaveTargets = true;
                if (arenaTargets[i].Count > 0)
                {
                    hasTargets = true;
                    break;
                }
            }
        }

        if (!hasTargets && shouldHaveTargets && episodeActive)
        {
            episodeActive = false;
            UpdateVisuals();
            LogAction("Episode ended", "");
        }
    }

    private void UpdateVisuals()
    {
        if (arenaLight != null)
        {
            bool hasTargets = GetTargetCount() > 0;
            arenaLight.color = (episodeActive && hasTargets) ? activeColor : inactiveColor;
        }
    }

    private int GetTargetCount()
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

    private bool IsValidArenaIndex(int arenaIndex) => arenaIndex >= 0 && arenaIndex < arenas.Length;

    private void OnDestroy()
    {
        if (arenaTargets == null) return;

        foreach (var kvp in arenaTargets)
        {
            if (kvp.Value == null) continue;
            
            for (int i = kvp.Value.Count - 1; i >= 0; i--)
            {
                if (kvp.Value[i] != null)
                    DestroyTarget(kvp.Value[i]);
            }
            kvp.Value.Clear();
        }
    }

    private void LogAction(string action, string details)
    {
        if (debugSpawning)
            Debug.Log($"[{gameObject.name}] {action}{(string.IsNullOrEmpty(details) ? "" : $": {details}")}");
    }

    private void LogArenaConfigurations()
    {
        if (!debugSpawning) return;

        for (int i = 0; i < arenas.Length; i++)
            Debug.Log($"Arena {i}: MaxTargets={arenas[i].maxTargets}, Bounds={arenaBounds[i].center}");
    }

    private void OnDrawGizmosSelected()
    {
        if (arenas == null) return;

        for (int i = 0; i < arenas.Length; i++)
        {
            // Draw arena boundaries
            Gizmos.color = Color.cyan;
            if (arenaBounds.ContainsKey(i))
            {
                var bounds = arenaBounds[i];
                Vector3 size = new Vector3(bounds.maxX - bounds.minX, 2f, bounds.maxZ - bounds.minZ);
                Gizmos.DrawWireCube(bounds.center, size);
                
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(bounds.center + Vector3.up * 3f, 0.3f);
            }

            // Draw custom spawn points
            var arena = arenas[i];
            if (arena.customSpawnPoints != null)
            {
                Gizmos.color = Color.yellow;
                foreach (var spawnPoint in arena.customSpawnPoints)
                {
                    if (spawnPoint != null)
                        Gizmos.DrawWireSphere(spawnPoint.position, 0.5f);
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
                        Gizmos.DrawLine(arenaBounds[i].center, target.transform.position);
                }
            }
        }
    }
}