using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class GradeText : MonoBehaviour
{
    private GameTimer gt;
    public Text gradedScore;
    bool graded = false;
    void Start()
    {
        gt = GameTimer.Instance;
    }
    void Update()
    {
        if (gt == null) gt = GameTimer.Instance;
        Debug.Log("GT " + gt.elapsedTime);
        if (!graded)
        {
            if (gt.elapsedTime <= 900f)
                gradedScore.text = "S";
            else if (gt.elapsedTime <= 1200f)
                gradedScore.text = "A";
            else if (gt.elapsedTime <= 1500f)
                gradedScore.text = "B";
            else if (gt.elapsedTime <= 1800f)
                gradedScore.text = "C";
            else
                gradedScore.text = "D";
            graded = true;
        }
    }
}
