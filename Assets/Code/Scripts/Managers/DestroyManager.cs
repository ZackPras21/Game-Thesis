using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroyManager : MonoBehaviour
{
    public float Duration;
    void Start()
    {
        Destroy(this.gameObject, Duration);
    }
}
