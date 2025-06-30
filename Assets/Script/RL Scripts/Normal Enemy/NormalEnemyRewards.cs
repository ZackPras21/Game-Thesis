using UnityEngine;

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
}

