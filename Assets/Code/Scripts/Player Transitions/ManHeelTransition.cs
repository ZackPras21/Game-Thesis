using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ManHeelTransition : StateMachineBehaviour
{
    // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
    //override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    //{
    //    
    //}
    int max = 54;
    int currentFrame = 0;
    // OnStateUpdate is called on each Update frame between OnStateEnter and OnStateExit callbacks
    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        currentFrame++;
        switch (currentFrame)
        {
            case 14:
                PlayerController.Instance.Animator.SetBool("IsRightHeel", false);
                break;
            case 32:
                PlayerController.Instance.Animator.SetBool("IsRightHeel", false);
                break;
            case 50:
                PlayerController.Instance.Animator.SetBool("IsRightHeel", false);
                break;
            case 4:
                PlayerController.Instance.Animator.SetBool("IsRightHeel", true);
                break;
            case 22:
                PlayerController.Instance.Animator.SetBool("IsRightHeel", true);
                break;
            case 40:
                PlayerController.Instance.Animator.SetBool("IsRightHeel", true);
                break;
            default:
                break;
        }
        if(currentFrame == max) currentFrame = 0;
    }

    // OnStateExit is called when a transition ends and the state machine finishes evaluating this state
    //override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    //{
    //    
    //}

    // OnStateMove is called right after Animator.OnAnimatorMove()
    //override public void OnStateMove(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    //{
    //    // Implement code that processes and affects root motion
    //}

    // OnStateIK is called right after Animator.OnAnimatorIK()
    //override public void OnStateIK(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    //{
    //    // Implement code that sets up animation IK (inverse kinematics)
    //}
}
