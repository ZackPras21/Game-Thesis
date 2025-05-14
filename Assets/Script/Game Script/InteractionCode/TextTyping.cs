using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class TextTyping : MonoBehaviour
{
    public float typingSpeed = 0.1f;
    public string fullText;
    private string currentText = "";

    private int index = 0;

    public Text textComponent;

    void Start()
    {
        textComponent = GetComponent<Text>();
        fullText = "Apakah kamu mau permen??";
    }

    void FixedUpdate()
    {
        if (index < fullText.Length)
        {
            currentText += fullText[index];
            textComponent.text = currentText;
            index++;
        }
    }
}
