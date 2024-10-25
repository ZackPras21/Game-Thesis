using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScreenSize : MonoBehaviour
{
    private int width, height;
    void Awake()
    {
        width = PlayerPrefs.GetInt("ScreenWidth", Screen.currentResolution.width);
        height = PlayerPrefs.GetInt("ScreenHeight", Screen.currentResolution.height);
    }
    public void WindowedScreen()
    {
        Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
    }
    public void FullScreen()
    {
        Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
        Screen.SetResolution(width, height, true);
    }
}