using UnityEngine;
using Unity.MLAgents;

public class NormalEnemyRewards : MonoBehaviour
{
    [Header("Very Large Rewards/Punishments (+1 / -1)")]
    public float KillPlayerReward = +1f;
    public float DiedByPlayerPunishment = -1f;

    [Header("Large Rewards/Punishments (+0.5 … +1 / -0.5 … -1)")]
    public float DetectPlayerReward = +0.5f;
    public float PatrolCompleteReward = +0.5f;
    public float ChasePlayerReward = +0.9f;
    public float AttackPlayerReward = +0.8f;
    public float HitByPlayerPunishment = -0.7f;
    public float ObstaclePunishment = -0.8f;

    [Header("Small Rewards/Punishments (+0.005 … +0.5 / –0.005 … –0.5)")]
    public float PatrolStepReward = +0.015f;
    public float ChaseStepReward = +0.010f;
    public float IdlePunishment = -0.010f;
    public float NoMovementPunishment = -0.015f;
    public float ApproachPlayerReward = +0.01f;
    public float StayFarFromPlayerPunishment = -0.05f;
    public float DoesntChasePlayerPunishment = -0.05f;
    public float AttackIncentive = -0.01f;
    public float AttackMissedPunishment = -0.1f;

    public class RewardSystem
    {
        private readonly Agent agent;
        private readonly NormalEnemyRewards rewardConfig;

        public RewardSystem(Agent agent, NormalEnemyRewards rewardConfig)
        {
            this.agent = agent;
            this.rewardConfig = rewardConfig;
        }

        public void AddDetectionReward(float deltaTime) => agent.AddReward(rewardConfig.DetectPlayerReward * deltaTime); 
        public void AddIdlePunishment(float deltaTime) => agent.AddReward(rewardConfig.IdlePunishment * deltaTime);
        public void AddPatrolReward() => agent.AddReward(rewardConfig.PatrolCompleteReward);
        public void AddAttackReward() => agent.AddReward(rewardConfig.AttackPlayerReward);
        public void AddKillPlayerReward() => agent.AddReward(rewardConfig.KillPlayerReward);
        public void AddObstaclePunishment() => agent.AddReward(rewardConfig.ObstaclePunishment);
        public void AddDeathPunishment() => agent.AddReward(rewardConfig.DiedByPlayerPunishment);
        public void AddDamagePunishment() => agent.AddReward(rewardConfig.HitByPlayerPunishment);
        public void AddNoMovementPunishment(float deltaTime) => agent.AddReward(rewardConfig.NoMovementPunishment * deltaTime);
        public void AddApproachPlayerReward(float deltaTime) => agent.AddReward(rewardConfig.ApproachPlayerReward * deltaTime);
        public void AddStayFarFromPlayerPunishment(float deltaTime) => agent.AddReward(rewardConfig.StayFarFromPlayerPunishment * deltaTime);
        public void AddDoesntChasePlayerPunishment(float deltaTime) => agent.AddReward(rewardConfig.DoesntChasePlayerPunishment * deltaTime);
        public void AddAttackMissedPunishment() => agent.AddReward(rewardConfig.AttackMissedPunishment);
        public void AddChasePlayerReward() => agent.AddReward(rewardConfig.ChasePlayerReward);
        public void AddChaseStepReward(float deltaTime) => agent.AddReward(rewardConfig.ChaseStepReward * deltaTime);
        public void AddPatrolStepReward(float deltaTime) => agent.AddReward(rewardConfig.PatrolStepReward * deltaTime);
        public void AddAttackIncentive(float deltaTime) => agent.AddReward(rewardConfig.AttackIncentive * deltaTime);
    }
}

