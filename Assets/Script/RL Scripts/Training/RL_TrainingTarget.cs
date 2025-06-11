using UnityEngine;

public class RL_TrainingTarget : MonoBehaviour
{
    private RL_TrainingTargetSpawner spawner;
    private bool isBeingDestroyed = false;

    public void Initialize(RL_TrainingTargetSpawner newSpawner)
    {
        if (newSpawner != null)
        {
            spawner = newSpawner;
        }
    }

    public void ForceNotifyDestruction()
    {
        if (!isBeingDestroyed)
        {
            OnDestroy();
        }
    }

    private void OnDestroy()
    {
        if (isBeingDestroyed) return;
        
        isBeingDestroyed = true;
        NotifySpawnerOfDestruction();
    }

    private void NotifySpawnerOfDestruction()
    {
        if (spawner != null)
        {
            spawner.OnTargetDestroyed(gameObject);
        }
    }
}
