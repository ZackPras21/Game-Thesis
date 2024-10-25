using UnityEngine;

public class GameTimer : MonoBehaviour
{
    public float elapsedTime = 0f;
    public static GameTimer Instance;
    private bool stop = false;
    private void Awake()
    {
        if (Instance == null) Instance = this;
    }
    private void Update() {
        if(!stop)
        elapsedTime += Time.deltaTime;
    }
    public void Stop(){
        stop = true;
    }
}
