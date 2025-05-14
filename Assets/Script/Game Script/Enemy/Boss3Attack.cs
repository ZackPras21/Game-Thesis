using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Boss3Attack : MonoBehaviour
{
    private float damage;
    private bool attacked;

    void Start()
    {
        attacked = false;
        damage = BossEnemyData.Instance.enemyAttack;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && other.gameObject.layer == LayerMask.NameToLayer("Hitbox"))
        {
            if (!attacked){
                // Debug.Log("Player Hit By Boss 3");
                attacked = true;
                PlayerController.Instance.DamagePlayer((int)damage, knockback, transform.position);
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player") && other.gameObject.layer == LayerMask.NameToLayer("Hitbox"))
        {
            if (!attacked){
                // Debug.Log("Player Hit By Boss 3");
                attacked = true;
                PlayerController.Instance.DamagePlayer((int)damage, knockback, transform.position);
            }
        }
    }

    IEnumerator knockback()
    {
        // Debug.Log("Knock Back From Player Parry");
        yield return null;
    }
}
