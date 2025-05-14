using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;

public class BossHitboxController : MonoBehaviour
{
    public Boss3Controller enemyController;

    public void TakeDamage(int damageAmount)
    {
        // Debug.Log("Boss HP: " + enemyController.getBossHP());
        if (enemyController.getBossHP() > 0)
        {
            enemyController.TakeDamage(damageAmount);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
