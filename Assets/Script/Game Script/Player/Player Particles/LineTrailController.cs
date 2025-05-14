using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LineTrailController : MonoBehaviour
{
    // Start is called before the first frame update
    public TrailRenderer LineRenderers;
    private PlayerController playerController;
    void Start()
    {
        playerController = PlayerController.Instance;
    }

    // Update is called once per frame
    void Update()
    {
        if (playerController == null)
            playerController = PlayerController.Instance;
        if (!LineRenderers.enabled && (playerController.playerState == PlayerState.DashAttack || playerController.playerState == PlayerState.Attack1 || playerController.playerState == PlayerState.Attack1 || playerController.playerState == PlayerState.Attack2 || playerController.playerState == PlayerState.Attack3 || playerController.playerState == PlayerState.SkillAttack))
            LineRenderers.enabled = true;
        else if (LineRenderers.enabled)
            LineRenderers.enabled = false;
    }
}
