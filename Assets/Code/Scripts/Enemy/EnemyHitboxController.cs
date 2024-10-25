using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;

public class EnemyHitboxController : MonoBehaviour
{
    public EnemyController enemyController;

    public void TakeDamage(int damageAmount)
    {
        if (enemyController.enemyHP > 0)
        {
            enemyController.TakeDamage(damageAmount);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
