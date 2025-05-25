using UnityEngine;

public class CameraFollow : MonoBehaviour
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
        playerPos = GameObject.FindGameObjectWithTag("PlayerHead")?.transform;
        _mainOrthographicSize = MainOrthographicSize;
    }

    private void Update()
    {
        if (playerPos == null)
            return;

        if (PlayerController.Instance.isAlive)
        {
            if (i < 1.5f)
                i += Time.deltaTime;

            if (i > 1.4f)
            {
                Follow();
            }
        }
        if (PlayerController.Instance.playerState == PlayerState.Hurt || PlayerController.Instance.playerState == PlayerState.DashAttack || PlayerController.Instance.playerState == PlayerState.SkillAttack || PlayerController.Instance.playerState == PlayerState.Attack1 || PlayerController.Instance.playerState == PlayerState.Attack2 || PlayerController.Instance.playerState == PlayerState.Attack3)
        {
            if (PlayerController.Instance.playerState == PlayerState.Hurt)
                transform.position = new Vector3(transform.position.x, transform.position.y + (Random.insideUnitSphere.y * 0.03f), transform.position.z);
            else
                transform.position = new Vector3(transform.position.x, transform.position.y + (Random.insideUnitSphere.y * 0.01f), transform.position.z);
        }
    }
    private void Follow()
    {
        transform.position = new Vector3(
            Mathf.Lerp(transform.position.x, playerPos.position.x + X, Time.deltaTime * 4),
            Mathf.Lerp(transform.position.y, playerPos.position.y + Y, Time.deltaTime),
            Mathf.Lerp(transform.position.z, playerPos.position.z - Z, Time.deltaTime * 3)
        );

        var camera = GetComponent<Camera>();
        if (camera != null)
            camera.orthographicSize = Mathf.Lerp(camera.orthographicSize, MainOrthographicSize, Time.deltaTime);
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
