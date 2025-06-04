using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class WeaponController : MonoBehaviour
{
    private PlayerController player;
    private BoxCollider _collider;
    private void Start()
    {
        player = PlayerController.Instance;
        _collider = GetComponent<BoxCollider>();
        _collider.enabled = false;
    }
    private void Update()
    {
        if (player.playerState == PlayerState.Attack1 || player.playerState == PlayerState.Attack2 || player.playerState == PlayerState.Attack3 || player.playerState == PlayerState.DashAttack || player.playerState == PlayerState.SkillAttack)
        {
            _collider.enabled = true;
        }
        else
        {
            _collider.enabled = false;
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other != null && other.gameObject != null)
        {
            if (other.gameObject.CompareTag("Enemy") && other.gameObject.layer == LayerMask.NameToLayer("Hitbox"))
            {
                var hitbox = other.gameObject.GetComponent<EnemyHitboxController>();
                if (hitbox != null && player != null && player.playerData != null)
                {
                    hitbox.TakeDamage(player.playerData.playerAttack);
                }
            }
            else if (other.gameObject.CompareTag("Player"))
            {
                var target = other.gameObject.GetComponent<RL_Player>();
                if (target != null && player != null && player.playerData != null)
                {
                    target.DamagePlayer(player.playerData.playerAttack);
                }
            }
        }

        if (other.gameObject.CompareTag("Boss") && other.gameObject.layer == LayerMask.NameToLayer("Hitbox"))
        {
            // Debug.Log("Hit Boss");
            other.gameObject.GetComponent<BossHitboxController>().TakeDamage(player.playerData.playerAttack);
        }

        if (other.gameObject.CompareTag("Box"))
        {
            // Debug.Log("Hit Box");
            other.gameObject.GetComponent<BoxInteract>().Damage();
        }

        if (other.gameObject.CompareTag("BoxHealth"))
        {
            other.gameObject.GetComponent<BoxHealth>().Damage();
        }
    }
}