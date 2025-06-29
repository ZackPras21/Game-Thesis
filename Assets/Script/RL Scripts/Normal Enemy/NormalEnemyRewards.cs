using UnityEngine;

public class NormalEnemyRewards : MonoBehaviour
{
    [Header("Very Large Rewards/Punishments (+1 / -1)")]
    public float KillPlayerReward = +1f; //
    public float DiedByPlayerPunishment = -1f; //

    [Header("Large Rewards/Punishments (+0.5 … +1 / -0.5 … -1)")]
    public float DetectPlayerReward = +0.5f; //
    public float PatrolCompleteReward = +0.5f; 
    public float ChasePlayerReward = +0.9f; // 
    public float AttackPlayerReward = +0.8f; //     
    public float HitByPlayerPunishment = -0.7f; //
    public float ObstaclePunishment = -0.8f;

    [Header("Small Rewards/Punishments (+0.005 … +0.5 / –0.005 … –0.5)")]
    public float PatrolStepReward = +0.005f;
    public float ChaseStepReward = +0.010f; //
    public float IdlePunishment = -0.010f; 
    public float NoMovementPunishment = -0.01f; 
    public float ApproachPlayerReward = +0.01f; 
    public float StayFarFromPlayerPunishment = -0.05f; //
    public float DoesntChasePlayerPunishment = -0.05f; //
    public float AttackIncentive = -0.01f;  //
    public float AttackMissedPunishment = -0.1f; //

    private const float STUCK_POSITION_THRESHOLD = 0.01f;
    private const int STUCK_STEP_LIMIT = 50;
    private const float STUCK_TIME_LIMIT = 2f;

    public bool CheckIfStuck(Vector3 previousPosition, Vector3 currentPosition, int stepsSinceLastMove, float timeSinceLastMove)
    {
        float positionDelta = Vector3.Distance(previousPosition, currentPosition);
        
        return IsStuckBySteps(positionDelta, stepsSinceLastMove) || IsStuckByTime(positionDelta, timeSinceLastMove);
    }

    private bool IsStuckBySteps(float positionDelta, int stepsSinceLastMove)
    {
        return positionDelta < STUCK_POSITION_THRESHOLD && stepsSinceLastMove > STUCK_STEP_LIMIT;
    }

    private bool IsStuckByTime(float positionDelta, float timeSinceLastMove)
    {
        return positionDelta < STUCK_POSITION_THRESHOLD && timeSinceLastMove > STUCK_TIME_LIMIT;
    }
}