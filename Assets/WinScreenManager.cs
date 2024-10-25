using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class WinScreenManager : MonoBehaviour
{
    public Image img;
    public void ShowCredit() => StartCoroutine(RouteTransition("Credit_Scene"));
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
        PlayerController.Instance.playerData.playerHealth = PlayerController.Instance.MaxHealth;
        GameManager.Instance.SavePlayer(PlayerController.Instance.playerData);
        GameManager.Instance.SaveGear(PlayerController.Instance.Gear);
        SceneManager.LoadScene(route);
        AudioManager.instance.IsBossPlay = false;
        yield return null;
    }
}
