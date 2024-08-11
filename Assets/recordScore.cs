using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class recordScore : MonoBehaviour
{
    KinectController kinectController;

    public GameObject sss;

    private int score;
    private int frameNum;

    private Animator aTeacher;
    private Animator aStudent;

    private float tleftFoot, sleftFoot;
    private float trightFoot, srightFoot;
    private float tleftHand, sleftHand;
    private float trightHand, srightHand;

    void Awake()
    {
        kinectController = FindObjectOfType<KinectController>();
        if (kinectController == null)
        {
            print("requires a kinect controller");
            return;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        score = 0;
        frameNum = 0;
        tleftFoot = 0.0f;
        sleftFoot = 0.0f;
        trightFoot = 0.0f;
        srightFoot = 0.0f;
        tleftHand = 0.0f;
        sleftHand = 0.0f;
        trightHand = 0.0f;
        srightHand = 0.0f;

        aTeacher = this.GetComponent<Animator>();
        aStudent = sss.GetComponent<Animator>();
        aTeacher.SetBool("start", false);
    }

    // Update is called once per frame
    void Update()
    {
        frameNum++;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SceneManager.LoadScene(0);
        }

        if (kinectController == null)
        {
            Debug.Log("fail to get kinectController");
            return;
        }

        List<SkeletonInfo> skeles = kinectController.m_currentSkeletons;
        if (skeles != null)
        {
            aTeacher.SetBool("start", true);
        }

        AnimatorStateInfo stateInfo = aTeacher.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName("Dance003_mcp") && stateInfo.normalizedTime >= 0.95f)
        {
            score = (int)((Mathf.Min(srightFoot, trightFoot) / Mathf.Max(srightFoot, trightFoot)) + (Mathf.Min(sleftFoot, tleftFoot) / Mathf.Max(sleftFoot, tleftFoot))
                  + (Mathf.Min(srightHand, trightHand) / Mathf.Max(srightHand, trightHand)) + (Mathf.Min(sleftHand, tleftHand) / Mathf.Max(sleftHand, tleftHand))) * 25;
            PlayerPrefs.SetString("finalScore", score.ToString());
            SceneManager.LoadScene(3);
        }

        if (frameNum % 15 == 0)
        {
            float dotProduct;
            Transform shoulder, elbow, hand, hip, knee, foot;

            shoulder = aTeacher.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            elbow = aTeacher.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            hand = aTeacher.GetBoneTransform(HumanBodyBones.LeftHand);
            dotProduct = Vector3.Dot((shoulder.position - elbow.position).normalized, (hand.position - elbow.position).normalized);
            tleftHand = tleftHand + Mathf.Acos(dotProduct) * Mathf.Rad2Deg;

            shoulder = aStudent.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            elbow = aStudent.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            hand = aStudent.GetBoneTransform(HumanBodyBones.LeftHand);
            dotProduct = Vector3.Dot((shoulder.position - elbow.position).normalized, (hand.position - elbow.position).normalized);
            sleftHand = sleftHand + Mathf.Acos(dotProduct) * Mathf.Rad2Deg;

            shoulder = aTeacher.GetBoneTransform(HumanBodyBones.RightUpperArm);
            elbow = aTeacher.GetBoneTransform(HumanBodyBones.RightLowerArm);
            hand = aTeacher.GetBoneTransform(HumanBodyBones.RightHand);
            dotProduct = Vector3.Dot((shoulder.position - elbow.position).normalized, (hand.position - elbow.position).normalized);
            trightHand = trightHand + Mathf.Acos(dotProduct) * Mathf.Rad2Deg;

            shoulder = aStudent.GetBoneTransform(HumanBodyBones.RightUpperArm);
            elbow = aStudent.GetBoneTransform(HumanBodyBones.RightLowerArm);
            hand = aStudent.GetBoneTransform(HumanBodyBones.RightHand);
            dotProduct = Vector3.Dot((shoulder.position - elbow.position).normalized, (hand.position - elbow.position).normalized);
            srightHand = srightHand + Mathf.Acos(dotProduct) * Mathf.Rad2Deg;

            hip = aTeacher.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            knee = aTeacher.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            foot = aTeacher.GetBoneTransform(HumanBodyBones.LeftFoot);
            dotProduct = Vector3.Dot((hip.position - knee.position).normalized, (foot.position - knee.position).normalized);
            tleftFoot = tleftFoot + Mathf.Acos(dotProduct) * Mathf.Rad2Deg;

            hip = aStudent.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            knee = aStudent.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            foot = aStudent.GetBoneTransform(HumanBodyBones.LeftFoot);
            dotProduct = Vector3.Dot((hip.position - knee.position).normalized, (foot.position - knee.position).normalized);
            sleftFoot = sleftFoot + Mathf.Acos(dotProduct) * Mathf.Rad2Deg;

            hip = aTeacher.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            knee = aTeacher.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            foot = aTeacher.GetBoneTransform(HumanBodyBones.RightFoot);
            dotProduct = Vector3.Dot((hip.position - knee.position).normalized, (foot.position - knee.position).normalized);
            trightFoot = trightFoot + Mathf.Acos(dotProduct) * Mathf.Rad2Deg;

            hip = aStudent.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            knee = aStudent.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            foot = aStudent.GetBoneTransform(HumanBodyBones.RightFoot);
            dotProduct = Vector3.Dot((hip.position - knee.position).normalized, (foot.position - knee.position).normalized);
            srightFoot = srightFoot + Mathf.Acos(dotProduct) * Mathf.Rad2Deg;
        }
    }
}
