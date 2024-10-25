using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LaserBeam : MonoBehaviour
{
    public float onDuration = 5f;
    public float offDuration = 2f;
    public GameObject laser;

    private bool isLaserOn = false;
    private float timer;
    // Start is called before the first frame update
    void Start()
    {
        timer = onDuration;
        laser.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        timer -= Time.deltaTime;

        if (timer <= 0)
        {
            isLaserOn = !isLaserOn;
            laser.SetActive(isLaserOn);
            timer = isLaserOn ? onDuration : offDuration;
        }
    }
}
