using UnityEngine;

public class CameraFollowTrain : MonoBehaviour
{
    private Transform playerPos;
    public float X, Y, Z;
    private float _mainOrthographicSize;
    public float MainOrthographicSize;
    public float CombatOrthographicSize;
    public float shakeAmount = 3f;
    float i = 0;

    private void Start()
    {
        //playerPos = GameObject.FindGameObjectWithTag("PlayerHead")?.transform;
        // _mainOrthographicSize = MainOrthographicSize;
    }

    private void Update()
    {
        //
    }
    
    public void CombatMode()
    {
        MainOrthographicSize = CombatOrthographicSize;
    }
    public void NormalMode()
    {
        MainOrthographicSize = _mainOrthographicSize;
    }
}
