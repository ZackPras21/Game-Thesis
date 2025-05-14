using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InteractTrigger : MonoBehaviour
{
    private Transform player;
    public bool isFollowing = false;
    public int hp = 4;
    public int speed = 1;
    int speedData;

    private void Start() {
        speedData = speed;
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            gameObject.GetComponent<Renderer>().material.SetColor("_Color", Color.red);
            isFollowing = true;
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            gameObject.GetComponent<Renderer>().material.SetColor("_Color", Color.white);
            isFollowing = false;
        }
    }
    private void OnCollisionEnter(Collision other) {
        if (other.gameObject.CompareTag("Player"))
        {
            // Debug.Log("Serang Player");
        }
    }
    private void Update()
    {
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player").GetComponent<Transform>();
        }
        else if (isFollowing)
        {
            transform.position = new Vector3(Mathf.Lerp(transform.position.x, player.position.x, Time.deltaTime * speed), transform.position.y, Mathf.Lerp(transform.position.z, player.position.z, Time.deltaTime * speed));
        }
        if (hp <= 0)
        {
            Destroy(gameObject, 1f);
        }
    }
    public void Damage()
    {
        hp--;
        // Debug.Log("Ouch Damaged. Enemy HP: " + hp);
        speed = 0;
        StartCoroutine(Stun());
    }
    private IEnumerator Stun()
    {
        yield return new WaitForSeconds(2);
        speed = speedData;
    }
}
