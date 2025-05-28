using UnityEngine;

public class NormalEnemyRewards
{
    public float CalculateReward(NormalEnemyState state, EnemyAction action)
    {
        float reward = 0f;

        // Reward for moving towards player
        reward += state.DistanceToPlayer * -0.1f; // Closer is better

        // Penalty for getting hit
        if (action == EnemyAction.TakeDamage)
        {
            reward -= 10f;
        }

        // Reward for dealing damage
        if (action == EnemyAction.Attack)
        {
            reward += 5f;
        }

        // Reward for being in attack range
        if (state.PlayerInRange)
        {
            reward += 1f;
        }

        return reward;
    }
}
