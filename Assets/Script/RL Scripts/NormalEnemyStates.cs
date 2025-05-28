using UnityEngine;

[System.Serializable]
public class NormalEnemyState
{
    // Navigation state
    public Vector3 Position;
    public Vector3 PlayerPosition;
    public Vector3 LastPlayerPosition;
    public float DistanceToPlayer;
    
    // Detection state
    public bool PlayerInRange;
    public bool PlayerNear;
    public bool CanSeePlayer;
    
    // Combat state
    public float Health;
    public float HealthPercentage;
    public bool IsHealthLow;
    public bool IsAttacking;
    public float TimeSinceLastAttack;
    
    // Movement state
    public bool IsPatrol;
    public bool CaughtPlayer;
    public float WaitTime;
    public float TimeToRotate;
    
    public void UpdateFromController(EnemyController controller)
    {
        Position = controller.transform.position;
        PlayerPosition = controller.PlayerPosition;
        LastPlayerPosition = controller.PlayerLastPosition;
        DistanceToPlayer = Vector3.Distance(Position, PlayerPosition);
        
        PlayerInRange = controller.m_PlayerInRange;
        PlayerNear = controller.m_PlayerNear;
        CanSeePlayer = controller.m_PlayerInRange && !Physics.Raycast(
            Position,
            (PlayerPosition - Position).normalized,
            DistanceToPlayer,
            controller.obstacleMask);
            
        Health = controller.enemyHP;
        HealthPercentage = controller.GetHealthPercentage();
        IsHealthLow = controller.IsHealthLow();
        IsAttacking = controller.m_IsAttacking;
        
        IsPatrol = controller.m_IsPatrol;
        CaughtPlayer = controller.IsCaughtPlayer;
        WaitTime = controller.WaitTime;
        TimeToRotate = controller.TimeToRotate;
    }
}
