using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VFXManager : MonoBehaviour
{
    [Header("----------------Environment VFX----------------")]
    public GameObject ParticleChestLight;
    public GameObject ParticleTowerOne;
    public GameObject ParticleTowerTwo;
    public GameObject particleTowerOne;
    public GameObject particleTowerTwo;

    [Header("----------------Enemy VFX----------------")]
    public GameObject enemyGettingHit;
    public GameObject creepGettingHitVFX;
    public GameObject medium1GettingHitVFX;
    public GameObject medium2GettingHitVFX;

    public void StartChestLight(Transform transform)
    {
        Instantiate(ParticleChestLight, transform.position, Quaternion.identity, transform);
    }

    public void StartTowerVFXOne(Transform transform)
    {
        Instantiate(particleTowerOne, transform.position, Quaternion.identity, transform);
    }

    public void StartTowerVFXTwo(Transform transform)
    {
        Instantiate(particleTowerTwo, transform.position, Quaternion.identity, transform);
    }

    public void EnemyGettingHit(Transform transform, EnemyType enemyType)
    {
        GameObject vfx = null;
        switch (enemyType)
        {
            case EnemyType.Creep:
                vfx = creepGettingHitVFX;
                break;
            case EnemyType.Medium1:
                vfx = medium1GettingHitVFX;
                break;
            case EnemyType.Medium2:
                vfx = medium2GettingHitVFX;
                break;
        }

        if (vfx != null)
        {
            Instantiate(vfx, transform.position, Quaternion.identity, transform);
        }
    }
}
