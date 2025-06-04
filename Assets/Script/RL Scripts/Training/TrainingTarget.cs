using UnityEngine;

public class TrainingTarget : MonoBehaviour
{
    private RL_TrainingTargetSpawner _spawner;
    private bool _isBeingDestroyed = false;

    public void Initialize(RL_TrainingTargetSpawner spawner)
    {
        // Only update if new spawner is not null
        if (spawner != null)
        {
            _spawner = spawner;
        }
    }

    private void OnDestroy()
    {
        if (_isBeingDestroyed) return;
        _isBeingDestroyed = true;
        
        // If the spawner still exists, let it know one target died
        if (_spawner != null)
        {
            _spawner.OnTargetDestroyed(gameObject);
        }
    }
    
    public void ForceNotifyDestruction()
    {
        if (_isBeingDestroyed) return;
        OnDestroy();
    }
}
