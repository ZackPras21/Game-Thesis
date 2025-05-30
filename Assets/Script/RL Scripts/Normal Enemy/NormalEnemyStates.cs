using UnityEngine;

[System.Serializable]
public class NormalEnemyState
{
    public Vector3 Position;
    public Vector3 PlayerPosition;
    public float DistanceToPlayer;
    public bool PlayerInRange;
    public bool CanSeePlayer;
    public float Health;
    public bool IsAttacking;
    public bool IsHealthLow;

    // Update the state with the enemy's and player's position and other necessary info
    public void UpdateState(Vector3 enemyPosition, Vector3 playerPosition, float health)
    {
        Position = enemyPosition;
        PlayerPosition = playerPosition;
        DistanceToPlayer = Vector3.Distance(Position, PlayerPosition);
        PlayerInRange = DistanceToPlayer < 10f; // Example range
        CanSeePlayer = true; // You could add vision logic here
        Health = health;
        IsHealthLow = Health < 30f; // Example threshold for low health
    }
}
