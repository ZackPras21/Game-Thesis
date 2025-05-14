using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class NPCInteractable : MonoBehaviour
{
    public GameObject dialoguePanel;
    public Text dialogueText;
    public GameObject blackPanel;
    public GameObject boxText;

    public string[] dialogue;
    private int index;
    private GameObject player;

    public GameObject continueButton;
    public float wordSpeed;
    public bool playerIsClose;
    private bool interacted = false;
    public Animator animator;
    private AudioManager audioManager;
    private int talkCount = 0;
    public GameObject HUDStatus;
    private void Start()
    {
        animator = GetComponent<Animator>();
        animator.Play("npc_Hovering");
        audioManager = AudioManager.instance;
        player = GameObject.FindGameObjectWithTag("Player");

    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F) && playerIsClose && !interacted)
        {
            if (dialoguePanel.activeInHierarchy)
            {
                ZeroText();
            }
            else
            {
                HUDStatus.SetActive(false);
                StartCoroutine(Chat());
            }
        }
        if (dialogue.Length != 0 && index < dialogue.Length)
        {
            if (dialogueText.text == dialogue[index])
                continueButton.SetActive(true);
        }
    }

    IEnumerator Chat()
    {
        player.GetComponent<PlayerController>().enabled = false;
        player.GetComponent<Animator>().SetFloat("Speed", 0);
        interacted = true;
        PlayerController.Instance.playerState = PlayerState.Interact;
        audioManager.PlaySFX(audioManager.npcSalto);
        animator.SetBool("IsSalto", true);
        yield return new WaitForSeconds(2.3f);
        blackPanel.SetActive(true);
        dialoguePanel.SetActive(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        StartCoroutine(Typing());
    }

    public void ZeroText()
    {
        if (interacted)
        {
            HUDStatus.SetActive(true);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        player.GetComponent<PlayerController>().enabled = true;
        interacted = false;

        PlayerController.Instance.playerState = PlayerState.Idle;
        dialogueText.text = "";
        index = 0;
        dialoguePanel.SetActive(false);
        blackPanel.SetActive(false);
    }

    IEnumerator Typing()
    {
        if (talkCount == audioManager.npcTalk.Length)
        {
            talkCount = 0;
        }
        audioManager.PlaySFX(audioManager.npcTalk[talkCount]);
        talkCount++;
        foreach (char letter in dialogue[index].ToCharArray())
        {
            dialogueText.text += letter;
            yield return new WaitForSeconds(wordSpeed);
        }
        index++;
    }

    public void NextLine()
    {
        continueButton.SetActive(false);

        if (index < dialogue.Length)
        {
            dialogueText.text = "";
            StartCoroutine(Typing());
        }
        else
        {
            ZeroText();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && other.gameObject.layer == LayerMask.NameToLayer("Hitbox"))
        {
            playerIsClose = true;
            boxText.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && other.gameObject.layer == LayerMask.NameToLayer("Hitbox"))
        {
            playerIsClose = false;
            boxText.SetActive(false);
            ZeroText();
        }
    }
}
