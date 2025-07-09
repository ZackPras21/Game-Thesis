using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class TowerInteract : MonoBehaviour
{
    public int dataNeeded = 2; // Jumlah Data yang dibutuhkan
    public Animator animator;
    private bool isTowerRepaired = false;
    public GameObject text;
    public GameObject boxText;
    public GameObject interactBtn;

    public ParticleSystem particleSystemOne;
    public ParticleSystem particlesSystemTwo;

    public GameObject mainCamera;
    public GameObject sceneCamera;
    private GameObject player;
    private bool isPlayerDetected;
    private bool isInsideTowerTrigger = false;

    public int timeAnimation;


    private void Start()
    {
        animator = GetComponent<Animator>();
        isPlayerDetected = false;
    }

    private void Update()
    {
        if (isTowerRepaired)
        {
            interactBtn.SetActive(false);
            text.SetActive(false);
            boxText.SetActive(false);
        }
        else if (isInsideTowerTrigger && Input.GetKeyDown(KeyCode.F))
        {
            Repair();
        }
        if (!isPlayerDetected)
        {
            if (GameObject.FindGameObjectWithTag("Player"))
            {
                player = GameObject.FindGameObjectWithTag("Player");
            }
        }
    }

    public void Repair()
    {
        if (!isTowerRepaired)
        {
            if (PlayerController.Instance.Data >= dataNeeded)
            {
                // Lakukan perbaikan pada tower
                //Debug.Log("Tower diperbaiki!");
                AudioManager.instance.PlayTower();
                SceneCamera(); 
                PlayerController.Instance.Data -= dataNeeded;
                animator.Play("TowerRecovery");
                isTowerRepaired = true;
                //Instantiate(particleSystemOne, transform.position, rotation, gameObject.transform);
                particleSystemOne.Play();
                particlesSystemTwo.Play();

                interactBtn.SetActive(false);
                text.gameObject.SetActive(false);
                boxText.gameObject.SetActive(false);
                GameProgression.Instance.TowerRecover();
            }
            else
            {
                // Debug.Log("Tidak cukup Data untuk perbaikan.");
            }
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isTowerRepaired)
        {
            isInsideTowerTrigger = true;
            interactBtn.SetActive(true);       
        }
    } 
    void ReturnToMainCamera()
    {
        player.GetComponent<PlayerController>().enabled = true;
        MainCamera();
    }
    public void MainCamera()
    {
        mainCamera.SetActive(true);
        sceneCamera.SetActive(false);
    }

    public void SceneCamera()
    {
        player.GetComponent<PlayerController>().enabled = false;
        player.GetComponent<Animator>().SetFloat("Speed", 0);
        sceneCamera.SetActive(true);
        mainCamera.SetActive(false);
        animator.Play("towerCamera");
        Invoke("ReturnToMainCamera", timeAnimation);
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && other.gameObject.layer == LayerMask.NameToLayer("Hitbox"))
        {
            isInsideTowerTrigger = false;
            interactBtn.SetActive(false);
        }
    }

}
