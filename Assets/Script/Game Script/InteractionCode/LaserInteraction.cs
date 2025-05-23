using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LaserInteraction : MonoBehaviour
{
    public int damageAmount = 10;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    private void OnTriggerEnter(Collider other)
    {
        // Debug.Log(other.name);

        if (other.CompareTag("Player") && other.gameObject.layer == LayerMask.NameToLayer("Hitbox"))
        {
            PlayerController.Instance.DamagePlayer(damageAmount, null, transform.position);
        }
    }
}
