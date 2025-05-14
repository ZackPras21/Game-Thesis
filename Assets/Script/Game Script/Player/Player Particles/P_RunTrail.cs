using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class P_RunTrail : MonoBehaviour
{
    private ParticleSystem walkTrail;
    private PlayerState playerState;
    private bool IsTrailPlaying;
    // Start is called before the first frame update
    void Start()
    {
        walkTrail = gameObject.GetComponent<ParticleSystem>();
        walkTrail.Stop();
        IsTrailPlaying = false;
    }

    // // Update is called once per frame
    void Update()
    {
        playerState = PlayerController.Instance.playerState;
        if(playerState == PlayerState.Run && !IsTrailPlaying && PlayerController.Instance.velocity > 0.95f){
            walkTrail.Play();
            IsTrailPlaying = true;
        } else if (!(playerState == PlayerState.Run) && IsTrailPlaying == true ){
            walkTrail.Stop();
            IsTrailPlaying = false;
        } else if(PlayerController.Instance.velocity <= 0.95f){
            walkTrail.Stop();
            IsTrailPlaying = false;
        }
    }
}
