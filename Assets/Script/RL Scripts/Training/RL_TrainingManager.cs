using UnityEngine;
using System.Collections.Generic;

public class RL_TrainingManager : MonoBehaviour
{
    private RL_TrainingTargetSpawner targetSpawner;
    private List<NormalEnemyAgent> allAgents;

    private void Awake()
    {
        InitializeComponents();
    }

    public void StartNewEpisode()
    {
        ResetTargetSpawner();
        ResetAllAgents();
    }

    private void InitializeComponents()
    {
        targetSpawner = Object.FindFirstObjectByType<RL_TrainingTargetSpawner>();
        allAgents = new List<NormalEnemyAgent>(FindObjectsByType<NormalEnemyAgent>(FindObjectsSortMode.None));
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
            agent.EndEpisode();
        }
    }
}

