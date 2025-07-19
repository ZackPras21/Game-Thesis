using UnityEngine;
using Unity.MLAgents;

public class NormalEnemyRewards : MonoBehaviour
{
    #region Reward Configuration
    [Header("Massive Rewards/Punishments (+1 / -1)")]
    public float KillPlayerReward = +1f;
    public float DiedByPlayerPunishment = -1f;

    [Header("Major Rewards/Punishments (+0.5 to +1 / -0.5 to -1)")]
    public float DetectPlayerReward = +0.5f;
    public float PatrolCompleteReward = +0.5f;
    public float ChasePlayerReward = +0.9f;
    public float AttackPlayerReward = +0.8f;
    public float HitByPlayerPunishment = -0.7f;
    public float ObstaclePunishment = -0.8f;

    [Header("Rewards/Punishments (+0.005 to +0.5 / -0.005 to -0.5)")]
    public float PatrolStepReward = +0.015f;
    public float ChaseStepReward = +0.010f;
    public float IdlePunishment = -0.010f;
    public float NoMovementPunishment = -0.015f;
    public float ApproachPlayerReward = +0.01f;
    public float StayFarFromPlayerPunishment = -0.05f;
    public float DoesntChasePlayerPunishment = -0.05f;
    public float AttackIncentive = -0.01f;
    public float AttackMissedPunishment = -0.1f;
    #endregion

    #region Massive Rewards
    public void AddKillPlayerReward(Agent agent) 
    {
        agent.AddReward(KillPlayerReward);
        Debug.Log($"[REWARD] {agent.name} killed player: +{KillPlayerReward}");
    }

    public void AddDeathPunishment(Agent agent)
    {
        agent.AddReward(DiedByPlayerPunishment);
        Debug.Log($"[PUNISHMENT] {agent.name} died: {DiedByPlayerPunishment}");
    }
    #endregion

    #region Major Rewards
    public void AddDetectionReward(Agent agent, float deltaTime)
    {
        float reward = DetectPlayerReward * deltaTime;
        agent.AddReward(reward);
    }

    public void AddPatrolReward(Agent agent)
    {
        agent.AddReward(PatrolCompleteReward);
    }

    public void AddChasePlayerReward(Agent agent)
    {
        agent.AddReward(ChasePlayerReward);
    }

    public void AddAttackReward(Agent agent)
    {
        agent.AddReward(AttackPlayerReward);
    }

    public void AddDamagePunishment(Agent agent)
    {
        agent.AddReward(HitByPlayerPunishment);
    }

    public void AddObstaclePunishment(Agent agent)
    {
        agent.AddReward(ObstaclePunishment);
    }

    #endregion

    #region Rewards

    public void AddPatrolStepReward(Agent agent, float deltaTime)
    {
        agent.AddReward(PatrolStepReward);
    }

    public void AddChaseStepReward(Agent agent, float deltaTime)
    {
        agent.AddReward(ChaseStepReward);
    }

    public void AddIdlePunishment(Agent agent, float deltaTime)
    {
        agent.AddReward(IdlePunishment);
    }

    public void AddNoMovementPunishment(Agent agent, float deltaTime)
    {
        agent.AddReward(NoMovementPunishment);
    }

    public void AddApproachPlayerReward(Agent agent, float deltaTime)
    {
        agent.AddReward(ApproachPlayerReward);
    }

    public void AddStayFarFromPlayerPunishment(Agent agent, float deltaTime)
    {
        agent.AddReward(StayFarFromPlayerPunishment);
    }

    public void AddDoesntChasePlayerPunishment(Agent agent, float deltaTime)
    {
        agent.AddReward(DoesntChasePlayerPunishment);
    }

    public void AddAttackIncentive(Agent agent, float deltaTime) //unimplemented
    {
        agent.AddReward(AttackIncentive);
    }

    public void AddAttackMissedPunishment(Agent agent) //unimplemented
    {
        agent.AddReward(AttackMissedPunishment);
    }

    #endregion
}

