using Unity.MLAgents.Policies;

public static class NormalEnemyPolicy
{
    // Currently empty. If you wanted to add a “Heuristic()” that sets
    // moveX, moveZ, rotateY based on keyboard for debugging, do it here.
    /* public static void Heuristic(
        ref float moveX, ref float moveZ, ref float rotateY
    )
    {
        // Example (WASD + Q/E for rotate):
        moveZ   = Input.GetKey(KeyCode.W) ? 1f : (Input.GetKey(KeyCode.S) ? -1f : 0f);
        moveX   = Input.GetKey(KeyCode.D) ? 1f : (Input.GetKey(KeyCode.A) ? -1f : 0f);
        rotateY = Input.GetKey(KeyCode.E) ? 1f : (Input.GetKey(KeyCode.Q) ? -1f : 0f);
    } */
}
