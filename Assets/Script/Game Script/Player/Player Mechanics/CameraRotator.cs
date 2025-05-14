using System.Collections;
using UnityEngine;

public class CameraRotator : MonoBehaviour
{
    public float speed;
    public Camera mainCamera;
    public Camera rotatorCamera;
    public float duration;
    private bool isPlayerDetected = false;
    private PlayerController playerController;
    private void Start()
    {
        playerController = PlayerController.Instance;
    }
    void Update()
    {
        if (playerController != null)
        {
            playerController.currentInputVector = Vector3.zero;
            playerController.Animator.SetFloat("Speed", 0);
        }
        else
            playerController = PlayerController.Instance;
        if (!isPlayerDetected)
        {
            isPlayerDetected = true;
            StartCoroutine(SwitchCamera());
        }
        if (rotatorCamera.enabled)
            transform.Rotate(0, speed * Time.deltaTime, 0);
    }

    IEnumerator SwitchCamera()
    {
        mainCamera.enabled = false;
        rotatorCamera.enabled = true;
        yield return new WaitForSeconds(duration);
        ReturnToMainCamera();
    }

    void ReturnToMainCamera()
    {
        mainCamera.enabled = true;
        rotatorCamera.enabled = false;
        playerController.HasTeleported = true;
        Destroy(gameObject, 1);
    }
}
