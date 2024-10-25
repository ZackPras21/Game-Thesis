using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AudioSliderController : MonoBehaviour
{
    public Slider sfx;
    public Slider bgm;
    private AudioManager audioManager;
    void Start()
    {
        CallAudioManager();
    }
    void Update()
    {
        if (audioManager == null)
        {
            CallAudioManager(); 
        }
    }
    void CallAudioManager()
    {
        audioManager = AudioManager.instance;
        sfx.value = audioManager.GetSFX();
        bgm.value = audioManager.GetBGM();
    }
    public void SetSFX(){
        audioManager.SetSFXSliderVolume(sfx.value);
    }
    public void SetBGM(){
        audioManager.SetMusicSliderVolume(bgm.value);
    }
}
