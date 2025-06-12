using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RL_TrainingManager : MonoBehaviour
{
    [Header("Training Configuration")]
    [SerializeField] private bool autoStartTraining = true;
    [SerializeField] private float episodeResetDelay = 1f;
    [SerializeField] private bool debugTraining = true;

    private RL_TrainingEnemySpawner enemySpawner;
    private RL_TrainingTargetSpawner targetSpawner;
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
            StartCoroutine(WaitForSpawnersAndStart());
        }
    }

    private IEnumerator WaitForSpawnersAndStart()
    {
        // Wait a frame to ensure all spawners have initialized
        yield return new WaitForEndOfFrame();
        
        // Wait for enemy spawning to complete
        yield return new WaitForSeconds(2f);
        
        RefreshAgentList();
        
        if (debugTraining)
        {
            Debug.Log($"Training started with {allAgents.Count} agents across {GetArenaCount()} arenas");
        }
    }

    public void StartNewEpisode()
    {
        if (isResetting) return;
        
        StartCoroutine(ResetEpisodeCoroutine());
    }

    private IEnumerator ResetEpisodeCoroutine()
    {
        isResetting = true;
        
        if (debugTraining)
        {
            Debug.Log("Starting new training episode...");
        }

        // Reset target spawner first
        ResetTargetSpawner();
        
        // Wait a bit before respawning enemies
        yield return new WaitForSeconds(episodeResetDelay);
        
        // Respawn all enemies
        ResetEnemySpawner();
        
        // Wait for respawning to complete
        yield return new WaitForSeconds(1f);
        
        // Refresh agent list to include newly spawned agents
        RefreshAgentList();
        
        // Reset all agents
        ResetAllAgents();
        
        if (debugTraining)
        {
            Debug.Log($"Episode reset complete. Active agents: {allAgents.Count}");
        }
        
        isResetting = false;
    }

    public void ResetSpecificArena(int arenaIndex)
    {
        if (isResetting) return;
        
        StartCoroutine(ResetArenaCoroutine(arenaIndex));
    }

    private IEnumerator ResetArenaCoroutine(int arenaIndex)
    {
        if (debugTraining)
        {
            Debug.Log($"Resetting Arena {arenaIndex}");
        }

        // Reset enemies in specific arena
        if (enemySpawner != null)
        {
            enemySpawner.RespawnSpecificArena(arenaIndex);
        }
        
        yield return new WaitForSeconds(0.5f);
        
        // Refresh agents and reset those in the specific arena
        RefreshAgentList();
        ResetAgentsInArena(arenaIndex);
    }

    private void InitializeComponents()
    {
        enemySpawner = Object.FindFirstObjectByType<RL_TrainingEnemySpawner>();
        targetSpawner = Object.FindFirstObjectByType<RL_TrainingTargetSpawner>();
        allAgents = new List<NormalEnemyAgent>();
        
        if (enemySpawner == null)
        {
            Debug.LogError("RL_TrainingEnemySpawner not found! Please add it to the scene.");
        }
        
        if (targetSpawner == null)
        {
            Debug.LogWarning("RL_TrainingTargetSpawner not found. Target spawning will be skipped.");
        }
    }

    private void RefreshAgentList()
    {
        // Clear existing list
        allAgents.Clear();
        
        // Find all NormalEnemyAgent components in the scene
        NormalEnemyAgent[] foundAgents = FindObjectsByType<NormalEnemyAgent>(FindObjectsSortMode.None);
        
        foreach (var agent in foundAgents)
        {
            if (agent != null && agent.gameObject.activeInHierarchy)
            {
                allAgents.Add(agent);
            }
        }
        
        if (debugTraining)
        {
            Debug.Log($"Refreshed agent list: Found {allAgents.Count} active agents");
        }
    }

    private void ResetEnemySpawner()
    {
        if (enemySpawner != null)
        {
            enemySpawner.RespawnAllArenas();
        }
    }

    private void ResetTargetSpawner()
    {
        if (targetSpawner != null)
        {
            targetSpawner.ResetArena();
        }
    }

    private void ResetAllAgents()
    {
        foreach (var agent in allAgents)
        {
            if (agent != null && agent.gameObject.activeInHierarchy)
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
        }
    }

    private void ResetAgentsInArena(int arenaIndex)
    {
        // This would require knowing which agents belong to which arena
        // For now, we'll reset all agents as a fallback
        ResetAllAgents();
    }

    private int GetArenaCount()
    {
        if (enemySpawner == null) return 0;
        
        // Access the arenas array through reflection or make it public
        var arenasField = typeof(RL_TrainingEnemySpawner).GetField("arenas", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (arenasField != null)
        {
            var arenas = arenasField.GetValue(enemySpawner) as RL_TrainingEnemySpawner.ArenaConfiguration[];
            return arenas?.Length ?? 0;
        }
        
        return 0;
    }

    // Public methods for external control
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
    }

    public void ResumeTraining()
    {
        if (!isResetting)
        {
            StartNewEpisode();
        }
    }

    // Debug information
    public int GetActiveAgentCount()
    {
        return allAgents.Count;
    }

    public bool IsResetting()
    {
        return isResetting;
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
    }
}