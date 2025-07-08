using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    public Slider slider;
    public Image fill;
    [SerializeField] private float smoothing = 5f;
    private float healthValue;

    private void Update()
    {
        if (Mathf.Abs(slider.value - healthValue) > 0.01f)
        {
            slider.value = Mathf.Lerp(slider.value, healthValue, Time.deltaTime * smoothing);
        }
        else
        {
            slider.value = healthValue;
        }
    }

    public void SetHealth(int health)
    {
        healthValue = health;
        slider.maxValue = health;
        slider.value = health;
    }

}