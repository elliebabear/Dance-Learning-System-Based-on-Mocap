using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class displayScore : MonoBehaviour
{
    public Text scoreText;
    //public recordScore recordscore;
    // Start is called before the first frame update
    void Start()
    {
        string finalscore = PlayerPrefs.GetString("finalScore");
        scoreText.text = finalscore + "/100";
    }

    // Update is called once per frame
    //void Update()
    //{
        
    //}
}
