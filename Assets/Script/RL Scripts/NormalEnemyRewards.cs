using UnityEngine;

public class NormalEnemyRewards
{
    // Calculate the reward based on the action taken
    public float CalculateReward(NormalEnemyState state, EnemyAction action)
    {
        float reward = 0f;

        // Reward for moving towards the player (closeness is better)
        reward += state.DistanceToPlayer * -0.1f; // Negative for distance, closer is better

        // Penalty for getting hit
        if (action == EnemyAction.TakeDamage)
        {
            reward -= 1f; // Penalty for taking damage
        }

        // Reward for dealing damage
        if (action == EnemyAction.Attack)
        {
            reward += 2f; // Reward for attacking successfully
        }

        // Additional rewards or penalties based on other conditions (e.g., kill)
        if (state.IsHealthLow)
        {
            reward -= 0.5f; // Penalty for being low on health
        }

        return reward;
    }
}
