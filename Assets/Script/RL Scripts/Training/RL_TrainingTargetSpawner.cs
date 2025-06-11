using System.Collections.Generic;
using UnityEngine;

public class RL_TrainingTargetSpawner : MonoBehaviour
{
    private static List<RL_TrainingTargetSpawner> activeSpawners = new List<RL_TrainingTargetSpawner>();
    private static RL_TrainingTargetSpawner activeInstance;
    
    [Header("Spawn Configuration")]
    [SerializeField] private GameObject trainingTargetPrefab;
    [SerializeField] private int maxTargets = 3;
    [SerializeField] private int maxSpawnAttempts = 10;
    [SerializeField] private float spawnRadius = 10f;
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private LayerMask spawnCollisionLayers;

    [Header("Visual Effects")]
    [SerializeField] private GameObject spawnParticlePrefab;
    [SerializeField] private GameObject episodeStartPrefab;
    [SerializeField] private GameObject episodeEndPrefab;
    [SerializeField] private Light arenaLight;
    [SerializeField] private Color activeColor = Color.green;
    [SerializeField] private Color inactiveColor = Color.red;

    private List<GameObject> activeTargets = new List<GameObject>();
    private float lastSpawnTime;
    private bool episodeActive;
    private bool isActiveInstance;

    private void Start()
    {
        InitializeSpawner();
    }

    private void Update()
    {
        HandleContinuousSpawning();
    }

    private void OnDestroy()
    {
        CleanupSpawnerInstance();
    }

    public void ResetArena()
    {
        DestroyAllTargets();
        PlayEpisodeStartEffect();
        StartNewEpisode();
        SpawnInitialTargets();
    }

    public void OnTargetDestroyed(GameObject target)
    {
        if (!isActiveInstance) return;
        
        RemoveTargetFromTracking(target);
        UpdateArenaVisuals();
        TrySpawnReplacementTarget();
        CheckForEpisodeEnd();
    }

    public void NotifyTargetRemoved(GameObject target)
    {
        activeTargets.Remove(target);
        UpdateArenaVisuals();
        SpawnUpToMaximum();
    }

    private void InitializeSpawner()
    {
        activeSpawners.Add(this);
        SetAsActiveInstance();
        SpawnUpToMaximum();
        UpdateArenaVisuals();
    }

    private void HandleContinuousSpawning()
    {
        if (!episodeActive) return;
        if (activeTargets.Count >= maxTargets) return;
        if (Time.time < lastSpawnTime + spawnInterval) return;

        SpawnSingleTarget();
        lastSpawnTime = Time.time;
    }

    private void DestroyAllTargets()
    {
        foreach (GameObject target in activeTargets)
        {
            if (target != null)
                Destroy(target);
        }
        activeTargets.Clear();
    }

    private void PlayEpisodeStartEffect()
    {
        if (episodeStartPrefab != null)
        {
            Instantiate(episodeStartPrefab, transform.position, Quaternion.identity);
        }
    }

    private void StartNewEpisode()
    {
        episodeActive = true;
        lastSpawnTime = Time.time - spawnInterval;
        UpdateArenaVisuals();
    }

    private void SpawnInitialTargets()
    {
        for (int i = 0; i < maxTargets; i++)
        {
            SpawnSingleTarget();
            lastSpawnTime = Time.time;
        }
    }

    private void SpawnUpToMaximum()
    {
        while (activeTargets.Count < maxTargets)
        {
            Vector3 spawnPosition = FindValidSpawnPosition();
            if (spawnPosition == Vector3.zero) break;
            
            CreateTargetAtPosition(spawnPosition);
        }
    }

    private void SpawnSingleTarget()
    {
        if (trainingTargetPrefab == null)
        {
            Debug.LogWarning("Training target prefab is not assigned!");
            return;
        }

        Vector3 spawnPosition = GetValidSpawnPosition();
        if (spawnPosition == Vector3.zero) return;

        GameObject newTarget = CreateTargetAtPosition(spawnPosition);
        ConfigureNewTarget(newTarget);
        PlaySpawnEffect(spawnPosition);
        UpdateArenaVisuals();
    }

    private GameObject CreateTargetAtPosition(Vector3 position)
    {
        GameObject newTarget = Instantiate(trainingTargetPrefab, position, Quaternion.identity);
        activeTargets.Add(newTarget);
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

        var lifeTracker = target.AddComponent<RL_TrainingTarget>();
        lifeTracker.Initialize(this);
    }

    private void PlaySpawnEffect(Vector3 position)
    {
        if (spawnParticlePrefab != null)
        {
            Instantiate(spawnParticlePrefab, position, Quaternion.identity);
        }
    }

    private Vector3 FindValidSpawnPosition()
    {
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            Vector3 offset = Random.insideUnitSphere * spawnRadius;
            offset.y = 0;
            Vector3 candidatePosition = transform.position + offset;
            
            // Check if this position is valid (no collision)
            if (!Physics.CheckSphere(candidatePosition, 1f, spawnCollisionLayers))
            {
                return candidatePosition;
            }
        }
        return Vector3.zero;
    }

    private Vector3 GetValidSpawnPosition()
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            Vector3 candidatePosition = transform.position + Random.insideUnitSphere * spawnRadius;
            candidatePosition.y = transform.position.y;

            if (!Physics.CheckSphere(candidatePosition, 1f, spawnCollisionLayers))
            {
                return candidatePosition;
            }
        }
        return Vector3.zero;
    }

    private void RemoveTargetFromTracking(GameObject target)
    {
        if (activeTargets.Contains(target))
        {
            activeTargets.Remove(target);
        }
    }

    private void TrySpawnReplacementTarget()
    {
        if (activeTargets.Count < maxTargets && episodeActive)
        {
            SpawnSingleTarget();
            lastSpawnTime = Time.time;
        }
    }

    private void CheckForEpisodeEnd()
    {
        if (activeTargets.Count <= 0 && episodeActive)
        {
            EndCurrentEpisode();
        }
    }

    private void EndCurrentEpisode()
    {
        episodeActive = false;
        PlayEpisodeEndEffect();
    }

    private void PlayEpisodeEndEffect()
    {
        if (episodeEndPrefab != null)
        {
            Instantiate(episodeEndPrefab, transform.position, Quaternion.identity);
        }
    }

    private void SetAsActiveInstance()
    {
        if (activeInstance != null && activeInstance != this)
        {
            TransferTargetsFromPreviousInstance();
        }
        
        activeInstance = this;
        isActiveInstance = true;
        UpdateArenaVisuals();
    }

    private void TransferTargetsFromPreviousInstance()
    {
        foreach (var target in activeInstance.activeTargets)
        {
            if (target != null)
            {
                var tracker = target.GetComponent<RL_TrainingTarget>();
                if (tracker != null) tracker.Initialize(this);
                activeTargets.Add(target);
            }
        }
        
        activeInstance.activeTargets.Clear();
        activeInstance.isActiveInstance = false;
    }

    private void CleanupSpawnerInstance()
    {
        activeSpawners.Remove(this);
        
        if (activeInstance == this)
        {
            activeInstance = activeSpawners.Count > 0 ? activeSpawners[0] : null;
            if (activeInstance != null) 
                activeInstance.SetAsActiveInstance();
        }
    }

    private void UpdateArenaVisuals()
    {
        if (arenaLight != null)
        {
            arenaLight.color = (activeTargets.Count > 0) ? activeColor : inactiveColor;
        }
    }
}