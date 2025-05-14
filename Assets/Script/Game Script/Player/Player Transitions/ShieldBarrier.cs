using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShieldBarrier : MonoBehaviour
{
    private Animator animator;
    private void Start()
    {
        animator = GetComponent<Animator>();
    }
    public void TurnOnShield()
    {
        if (!animator.GetBool("IsShieldOn"))
        {
            animator.SetBool("IsShieldOn", true);
        }

    }
    public void TurnOffShield()
    {
        animator.SetBool("IsShieldOn", false);
    }
}
