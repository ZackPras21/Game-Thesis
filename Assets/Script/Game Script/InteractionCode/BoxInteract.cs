using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoxInteract : MonoBehaviour
{
    //private bool isCrack = false;
    private Animator animator;
    public LootManager lootManager;
    private bool lootManagerSudahMuncul = false;
    private AudioManager audioManager;

    public int hp = 5;

    private void Start()
    {
        audioManager = AudioManager.instance;
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (hp < 0)
        {
            BoxCrack();
        }
    }

    private void BoxCrack()
    {
        //isCrack = true;
        animator.Play("BoxDestruction");

        if (!lootManagerSudahMuncul)
        {
            GameProgression.Instance.BoxDestroy();
            lootManager.SpawnDataLoot(transform);
            lootManagerSudahMuncul = true;
        }

        Destroy(gameObject, 1f);
    }

    public void Damage()
    {
        audioManager.PlayBoxGettingHit(hp);
        hp--;
        animator.SetTrigger("GettingHit");
        //Debug.Log("Damaged. Box HP: " + hp);
        StartCoroutine(Stun());
    }

    private IEnumerator Stun()
    {
        yield return new WaitForSeconds(2);
    }
}
