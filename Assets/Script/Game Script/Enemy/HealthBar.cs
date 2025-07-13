using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    public Slider slider;
    public Image fill;
    public float smoothSpeed = 6f; 

    private float _targetHealth; 
    private bool _initialized = false; 

    public void SetMaxHealth(int health)
    {
        slider.maxValue = health;
        slider.value = health;
        _targetHealth = health; 
        _initialized = true; 
    }

    public void SetHealth(int health)
    {
        _targetHealth = health; 
    }

    void Update()
    {
        if (_initialized)
        {
            slider.value = Mathf.Lerp(slider.value, _targetHealth, smoothSpeed * Time.deltaTime);
        }
    }
}