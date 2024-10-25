using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameStartTransition : MonoBehaviour
{
    private Image img;
    private PlayerController player;
    private bool isAlive;
    private void Awake()
    {
        img = GetComponent<Image>();
        img.color = new Color(img.color.r, img.color.g, img.color.b, 255);
    }
    // Start is called before the first frame update
    void Start()
    {
        player = PlayerController.Instance;
        isAlive = player.isAlive;
        StartCoroutine(StartTransition());
    }
    // private void Update()
    // {
    //     // if (player != null)
    //     // {
    //     //     if (!player.isAlive && isAlive)
    //     //     {
    //     //         img.enabled = true;
    //     //         StartCoroutine(DeathTransition());
    //     //         isAlive = false;
    //     //     }
    //     // }
    // }
    private IEnumerator StartTransition()
    {
        while (img.color.a > 0.1)
        {
            img.color = new Color(img.color.r, img.color.g, img.color.b, Mathf.Lerp(img.color.a, 0, Time.deltaTime * 8f));
            yield return null;
        }
        if (img.color.a < 0.1f)
        {
            img.enabled = false;
            yield return null;
        }
        yield return null;
    }
    private IEnumerator DeathTransition()
    {
        yield return new WaitForSeconds(1f);
        while (img.color.a < 1)
        {
            img.color = new Color(img.color.r, img.color.g, img.color.b, Mathf.Lerp(img.color.a, 1, Time.deltaTime * 8f));
            yield return null;
        }
        yield return null;
    }
}
