using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class RL_TrainingManager : MonoBehaviour
{
    [Header("Training Configuration")]
    [SerializeField] private bool autoStartTraining = true;
    [SerializeField] private float episodeResetDelay = 1f;
    [SerializeField] private bool debugTraining = true;
    [SerializeField] private bool enableTargetSpawning = true;

    private RL_TrainingEnemySpawner enemySpawner;
    private RL_TrainingTargetSpawner[] targetSpawners;
    private List<NormalEnemyAgent> allAgents = new List<NormalEnemyAgent>();
    private bool isResetting = false;
    private int activeEnemiesCount = 0;

    #region Unity Lifecycle
    private void Awake() => InitializeComponents();
    
    private void Start()
    {
        if (autoStartTraining)
            StartCoroutine(InitializeTrainingSession());
    }

    private void OnDestroy() => StopAllCoroutines();
    #endregion

    #region Public Interface
    public void StartNewEpisode()
    {
        if (!isResetting)
            StartCoroutine(ResetEpisodeCoroutine());
    }

    public void ResetSpecificArena(int arenaIndex)
    {
        if (!isResetting)
            StartCoroutine(ResetArenaCoroutine(arenaIndex));
    }

    public void SetTargetSpawningEnabled(bool enabled)
    {
        enableTargetSpawning = enabled;
        ConfigureAllTargetSpawners();
        LogDebug($"Target spawning globally set to: {enableTargetSpawning}");
    }

    public void SetMaxTargetsPerArena(int maxTargets)
    {
        if (targetSpawners == null) return;
        
        foreach (var spawner in targetSpawners)
            spawner?.SetMaxTargets(maxTargets);
    }

    public void ForceReset()
    {
        StopAllCoroutines();
        isResetting = false;
        StartNewEpisode();
    }

    public void PauseTraining()
    {
        StopAllCoroutines();
        isResetting = false;
        SetAllTargetSpawners(false);
    }

    public void ResumeTraining()
    {
        if (!isResetting)
        {
            SetAllTargetSpawners(enableTargetSpawning);
            StartNewEpisode();
        }
    }

    public void HandleEnemyDeath()
    {
        if (--activeEnemiesCount <= 0)
            StartCoroutine(ResetEpisodeCoroutine());
    }
    #endregion

    #region Initialization
    private void InitializeComponents()
    {
        enemySpawner = Object.FindFirstObjectByType<RL_TrainingEnemySpawner>();
        targetSpawners = Object.FindObjectsByType<RL_TrainingTargetSpawner>(FindObjectsSortMode.None);
        
        ValidateComponents();
    }

    private void ValidateComponents()
    {
        if (enemySpawner == null)
            Debug.LogError("RL_TrainingEnemySpawner not found!");
        
        if (targetSpawners?.Length == 0)
            Debug.LogWarning("No RL_TrainingTargetSpawner found.");
        else
            LogDebug($"Found {targetSpawners.Length} target spawners");
    }

    private IEnumerator InitializeTrainingSession()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        
        ConfigureAllTargetSpawners();
        yield return new WaitForSeconds(2f);
        
        RefreshAgentList();
        LogDebug($"Training started with {allAgents.Count} agents across {GetArenaCount()} arenas");
    }
    #endregion

    #region Episode Management
    private IEnumerator ResetEpisodeCoroutine()
    {
        isResetting = true;
        LogDebug("Starting new training episode...");

        RefreshAgentList();
        bool allDead = allAgents.Count == 0 || allAgents.All(agent => agent.IsDead);
        
        if (allDead)
        {
            ConfigureAllTargetSpawners();
            ResetAllTargetSpawners();
            yield return new WaitForSeconds(episodeResetDelay);
            enemySpawner?.RespawnAllArenas();
            yield return new WaitForSeconds(1f);
            RefreshAgentList();
        }
        
        ResetAllAgents();
        LogDebug($"Episode reset complete. Active agents: {allAgents.Count}");
        isResetting = false;
    }

    private IEnumerator ResetArenaCoroutine(int arenaIndex)
    {
        LogDebug($"Resetting Arena {arenaIndex}");
        RefreshAgentList();
        
        if (activeEnemiesCount <= 0)
        {
            ResetArenaComponents(arenaIndex);
            yield return new WaitForSeconds(0.5f);
            RefreshAgentList();
            ResetAllAgents();
        }
        else
        {
            LogDebug($"Skipping arena {arenaIndex} reset - enemies still active");
        }
    }

    private void ResetArenaComponents(int arenaIndex)
    {
        if (IsValidArenaIndex(arenaIndex))
        {
            var spawner = targetSpawners[arenaIndex];
            spawner?.SetSpawningEnabled(enableTargetSpawning);
            spawner?.ResetArena();
            enemySpawner?.RespawnSpecificArena(arenaIndex);
        }
    }
    #endregion

    #region Agent Management
    private void RefreshAgentList()
    {
        allAgents.Clear();
        activeEnemiesCount = 0;
        
        var foundAgents = FindObjectsByType<NormalEnemyAgent>(FindObjectsSortMode.None);
        
        foreach (var agent in foundAgents)
        {
            if (agent?.gameObject.activeInHierarchy == true)
            {
                allAgents.Add(agent);
                activeEnemiesCount++;
            }
        }
        
        LogDebug($"Refreshed agent list: Found {allAgents.Count} active agents");
    }

    private void ResetAllAgents()
    {
        foreach (var agent in allAgents)
        {
            if (agent?.gameObject.activeInHierarchy == true)
            {
                try { agent.EndEpisode(); }
                catch (System.Exception e) { Debug.LogError($"Error resetting agent {agent.name}: {e.Message}"); }
            }
        }
    }
    #endregion

    #region Target Spawner Management
    private void ConfigureAllTargetSpawners()
    {
        if (targetSpawners == null) return;
        
        LogDebug($"Configuring {targetSpawners.Length} target spawners - Spawning enabled: {enableTargetSpawning}");
        SetAllTargetSpawners(enableTargetSpawning);
    }

    private void SetAllTargetSpawners(bool enabled)
    {
        if (targetSpawners == null) return;
        
        foreach (var spawner in targetSpawners)
            spawner?.SetSpawningEnabled(enabled);
    }

    private void ResetAllTargetSpawners()
    {
        if (targetSpawners == null) return;
        
        LogDebug($"Resetting {targetSpawners.Length} target spawners");
        foreach (var spawner in targetSpawners)
        {
            if (spawner != null)
            {
                spawner.SetSpawningEnabled(enableTargetSpawning);
                spawner.ResetArena();
            }
        }
    }
    #endregion

    #region Utility Methods
    private bool IsValidArenaIndex(int arenaIndex) => 
        targetSpawners != null && arenaIndex >= 0 && arenaIndex < targetSpawners.Length;

    private int GetArenaCount()
    {
        if (enemySpawner == null) return 0;
        
        var arenasField = typeof(RL_TrainingEnemySpawner).GetField("arenas", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        return (arenasField?.GetValue(enemySpawner) as RL_TrainingEnemySpawner.ArenaConfiguration[])?.Length ?? 0;
    }

    private void LogDebug(string message)
    {
        if (debugTraining) Debug.Log(message);
    }
    #endregion

    #region Debug Information
    public int GetActiveAgentCount() => allAgents.Count;
    public bool IsResetting() => isResetting;

    public int GetActiveTargetsCount()
    {
        if (targetSpawners == null) return 0;
        return targetSpawners.Where(s => s != null).Sum(s => s.GetActiveTargetCount());
    }

    [System.Serializable]
    public class DebugInfo
    {
        public int activeAgents;
        public int totalTargets;
        public bool isResetting;
        public bool targetSpawningEnabled;
    }

    public DebugInfo GetDebugInfo() => new DebugInfo
    {
        activeAgents = allAgents.Count,
        totalTargets = GetActiveTargetsCount(),
        isResetting = isResetting,
        targetSpawningEnabled = enableTargetSpawning
    };
    #endregion
}