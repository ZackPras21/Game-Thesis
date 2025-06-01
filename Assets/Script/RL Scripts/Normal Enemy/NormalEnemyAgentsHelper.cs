using UnityEngine;
using Unity.MLAgents;
using System.Collections.Generic;
public class NormalEnemyAgentsHelper : MonoBehaviour
{
    public NormalEnemyAgent enemyPrefab;
    public int numberOfEnemies = 5;
    private List<NormalEnemyAgent> spawnedAgents = new List<NormalEnemyAgent>();

    void Start()
    {
        for (int i = 0; i < numberOfEnemies; i++)
        {
            var go = Instantiate(enemyPrefab.gameObject);
            spawnedAgents.Add(go.GetComponent<NormalEnemyAgent>());
        }
    }
}
