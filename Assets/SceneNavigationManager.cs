using UnityEngine;

public class SceneNavigationManager : MonoBehaviour
{
    public static SceneNavigationManager Instance; 
    public string NextScene;
    private void Awake()
    {
        if (Instance == null) Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
