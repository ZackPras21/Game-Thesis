using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class IntroStory : MonoBehaviour
{
    public float wait_time = 18f;
    public Image img;
    void Start()
    {
        StartCoroutine(StartTransition());
        StartCoroutine(Wait_for_intro());
    }

    private IEnumerator StartTransition()
    {
        while (img.color.a > 0.1)
        {
            img.color = new Color(img.color.r, img.color.g, img.color.b, Mathf.Lerp(img.color.a, 0, Time.deltaTime * 4f));
            yield return null;
        }
        if (img.color.a < 0.1f)
        {
            img.enabled = false;
            yield return null;
        }
        yield return null;
    }
    private IEnumerator LoadGameTransition()
    {
        img.enabled = true;
        while (img.color.a < 0.99)
        {
            img.color = new Color(img.color.r, img.color.g, img.color.b, Mathf.Lerp(img.color.a, 1, Time.deltaTime * 4f));
            yield return null;
        }
        Debug.Log("Load Scene");
        SceneManager.LoadScene("NewLevelOne");
        yield return null;
    }

    IEnumerator Wait_for_intro()
    {
        yield return new WaitForSeconds(wait_time);
        StartCoroutine(LoadGameTransition());
    }
}
