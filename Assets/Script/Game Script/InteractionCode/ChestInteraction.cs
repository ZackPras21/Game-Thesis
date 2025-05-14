using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;

public class ChestInteraction : MonoBehaviour
{
    public GameObject spriteButton;
    public GameObject Chest;
    private Animator myAnimator;
    private bool isOpen = false;

    public LootManager lootManager;
    public VFXManager vfxManager;
    private AudioManager audioManager;
    private bool lootManagerSudahMuncul = false;
    public Transform positionParticles;

    bool btnInteract;
    private void Awake()
    {
    }
    void Start()
    {
        myAnimator = GetComponent<Animator>();
        btnInteract = false;
        spriteButton.SetActive(false);
        audioManager = AudioManager.instance;
    }

    void Update()
    {
        if (audioManager == null)
        {
            audioManager = AudioManager.instance;
        }
        if (btnInteract)
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                if (isOpen)
                {
                    CloseChest();
                }
                else
                {
                    OpenChest();
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && other.gameObject.layer == LayerMask.NameToLayer("Hitbox"))
        {
            btnInteract = true;
            spriteButton.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && other.gameObject.layer == LayerMask.NameToLayer("Hitbox"))
        {
            btnInteract = false;
            spriteButton.SetActive(false);
        }
    }

    private void OpenChest()
    {
        isOpen = true;
        myAnimator.Play("ChestOpen");

        if (!lootManagerSudahMuncul)
        {
            lootManager.SpawnDataLoot(transform);
            GameProgression.Instance.ChestClaim();
            vfxManager.StartChestLight(positionParticles);
            audioManager.PlayChest();
            lootManagerSudahMuncul = true;
        }
    }

    private void CloseChest()
    {
        isOpen = false;
    }
}
