using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RL_TrainingManager : MonoBehaviour
{
    [Header("Training Configuration")]
    [SerializeField] private bool autoStartTraining = true;
    [SerializeField] private float episodeResetDelay = 1f;
    [SerializeField] private bool debugTraining = true;
    [SerializeField] private bool enableTargetSpawning = true;

    private RL_TrainingEnemySpawner enemySpawner;
    private RL_TrainingTargetSpawner[] targetSpawners;
    private List<NormalEnemyAgent> allAgents;
    private bool isResetting = false;

    private void Awake()
    {
        InitializeComponents();
    }

    private void Start()
    {
        if (autoStartTraining)
        {
            StartCoroutine(InitializeTrainingSession());
        }
    }

    #region Public Interface

    public void StartNewEpisode()
    {
        if (!isResetting)
        {
            StartCoroutine(ResetEpisodeCoroutine());
        }
    }

    public void ResetSpecificArena(int arenaIndex)
    {
        if (!isResetting)
        {
            StartCoroutine(ResetArenaCoroutine(arenaIndex));
        }
    }

    public void SetTargetSpawningEnabled(bool enabled)
    {
        enableTargetSpawning = enabled;
        ConfigureAllTargetSpawners();
        LogTargetSpawningStatus();
    }

    public void SetMaxTargetsPerArena(int maxTargets)
    {
        UpdateTargetSpawnerLimits(maxTargets);
        LogTargetSpawnerStatus();
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
        DisableAllTargetSpawners();
    }

    public void ResumeTraining()
    {
        if (!isResetting)
        {
            EnableTargetSpawnersIfGloballyEnabled();
            StartNewEpisode();
        }
    }

    #endregion

    #region Initialization

    private void InitializeComponents()
    {
        enemySpawner = Object.FindFirstObjectByType<RL_TrainingEnemySpawner>();
        targetSpawners = Object.FindObjectsByType<RL_TrainingTargetSpawner>(FindObjectsSortMode.None);
        allAgents = new List<NormalEnemyAgent>();
        
        ValidateRequiredComponents();
    }

    private void ValidateRequiredComponents()
    {
        if (enemySpawner == null)
        {
            Debug.LogError("RL_TrainingEnemySpawner not found! Please add it to the scene.");
        }
        
        if (targetSpawners == null || targetSpawners.Length == 0)
        {
            Debug.LogWarning("No RL_TrainingTargetSpawner found. Target spawning will be skipped.");
        }
        else if (debugTraining)
        {
            Debug.Log($"Found {targetSpawners.Length} target spawners");
        }
    }

    private IEnumerator InitializeTrainingSession()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        
        ConfigureAllTargetSpawners();
        yield return new WaitForSeconds(2f);
        
        RefreshAgentList();
        LogTrainingSessionStart();
    }

    #endregion

    #region Episode Management

    private IEnumerator ResetEpisodeCoroutine()
    {
        isResetting = true;
        LogEpisodeStart();

        ConfigureAllTargetSpawners();
        ResetAllTargetSpawners();
        
        yield return new WaitForSeconds(episodeResetDelay);
        
        ResetEnemySpawner();
        yield return new WaitForSeconds(1f);
        
        RefreshAgentList();
        ResetAllAgents();
        
        LogEpisodeComplete();
        isResetting = false;
    }

    private IEnumerator ResetArenaCoroutine(int arenaIndex)
    {
        LogArenaReset(arenaIndex);

        ConfigureSpecificTargetSpawner(arenaIndex);
        ResetSpecificTargetSpawner(arenaIndex);
        RespawnEnemiesInArena(arenaIndex);
        
        yield return new WaitForSeconds(0.5f);
        
        RefreshAgentList();
        ResetAgentsInArena(arenaIndex);
    }

    #endregion

    #region Target Spawner Management

    private void ConfigureAllTargetSpawners()
    {
        if (targetSpawners == null) return;
        
        LogTargetSpawnerConfiguration();
        
        foreach (var spawner in targetSpawners)
        {
            if (spawner != null)
            {
                spawner.SetSpawningEnabled(enableTargetSpawning);
                LogIndividualSpawnerConfiguration(spawner);
            }
        }
    }

    private void ConfigureSpecificTargetSpawner(int arenaIndex)
    {
        if (IsValidArenaIndex(arenaIndex) && targetSpawners[arenaIndex] != null)
        {
            targetSpawners[arenaIndex].SetSpawningEnabled(enableTargetSpawning);
            LogArenaSpawnerConfiguration(arenaIndex);
        }
    }

    private void UpdateTargetSpawnerLimits(int maxTargets)
    {
        if (targetSpawners == null) return;

        foreach (var spawner in targetSpawners)
        {
            if (spawner != null)
            {
                spawner.SetMaxTargets(maxTargets);
            }
        }
    }

    private void ResetAllTargetSpawners()
    {
        if (targetSpawners == null) return;
        
        LogTargetSpawnerReset();
        
        foreach (var spawner in targetSpawners)
        {
            if (spawner != null)
            {
                spawner.SetSpawningEnabled(enableTargetSpawning);
                spawner.ResetArena();
                LogSpawnerResetResult(spawner);
            }
        }
    }

    private void ResetSpecificTargetSpawner(int arenaIndex)
    {
        if (IsValidArenaIndex(arenaIndex) && targetSpawners[arenaIndex] != null)
        {
            var spawner = targetSpawners[arenaIndex];
            spawner.SetSpawningEnabled(enableTargetSpawning);
            spawner.ResetArena();
            LogSpecificSpawnerReset(arenaIndex, spawner);
        }
    }

    private void DisableAllTargetSpawners()
    {
        if (targetSpawners == null) return;

        foreach (var spawner in targetSpawners)
        {
            if (spawner != null)
            {
                spawner.SetSpawningEnabled(false);
            }
        }
    }

    private void EnableTargetSpawnersIfGloballyEnabled()
    {
        if (targetSpawners == null) return;

        foreach (var spawner in targetSpawners)
        {
            if (spawner != null)
            {
                spawner.SetSpawningEnabled(enableTargetSpawning);
            }
        }
    }

    #endregion

    #region Agent Management

    private void RefreshAgentList()
    {
        allAgents.Clear();
        
        NormalEnemyAgent[] foundAgents = FindObjectsByType<NormalEnemyAgent>(FindObjectsSortMode.None);
        
        foreach (var agent in foundAgents)
        {
            if (IsValidActiveAgent(agent))
            {
                allAgents.Add(agent);
            }
        }
        
        LogAgentRefresh();
    }

    private bool IsValidActiveAgent(NormalEnemyAgent agent)
    {
        return agent != null && agent.gameObject.activeInHierarchy;
    }

    private void ResetAllAgents()
    {
        foreach (var agent in allAgents)
        {
            if (IsValidActiveAgent(agent))
            {
                TryResetAgent(agent);
            }
        }
    }

    private void ResetAgentsInArena(int arenaIndex)
    {
        ResetAllAgents();
    }

    private void TryResetAgent(NormalEnemyAgent agent)
    {
        try
        {
            agent.EndEpisode();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error resetting agent {agent.name}: {e.Message}");
        }
    }

    #endregion

    #region Enemy Spawner Management

    private void ResetEnemySpawner()
    {
        if (enemySpawner != null)
        {
            enemySpawner.RespawnAllArenas();
        }
    }

    private void RespawnEnemiesInArena(int arenaIndex)
    {
        if (enemySpawner != null)
        {
            enemySpawner.RespawnSpecificArena(arenaIndex);
        }
    }

    #endregion

    #region Utility Methods

    private bool IsValidArenaIndex(int arenaIndex)
    {
        return targetSpawners != null && arenaIndex >= 0 && arenaIndex < targetSpawners.Length;
    }

    private int GetArenaCount()
    {
        if (enemySpawner == null) return 0;
        
        var arenasField = typeof(RL_TrainingEnemySpawner).GetField("arenas", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (arenasField != null)
        {
            var arenas = arenasField.GetValue(enemySpawner) as RL_TrainingEnemySpawner.ArenaConfiguration[];
            return arenas?.Length ?? 0;
        }
        
        return 0;
    }

    #endregion

    #region Debug Information

    public int GetActiveAgentCount() => allAgents.Count;
    public bool IsResetting() => isResetting;

    public int GetActiveTargetsCount()
    {
        if (targetSpawners == null) return 0;

        int totalTargets = 0;
        foreach (var spawner in targetSpawners)
        {
            if (spawner != null)
            {
                totalTargets += spawner.GetActiveTargetCount();
            }
        }
        return totalTargets;
    }

    [System.Serializable]
    public class DebugInfo
    {
        public int activeAgents;
        public int totalTargets;
        public bool isResetting;
        public bool targetSpawningEnabled;
    }

    public DebugInfo GetDebugInfo()
    {
        return new DebugInfo
        {
            activeAgents = allAgents.Count,
            totalTargets = GetActiveTargetsCount(),
            isResetting = isResetting,
            targetSpawningEnabled = enableTargetSpawning
        };
    }

    #endregion

    #region Logging

    private void LogTrainingSessionStart()
    {
        if (debugTraining)
        {
            Debug.Log($"Training started with {allAgents.Count} agents across {GetArenaCount()} arenas");
            Debug.Log($"Target spawning enabled: {enableTargetSpawning}");
            LogTargetSpawnerStatus();
        }
    }

    private void LogEpisodeStart()
    {
        if (debugTraining)
        {
            Debug.Log("Starting new training episode...");
        }
    }

    private void LogEpisodeComplete()
    {
        if (debugTraining)
        {
            Debug.Log($"Episode reset complete. Active agents: {allAgents.Count}");
            LogTargetSpawnerStatus();
        }
    }

    private void LogArenaReset(int arenaIndex)
    {
        if (debugTraining)
        {
            Debug.Log($"Resetting Arena {arenaIndex}");
        }
    }

    private void LogTargetSpawningStatus()
    {
        if (debugTraining)
        {
            Debug.Log($"Target spawning globally set to: {enableTargetSpawning}");
            LogTargetSpawnerStatus();
        }
    }

    private void LogTargetSpawnerConfiguration()
    {
        if (debugTraining)
        {
            Debug.Log($"Configuring {targetSpawners.Length} target spawners - Spawning enabled: {enableTargetSpawning}");
        }
    }

    private void LogIndividualSpawnerConfiguration(RL_TrainingTargetSpawner spawner)
    {
        if (debugTraining)
        {
            Debug.Log($"Configured spawner {spawner.name}: Enabled={spawner.IsSpawningEnabled()}, MaxTargets={spawner.GetMaxTargets()}");
        }
    }

    private void LogArenaSpawnerConfiguration(int arenaIndex)
    {
        if (debugTraining)
        {
            Debug.Log($"Configured Arena {arenaIndex} spawner: Enabled={enableTargetSpawning}");
        }
    }

    private void LogAgentRefresh()
    {
        if (debugTraining)
        {
            Debug.Log($"Refreshed agent list: Found {allAgents.Count} active agents");
        }
    }

    private void LogTargetSpawnerReset()
    {
        if (debugTraining)
        {
            Debug.Log($"Resetting {targetSpawners.Length} target spawners");
        }
    }

    private void LogSpawnerResetResult(RL_TrainingTargetSpawner spawner)
    {
        if (debugTraining)
        {
            Debug.Log($"Reset spawner {spawner.name}: Active targets after reset = {spawner.GetActiveTargetCount()}");
        }
    }

    private void LogSpecificSpawnerReset(int arenaIndex, RL_TrainingTargetSpawner spawner)
    {
        if (debugTraining)
        {
            Debug.Log($"Reset Arena {arenaIndex} spawner: Active targets = {spawner.GetActiveTargetCount()}");
        }
    }

    private void LogTargetSpawnerStatus()
    {
        if (targetSpawners == null) return;
        
        for (int i = 0; i < targetSpawners.Length; i++)
        {
            var spawner = targetSpawners[i];
            if (spawner != null)
            {
                Debug.Log($"Arena {i} Target Spawner: {spawner.GetActiveTargetCount()}/{spawner.GetMaxTargets()} targets, " +
                         $"Enabled: {spawner.IsSpawningEnabled()}, Episode Active: {spawner.IsEpisodeActive()}");
            }
        }
    }

    #endregion

    private void OnDestroy()
    {
        StopAllCoroutines();
    }
}