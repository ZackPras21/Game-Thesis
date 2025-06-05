using UnityEngine;
using System.Collections.Generic;
public class RL_TrainingManager : MonoBehaviour
{
    private RL_TrainingTargetSpawner targetSpawner;
    private List<NormalEnemyAgent> allAgents;

    private void Awake()
    {
        targetSpawner = FindObjectOfType<RL_TrainingTargetSpawner>();
        allAgents = new List<NormalEnemyAgent>(FindObjectsOfType<NormalEnemyAgent>());
    }

    public void StartNewEpisode()
    {
        if (targetSpawner != null)
        {
            targetSpawner.ResetArena();
        }
        foreach (var agent in allAgents)
        {
            agent.EndEpisode(); // forces them to run their own OnEpisodeBegin next
        }
    }
}
