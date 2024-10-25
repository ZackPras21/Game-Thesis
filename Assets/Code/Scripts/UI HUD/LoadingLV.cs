using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LoadingLV : MonoBehaviour
{
    public Transform masukanLoadingbar;
    public Image img;

    [SerializeField]
    private float nilaiSekarang;
    [SerializeField]
    private float nilaiKecepatan;
    private void Awake()
    {
        img.color = new Color(img.color.r, img.color.g, img.color.b, 255);
    }
    void Start()
    {
        StartCoroutine(StartTransition());
    }
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
    private IEnumerator LoadGameTransition()
    {
        img.enabled = true;
        while (img.color.a < 0.99)
        {
            img.color = new Color(img.color.r, img.color.g, img.color.b, Mathf.Lerp(img.color.a, 1, Time.deltaTime * 4f));
            yield return null;
        }
        // Debug.Log("Load Scene");
        SceneManager.LoadSceneAsync(SceneNavigationManager.Instance.NextScene);
        yield return null;
    }
    // Update is called once per frame
    void Update()
    {
        if (nilaiSekarang < 100)
        {
            nilaiSekarang += nilaiKecepatan * Time.deltaTime * 0.4f;
            // Debug.Log((int)nilaiSekarang);
        }
        else
        {

            if (img.enabled == false)
            {
                StartCoroutine(LoadGameTransition());
            }
        }
        masukanLoadingbar.GetComponent<Image>().fillAmount = nilaiSekarang / 100;

    }
}
