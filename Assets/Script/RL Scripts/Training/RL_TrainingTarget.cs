using UnityEngine;

public class RL_TrainingTarget : MonoBehaviour
{
    private RL_TrainingTargetSpawner spawner;
    private bool isBeingDestroyed = false;

    public void Initialize(RL_TrainingTargetSpawner targetSpawner) => spawner = targetSpawner;

    public void ForceNotifyDestruction()
    {
        if (!isBeingDestroyed)
            HandleDestruction();
    }

    private void OnDestroy() => HandleDestruction();

    private void HandleDestruction()
    {
        if (isBeingDestroyed) return;
        
        isBeingDestroyed = true;
        spawner?.OnTargetDestroyed(gameObject);
    }
}