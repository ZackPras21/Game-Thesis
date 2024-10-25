using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class Intro : MonoBehaviour
{
    public float wait_time = 4f;
    public Image img;
    void Start()
    {
        StartCoroutine(StartTransition());
        if(wait_time > 0f)
        StartCoroutine(Wait_for_intro());
    }

    private IEnumerator StartTransition()
    {
        float speed = 6;
        if(wait_time == 0){
            speed = 2.5f;
        }
        while (img.color.a > 0.1)
        {
            img.color = new Color(img.color.r, img.color.g, img.color.b, Mathf.Lerp(img.color.a, 0, Time.deltaTime * speed));
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
            img.color = new Color(img.color.r, img.color.g, img.color.b, Mathf.Lerp(img.color.a, 1, Time.deltaTime * 5f));
            yield return null;
        }
        yield return new WaitForSeconds(0.5f);
        SceneManager.LoadScene("Menu 3D");
        yield return null;
    }

    IEnumerator Wait_for_intro()
    {
        yield return new WaitForSeconds(wait_time);
        StartCoroutine(LoadGameTransition());
    }
}
