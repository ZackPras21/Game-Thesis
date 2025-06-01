using UnityEngine;

public class NormalEnemyRewards : MonoBehaviour
{
    // === Very large reward (+1) / very large punishment (–1) buckets ===
    public float KilledPlayerReward        = +1f;
    public float DiedByPlayerPunishment    = -1f;

    // === Large reward/punishment (+0.5 / –0.5) ===
    public float DetectPlayerReward        = +0.8f;
    public float ChasePlayerReward         = +0.9f;
    public float AttackPlayerReward        = +1f;
    public float LostFightPenalty          = -0.6f;
    public float ObstaclePenalty           = -0.8f;  // bumping into wall

    // === Small reward/punishment (+0.005 … +0.5 / –0.005 … –0.5) ===
    public float PatrolStepReward          = +0.005f; // each step moved while patrolling
    public float IdlePenalty               = -0.010f; // standing still too long
    public float NoMovementPenalty         = -0.01f;  // >50 steps stuck
    public float ApproachPlayerReward      = +0.01f;  // each timestep we get closer to the player
    public float StayFarFromPlayerPenalty  = -0.005f; // if distance to player is growing
    public float AttackMissedPenalty       = -0.1f;   // in range but failed to attack

    // Helper: If the agent hasn't moved for > n steps or > 2 seconds, skip the episode.
    public bool CheckIfStuck(
        Vector3 prevPos,
        Vector3 currPos,
        int      stepsSinceLastMove,
        float    timeSinceLastMove
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
