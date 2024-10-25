using UnityEngine;
using UnityEngine.UI;

public class GameTimerText : MonoBehaviour
{
    private Text text;
    private GameTimer gt;
    void Start()
    {
        text = GetComponent<Text>();
        gt = GameTimer.Instance;
    }
    void Update()
    {
        if (gt == null)
        {
            gt = GameTimer.Instance;
        }
        else
        {
            int hours = (int)(gt.elapsedTime / 3600);
            int minutes = (int)(gt.elapsedTime % 3600 / 60);
            int seconds = (int)(gt.elapsedTime % 60);
            text.text = string.Format("{0:D2}:{1:D2}:{2:D2}", hours, minutes, seconds);
        }
    }
}
