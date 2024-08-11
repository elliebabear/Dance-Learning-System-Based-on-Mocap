using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(KinectController))]
public class KinectControllerEditor : Editor
{
    string[] options;
    int selected = 0;
    
    public void Awake()
    {
          options = new string[]
         {
             "WFOV_2x2Binned", "NFOV_2x2Binned", "WFOV_Unbinned","NFOV_Unbinned"
         };
    }
    public override void OnInspectorGUI()
    {
        KinectController myTarget = (KinectController)target;
        
       switch (myTarget.depthMode)
        {
            case "WFOV_2x2Binned":
                selected = 0;
                break;
            case "NFOV_2x2Binned":
                selected = 1;
                break;
            case "WFOV_Unbinned":
                selected = 2;
                break;
            case "NFOV_Unbinned":
                selected = 3;
                break;
        }
        selected = EditorGUILayout.Popup("Depth Mode", selected, options);
        myTarget.depthMode = options[selected];

    }
}
