using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LoseScreenManager : MonoBehaviour
{
    public Image img;
    public void RestartGame() => StartCoroutine(RouteTransition("NewLevelOne"));
    public void BackToMainMenu() => StartCoroutine(RouteTransition("Menu 3D"));
    private IEnumerator RouteTransition(string route)
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        img.gameObject.SetActive(true);
        yield return new WaitForSeconds(1f);
        while (img.color.a < 0.98)
        {
            img.color = new Color(img.color.r, img.color.g, img.color.b, Mathf.Lerp(img.color.a, 1, Time.deltaTime * 2f));
            yield return null;
        }
        SceneNavigationManager.Instance.NextScene = route;
        SceneManager.LoadScene("Loading");
        yield return null;
    }
}
