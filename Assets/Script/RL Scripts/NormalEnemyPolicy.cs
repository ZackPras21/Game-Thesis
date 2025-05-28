using System.Collections.Generic;

public class NormalEnemyPolicy
{
    private Dictionary<NormalEnemyState, EnemyAction> policyMap = new Dictionary<NormalEnemyState, EnemyAction>();

    public EnemyAction GetAction(NormalEnemyState state)
    {
        if (policyMap.ContainsKey(state))
        {
            return policyMap[state];
        }
        return EnemyAction.Idle;
    }

    public void UpdatePolicy(NormalEnemyState state, EnemyAction action)
    {
        policyMap[state] = action;
    }

    public void UpdatePolicy(NormalEnemyState state, EnemyAction action, float reward)
    {
        // Simple policy update based on reward
        if (reward > 0)
        {
            policyMap[state] = action;
        }
    }
}
