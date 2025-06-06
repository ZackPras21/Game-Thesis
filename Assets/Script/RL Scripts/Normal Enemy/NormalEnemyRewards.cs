using UnityEngine;

public class NormalEnemyRewards : MonoBehaviour
{
    // === Very large reward (+1) / very large punishment (-1)===
    public float KillPlayerReward          = +1f;
    public float DiedByPlayerPunishment    = -1f;

    // === Large reward/punishment (+0.5 … +1 / -0.5 … -1) ===
    public float DetectPlayerReward        = +0.5f;
    public float FinishDetectReward        = +0.5f;
    public float PatrolCompleteReward      = +1f;
    public float ChasePlayerReward         = +0.6f;
    public float AttackPlayerReward        = +0.8f;
    public float HitByPlayerPunishment     = -0.7f;
    public float ObstaclePunishment        = -0.9f;  

    // === Small reward/punishment (+0.005 … +0.5 / –0.005 … –0.5) ===
    public float PatrolStepReward          = +0.005f; // each step moved while patrolling
    public float ChaseStepReward           = +0.010f;
    public float IdlePunishment            = -0.010f; // standing still too long
    public float NoMovementPunishment      = -0.01f;  // >50 steps stuck
    public float ApproachPlayerReward      = +0.01f;  
    public float StayFarFromPlayerPunishment  = -0.005f; // if distance to player is growing
    public float AttackMissedPunishment       = -0.1f;   // in range but failed to attack

    // Helper: If the agent hasn't moved for > n steps or > 2 seconds, skip the episode.
    public bool CheckIfStuck(
        Vector3 prevPos,
        Vector3 currPos,
        int stepsSinceLastMove,
        float timeSinceLastMove
    )
    {
        // If the agent has barely changed position (within 0.01f) for > 50 steps or > 2 seconds → stuck
        float delta = Vector3.Distance(prevPos, currPos);
        if ((delta < 0.01f && stepsSinceLastMove > 50) ||
            (delta < 0.01f && timeSinceLastMove > 2f))
        {
            return true;
        }
        return false;
    }
}
