using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;

public class NPCUpgrade : MonoBehaviour
{
    public GameObject text;
    public GameObject boxText;
    public GameObject spriteButton;
    public GameObject upgradeSystem;

    bool btnInteract;

    private void Start()
    {
        btnInteract = false;
        spriteButton.SetActive(false);
        boxText.SetActive(false);
    }

    private void Update()
    {
        if (btnInteract)
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                ShowUpgrade();
            }
        }    
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && other.gameObject.layer == LayerMask.NameToLayer("Hitbox"))
        {
            btnInteract = true;
            //text.SetActive(true);
            spriteButton.SetActive(true);
            boxText .SetActive(true);
        }
    }
    private void OnTriggerExit(Collider other)
    {
        btnInteract = false;
        //text.SetActive(false);
        spriteButton.SetActive(false);
        boxText?.SetActive(false);
    }

    private void ShowUpgrade()
    {
        upgradeSystem.SetActive(true);
    }
}
