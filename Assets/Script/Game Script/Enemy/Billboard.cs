using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Billboard : MonoBehaviour
{
    void LateUpdate()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera != null)
        {
            transform.LookAt(transform.position + mainCamera.transform.forward);
        }
        else
        {
            // Debug.LogWarning("Main camera not found!");
        }
    }
}
