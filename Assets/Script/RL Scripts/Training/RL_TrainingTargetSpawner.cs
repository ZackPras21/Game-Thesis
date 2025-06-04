using System.Collections.Generic;
using UnityEngine;

public class RL_TrainingTargetSpawner : MonoBehaviour
{
    private static List<RL_TrainingTargetSpawner> _activeSpawners = new List<RL_TrainingTargetSpawner>();
    private static RL_TrainingTargetSpawner _activeInstance;
    
    [Header("Spawn Settings")]
    [Tooltip("The prefab you want to spawn as a target (which also has RL_Player on it).")]
    public GameObject trainingTargetPrefab;

    [Tooltip("Maximum number of targets to have alive at once.")]
    public int maxTargets = 3;

    [Tooltip("How far from this spawner's position a valid spawn may appear.")]
    public float spawnRadius = 10f;

    [Tooltip("Seconds between consecutive spawns (if currentTargetCount < maxTargets).")]
    public float spawnInterval = 2f;

    [Tooltip("Layers you want to consider as obstacles when choosing a spawn location.")]
    public LayerMask spawnCollisionLayers;

    [Header("Visual Cues")]
    [Tooltip("A short “poof” or dust effect to play whenever a single target appears (optional).")]
    public GameObject spawnParticlePrefab;

    [Tooltip("A prefab containing a ParticleSystem for the **start** of each episode (e.g. a green flash).")]
    public GameObject episodeStartPrefab;

    [Tooltip("A prefab containing a ParticleSystem for the **end** of an episode (e.g. smoke puff / red flash).")]
    public GameObject episodeEndPrefab;

    [Tooltip("If you want the light to turn Green/Red depending on alive targets, drag it here.")]
    public Light arenaLight;

    [Tooltip("What color should the arena light be when targets are present?")]
    public Color activeColor = Color.green;

    [Tooltip("What color should the arena light be when no targets remain?")]
    public Color inactiveColor = Color.red;

    // ─── Private state ─────────────────────────────────────────────────────────
    private int currentTargetCount = 0;
    private float lastSpawnTime = 0f;
    private bool episodeActive = false;
    private bool isActiveInstance = false;

    // Keep track of all spawned targets so we can forcibly destroy them on reset
    private List<GameObject> activeTargets = new List<GameObject>();

    private void Start()
    {
        _activeSpawners.Add(this);
        if (_activeInstance == null)
        {
            SetAsActiveInstance();
        }
        UpdateArenaVisuals();
    }

    private void Update()
    {
        // Only spawn if the episode is active AND we haven't reached maxTargets yet
        if (!episodeActive) return;

        if (currentTargetCount < maxTargets && Time.time >= lastSpawnTime + spawnInterval)
        {
            SpawnTarget();
            lastSpawnTime = Time.time;
        }
    }


    public void ResetArena()
    {
        // 1) If there are any leftover targets (e.g. from a previous run), destroy them now.
        foreach (GameObject t in activeTargets)
        {
            if (t != null)
                Destroy(t);
        }
        activeTargets.Clear();
        currentTargetCount = 0;

        // 2) Play "Episode Start" Particle (once)
        if (episodeStartPrefab != null)
        {
            Instantiate(episodeStartPrefab, transform.position, Quaternion.identity);
        }

        // 3) Mark episode as active, reset spawn timer, update visuals to “no targets”
        episodeActive = true;
        lastSpawnTime = Time.time - spawnInterval; 
        // (Set it earlier so that the loop below can spawn immediately)

        UpdateArenaVisuals(); // This will set the arenaLight to "inactiveColor" (since currentTargetCount == 0)

        // 4) Immediately spawn exactly maxTargets targets
        for (int i = 0; i < maxTargets; i++)
        {
            SpawnTarget();
            lastSpawnTime = Time.time; 
        }
    }


    private void SpawnTarget()
    {
        if (trainingTargetPrefab == null)
        {
            Debug.LogWarning("RL_TrainingTargetSpawner: trainingTargetPrefab is NULL!");
            return;
        }

        Vector3 spawnPos = GetValidSpawnPosition();
        if (spawnPos == Vector3.zero)
        {
            // Couldn't find a place to spawn after a few tries—skip this round.
            return;
        }

        // 1) Instantiate the target prefab
        GameObject newTarget = Instantiate(trainingTargetPrefab, spawnPos, Quaternion.identity);

        // 2) Mark it as a training target (optional, if you use that flag in RL_Player)
        var playerComp = newTarget.GetComponent<RL_Player>();
        if (playerComp != null)
        {
            playerComp.isTrainingTarget = true;
        }

        // 3) Add the “TrainingTarget” life-tracker so we know when it dies.
        var lifeTracker = newTarget.AddComponent<TrainingTarget>();
        lifeTracker.Initialize(this);

        // 4) Keep track of it so we can forcibly clear on Reset.
        activeTargets.Add(newTarget);
        currentTargetCount++;

        // 5) Play the “spawnParticlePrefab” if assigned
        if (spawnParticlePrefab != null)
        {
            Instantiate(spawnParticlePrefab, spawnPos, Quaternion.identity);
        }

        // 6) Update the arena light to “activeColor” (since we now have ≥1 target)
        UpdateArenaVisuals();
    }


    public void OnTargetDestroyed(GameObject target)
    {
        // Only handle destruction if we're the active instance
        if (!isActiveInstance) return;
        
        // If the GameObject was already removed from activeTargets (or null), do nothing
        if (activeTargets.Contains(target))
        {
            activeTargets.Remove(target);
            currentTargetCount = Mathf.Max(0, currentTargetCount - 1);
        }

        // Update the arena light color based on whether any targets remain
        UpdateArenaVisuals();

        // If we just lost the final target, episode is over
        if (currentTargetCount <= 0 && episodeActive)
        {
            episodeActive = false; // Stop any more spawning

            // Play Episode End effect once
            if (episodeEndPrefab != null)
            {
                Instantiate(episodeEndPrefab, transform.position, Quaternion.identity);
            }

            // NOTE: We do NOT auto-call ResetArena() here. We wait for the Agent's OnEpisodeBegin().
        }
    }
    
    private void SetAsActiveInstance()
    {
        if (_activeInstance != null && _activeInstance != this)
        {
            // Transfer targets from previous active instance
            foreach (var target in _activeInstance.activeTargets)
            {
                if (target != null)
                {
                    var tracker = target.GetComponent<TrainingTarget>();
                    if (tracker != null) tracker.Initialize(this);
                    activeTargets.Add(target);
                }
            }
            _activeInstance.activeTargets.Clear();
            _activeInstance.isActiveInstance = false;
        }
        
        _activeInstance = this;
        isActiveInstance = true;
        currentTargetCount = activeTargets.Count;
        UpdateArenaVisuals();
    }
    
    private void OnDestroy()
    {
        _activeSpawners.Remove(this);
        if (_activeInstance == this)
        {
            _activeInstance = _activeSpawners.Count > 0 ? _activeSpawners[0] : null;
            if (_activeInstance != null) _activeInstance.SetAsActiveInstance();
        }
    }


    private Vector3 GetValidSpawnPosition()
    {
        for (int i = 0; i < 30; i++)
        {
            Vector3 randomPos = transform.position + Random.insideUnitSphere * spawnRadius;
            randomPos.y = transform.position.y; // same height as spawner

            if (!Physics.CheckSphere(randomPos, 1f, spawnCollisionLayers))
            {
                return randomPos;
            }
        }
        return Vector3.zero;
    }

    private void UpdateArenaVisuals()
    {
        if (arenaLight != null)
        {
            arenaLight.color = (currentTargetCount > 0) ? activeColor : inactiveColor;
        }
    }
}
