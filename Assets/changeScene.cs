using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class changeScene : MonoBehaviour
{
    // Start is called before the first frame update
    public void startLearning()
    {
        SceneManager.LoadScene(1);
    }

    // Update is called once per frame
    public void startEvaluation()
    {
        SceneManager.LoadScene(2);
    }
}
