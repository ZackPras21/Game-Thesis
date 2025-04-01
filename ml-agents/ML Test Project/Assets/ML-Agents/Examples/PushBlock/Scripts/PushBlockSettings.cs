using Unity.MLAgents;
using UnityEngine;

public class PushBlockSettings : MonoBehaviour
{
    // Remove this line:
    // public float spawnAreaMarginMultiplier; 

    // Add this property to read from Academy parameters:
    public float spawnAreaMarginMultiplier
    {
        get { return Academy.Instance.EnvironmentParameters.GetWithDefault("spawnAreaMarginMultiplier", 0.9f); }
    }

    // Keep other fields unchanged:
    public float agentRunSpeed;
    public Material goalScoredMaterial;
    public Material failMaterial;
}
