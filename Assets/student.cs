using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using Microsoft.Azure.Kinect.Sensor;
using Microsoft.Azure.Kinect.BodyTracking;

public class student : MonoBehaviour
{

    KinectController kinectController;

    private Vector3 initPos;
    Animator animator;
    private Transform root, spine, neck, head, leye, reye, lshoulder, lelbow, lhand, lthumb2, lmid1, rshoulder, relbow, rhand, rthumb2, rmid1, lhip, lknee, lfoot, ltoe, rhip, rknee, rfoot, rtoe;
    private Quaternion midRoot, midSpine, midNeck, midHead, midLshoulder, midLelbow, midLhand, midRshoulder, midRelbow, midRhand, midLhip, midLknee, midLfoot, midRhip, midRknee, midRfoot;
    private Transform final;
    private float smoothFactor = 0.6f;

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
        animator = this.GetComponent<Animator>();
        final = this.GetComponent<Transform>();
        /////////////////////////////////////////////////////////////////////////////////////////////////
        
        root = animator.GetBoneTransform(HumanBodyBones.Hips);
        spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        neck = animator.GetBoneTransform(HumanBodyBones.Neck);
        head = animator.GetBoneTransform(HumanBodyBones.Head);
        leye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
        reye = animator.GetBoneTransform(HumanBodyBones.RightEye);
        
        lshoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        lelbow = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        lhand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        lthumb2 = animator.GetBoneTransform(HumanBodyBones.LeftThumbIntermediate);
        lmid1 = animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
        
        rshoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        relbow = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        rhand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        rthumb2 = animator.GetBoneTransform(HumanBodyBones.RightThumbIntermediate);
        rmid1 = animator.GetBoneTransform(HumanBodyBones.RightMiddleDistal);
        
        lhip = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        lknee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        lfoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        ltoe = animator.GetBoneTransform(HumanBodyBones.LeftToes);
        
        rhip = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        rknee = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        rfoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        rtoe = animator.GetBoneTransform(HumanBodyBones.RightToes);

        initPos = root.position;
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        
        Vector3 forward = TriangleNormal(root.position, lknee.position, rknee.position);
        
        midRoot = Quaternion.Inverse(root.rotation) * Quaternion.LookRotation(forward);
        
        midSpine = Quaternion.Inverse(spine.rotation) * Quaternion.LookRotation(spine.position - neck.position, forward);
        midNeck = Quaternion.Inverse(neck.rotation) * Quaternion.LookRotation(neck.position - head.position, forward);
        
        midHead = Quaternion.Inverse(head.rotation) * Quaternion.LookRotation(leye.position * 0.5f + reye.position * 0.5f - head.position);
        
        midLshoulder = Quaternion.Inverse(lshoulder.rotation) * Quaternion.LookRotation(lshoulder.position - lelbow.position, forward);
        midLelbow = Quaternion.Inverse(lelbow.rotation) * Quaternion.LookRotation(lelbow.position - lhand.position, forward);
        midLhand = Quaternion.Inverse(lhand.rotation) * Quaternion.LookRotation(
            lthumb2.position - lmid1.position,
            TriangleNormal(lhand.position, lthumb2.position, lmid1.position)
            );
        
        midRshoulder = Quaternion.Inverse(rshoulder.rotation) * Quaternion.LookRotation(rshoulder.position - relbow.position, forward);
        midRelbow = Quaternion.Inverse(relbow.rotation) * Quaternion.LookRotation(relbow.position - rhand.position, forward);
        midRhand = Quaternion.Inverse(rhand.rotation) * Quaternion.LookRotation(
            rthumb2.position - rmid1.position,
            TriangleNormal(rhand.position, rthumb2.position, rmid1.position)
            );
        
        midLhip = Quaternion.Inverse(lhip.rotation) * Quaternion.LookRotation(lhip.position - lknee.position, forward);
        midLknee = Quaternion.Inverse(lknee.rotation) * Quaternion.LookRotation(lknee.position - lfoot.position, forward);
        midLfoot = Quaternion.Inverse(lfoot.rotation) * Quaternion.LookRotation(lfoot.position - ltoe.position, lknee.position - lfoot.position);
        
        midRhip = Quaternion.Inverse(rhip.rotation) * Quaternion.LookRotation(rhip.position - rknee.position, forward);
        midRknee = Quaternion.Inverse(rknee.rotation) * Quaternion.LookRotation(rknee.position - rfoot.position, forward);
        midRfoot = Quaternion.Inverse(rfoot.rotation) * Quaternion.LookRotation(rfoot.position - rtoe.position, rknee.position - rfoot.position);

        //Debug.Log(initPos);
    }

    Vector3 TriangleNormal(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 d1 = a - b;
        Vector3 d2 = a - c;

        Vector3 dd = Vector3.Cross(d1, d2);
        dd.Normalize();
        //dd.normalized;

        return dd;
    }


    // Update is called once per frame
    void Update()
    {
        if (kinectController == null)
        {
            Debug.Log("fail to get kinectController");
            return;
        }

        List<SkeletonInfo> skeles = kinectController.m_currentSkeletons;
        if (skeles == null)
        {
            return;
        }
        SkeletonInfo sk = skeles[0];
        // rShldrBend 0, rForearmBend 1, rHand 2, rThumb2 3, rMid1 4,
        // lShldrBend 5, lForearmBend 6, lHand 7, lThumb2 8, lMid1 9,
        // lEar 10, lEye 11, rEar 12, rEye 13, Nose 14,
        // rThighBend 15, rShin 16, rFoot 17, rToe 18,
        // lThighBend 19, lShin 20, lFoot 21, lToe 22,    
        // abdomenUpper 23,
        // hip 24, head 25, neck 26, spine 27
        // 
        ///////////////////////////////////////////
        float tallShin = (Vector3.Distance(new Vector3(sk.skeleton.GetJoint(19).Position.X, sk.skeleton.GetJoint(19).Position.Y, sk.skeleton.GetJoint(19).Position.Z),
                                           new Vector3(sk.skeleton.GetJoint(20).Position.X, sk.skeleton.GetJoint(20).Position.Y, sk.skeleton.GetJoint(20).Position.Z))
                       + Vector3.Distance(new Vector3(sk.skeleton.GetJoint(23).Position.X, sk.skeleton.GetJoint(23).Position.Y, sk.skeleton.GetJoint(23).Position.Z),
                                          new Vector3(sk.skeleton.GetJoint(24).Position.X, sk.skeleton.GetJoint(24).Position.Y, sk.skeleton.GetJoint(24).Position.Z))) / 2.0f;
        float tallThigh = (Vector3.Distance(new Vector3(sk.skeleton.GetJoint(18).Position.X, sk.skeleton.GetJoint(18).Position.Y, sk.skeleton.GetJoint(18).Position.Z),
                                           new Vector3(sk.skeleton.GetJoint(19).Position.X, sk.skeleton.GetJoint(19).Position.Y, sk.skeleton.GetJoint(19).Position.Z))
                       + Vector3.Distance(new Vector3(sk.skeleton.GetJoint(22).Position.X, sk.skeleton.GetJoint(22).Position.Y, sk.skeleton.GetJoint(22).Position.Z),
                                          new Vector3(sk.skeleton.GetJoint(23).Position.X, sk.skeleton.GetJoint(23).Position.Y, sk.skeleton.GetJoint(23).Position.Z))) / 2.0f;
        float tallUnity = (Vector3.Distance(lhip.position, lknee.position) + Vector3.Distance(lknee.position, lfoot.position) + Vector3.Distance(rhip.position, rknee.position) + Vector3.Distance(rknee.position, rfoot.position)) / 2.0f;
        //root.position = new Vector3(sk.skeleton.GetJoint(0).Position.X * (tallUnity / (tallThigh + tallShin)), sk.skeleton.GetJoint(0).Position.Y * (tallUnity / (tallThigh + tallShin)), sk.skeleton.GetJoint(0).Position.Z * (tallUnity / (tallThigh + tallShin)));
        root.position = new Vector3(sk.skeleton.GetJoint(0).Position.X * (tallUnity / (tallThigh + tallShin)), sk.skeleton.GetJoint(0).Position.Y * (tallUnity / (tallThigh + tallShin)) - 2.16f, sk.skeleton.GetJoint(0).Position.Z * (tallUnity / (tallThigh + tallShin)));

        /////////////////////////////////////////
        Vector3 forward = TriangleNormal(new Vector3(sk.skeleton.GetJoint(0).Position.X, sk.skeleton.GetJoint(0).Position.Y, sk.skeleton.GetJoint(0).Position.Z),
                                         new Vector3(sk.skeleton.GetJoint(19).Position.X, sk.skeleton.GetJoint(19).Position.Y, sk.skeleton.GetJoint(19).Position.Z),
                                         new Vector3(sk.skeleton.GetJoint(23).Position.X, sk.skeleton.GetJoint(23).Position.Y, sk.skeleton.GetJoint(23).Position.Z));
        
        root.rotation = Quaternion.LookRotation(forward) * Quaternion.Inverse(midRoot);
        
        spine.rotation = Quaternion.LookRotation(new Vector3(sk.skeleton.GetJoint(1).Position.X, sk.skeleton.GetJoint(1).Position.Y, sk.skeleton.GetJoint(1).Position.Z)
                                               - new Vector3(sk.skeleton.GetJoint(3).Position.X, sk.skeleton.GetJoint(3).Position.Y, sk.skeleton.GetJoint(3).Position.Z),
                                                 forward) * Quaternion.Inverse(midSpine) * Quaternion.Euler(0, 180, 0);
        neck.rotation = Quaternion.LookRotation(new Vector3(sk.skeleton.GetJoint(3).Position.X, sk.skeleton.GetJoint(3).Position.Y, sk.skeleton.GetJoint(3).Position.Z)
                                               - new Vector3(sk.skeleton.GetJoint(26).Position.X, sk.skeleton.GetJoint(26).Position.Y, sk.skeleton.GetJoint(26).Position.Z),
                                               forward) * Quaternion.Inverse(midNeck);
        
        head.rotation = Quaternion.LookRotation(new Vector3(sk.skeleton.GetJoint(27).Position.X, sk.skeleton.GetJoint(27).Position.Y, sk.skeleton.GetJoint(27).Position.Z)
                                               - new Vector3(sk.skeleton.GetJoint(26).Position.X, sk.skeleton.GetJoint(26).Position.Y, sk.skeleton.GetJoint(26).Position.Z),
            TriangleNormal(new Vector3(sk.skeleton.GetJoint(0).Position.X, sk.skeleton.GetJoint(0).Position.Y, sk.skeleton.GetJoint(0).Position.Z),
                          new Vector3(sk.skeleton.GetJoint(31).Position.X, sk.skeleton.GetJoint(31).Position.Y, sk.skeleton.GetJoint(31).Position.Z),
                          new Vector3(sk.skeleton.GetJoint(29).Position.X, sk.skeleton.GetJoint(29).Position.Y, sk.skeleton.GetJoint(29).Position.Z))) * Quaternion.Inverse(midHead);
        
        rshoulder.rotation = Quaternion.LookRotation(new Vector3(sk.skeleton.GetJoint(5).Position.X, sk.skeleton.GetJoint(5).Position.Y, sk.skeleton.GetJoint(5).Position.Z)
                                                   - new Vector3(sk.skeleton.GetJoint(6).Position.X, sk.skeleton.GetJoint(6).Position.Y, sk.skeleton.GetJoint(6).Position.Z), forward) * Quaternion.Inverse(midLshoulder);
        relbow.rotation = Quaternion.LookRotation(new Vector3(sk.skeleton.GetJoint(6).Position.X, sk.skeleton.GetJoint(6).Position.Y, sk.skeleton.GetJoint(6).Position.Z)
                                                   - new Vector3(sk.skeleton.GetJoint(7).Position.X, sk.skeleton.GetJoint(7).Position.Y, sk.skeleton.GetJoint(7).Position.Z), forward) * Quaternion.Inverse(midLelbow);
        rhand.rotation = Quaternion.LookRotation(new Vector3(sk.skeleton.GetJoint(10).Position.X, sk.skeleton.GetJoint(10).Position.Y, sk.skeleton.GetJoint(10).Position.Z)
                                               - new Vector3(sk.skeleton.GetJoint(9).Position.X, sk.skeleton.GetJoint(9).Position.Y, sk.skeleton.GetJoint(9).Position.Z),
            TriangleNormal(new Vector3(sk.skeleton.GetJoint(7).Position.X, sk.skeleton.GetJoint(7).Position.Y, sk.skeleton.GetJoint(7).Position.Z),
                          new Vector3(sk.skeleton.GetJoint(10).Position.X, sk.skeleton.GetJoint(10).Position.Y, sk.skeleton.GetJoint(10).Position.Z),
                          new Vector3(sk.skeleton.GetJoint(9).Position.X, sk.skeleton.GetJoint(9).Position.Y, sk.skeleton.GetJoint(9).Position.Z))) * Quaternion.Inverse(midLhand);
        
        lshoulder.rotation = Quaternion.LookRotation(new Vector3(sk.skeleton.GetJoint(12).Position.X, sk.skeleton.GetJoint(12).Position.Y, sk.skeleton.GetJoint(12).Position.Z)
                                               - new Vector3(sk.skeleton.GetJoint(13).Position.X, sk.skeleton.GetJoint(13).Position.Y, sk.skeleton.GetJoint(13).Position.Z), forward) * Quaternion.Inverse(midRshoulder);
        lelbow.rotation = Quaternion.LookRotation(new Vector3(sk.skeleton.GetJoint(13).Position.X, sk.skeleton.GetJoint(13).Position.Y, sk.skeleton.GetJoint(13).Position.Z)
                                               - new Vector3(sk.skeleton.GetJoint(14).Position.X, sk.skeleton.GetJoint(14).Position.Y, sk.skeleton.GetJoint(14).Position.Z), forward) * Quaternion.Inverse(midRelbow);
        lhand.rotation = Quaternion.LookRotation(new Vector3(sk.skeleton.GetJoint(17).Position.X, sk.skeleton.GetJoint(17).Position.Y, sk.skeleton.GetJoint(17).Position.Z)
                                               - new Vector3(sk.skeleton.GetJoint(16).Position.X, sk.skeleton.GetJoint(16).Position.Y, sk.skeleton.GetJoint(16).Position.Z),
            TriangleNormal(new Vector3(sk.skeleton.GetJoint(14).Position.X, sk.skeleton.GetJoint(14).Position.Y, sk.skeleton.GetJoint(14).Position.Z),
                          new Vector3(sk.skeleton.GetJoint(17).Position.X, sk.skeleton.GetJoint(17).Position.Y, sk.skeleton.GetJoint(17).Position.Z),
                          new Vector3(sk.skeleton.GetJoint(16).Position.X, sk.skeleton.GetJoint(16).Position.Y, sk.skeleton.GetJoint(16).Position.Z))) * Quaternion.Inverse(midRhand);
        
        rhip.rotation = Quaternion.LookRotation(new Vector3(sk.skeleton.GetJoint(22).Position.X, sk.skeleton.GetJoint(22).Position.Y, sk.skeleton.GetJoint(22).Position.Z)
                                               - new Vector3(sk.skeleton.GetJoint(23).Position.X, sk.skeleton.GetJoint(23).Position.Y, sk.skeleton.GetJoint(23).Position.Z), forward) * Quaternion.Inverse(midRhip);
        rknee.rotation = Quaternion.LookRotation(new Vector3(sk.skeleton.GetJoint(23).Position.X, sk.skeleton.GetJoint(23).Position.Y, sk.skeleton.GetJoint(23).Position.Z)
                                               - new Vector3(sk.skeleton.GetJoint(24).Position.X, sk.skeleton.GetJoint(24).Position.Y, sk.skeleton.GetJoint(24).Position.Z), forward) * Quaternion.Inverse(midRknee);
        rfoot.rotation = Quaternion.LookRotation(new Vector3(sk.skeleton.GetJoint(24).Position.X, sk.skeleton.GetJoint(24).Position.Y, sk.skeleton.GetJoint(24).Position.Z)
                                               - new Vector3(sk.skeleton.GetJoint(25).Position.X, sk.skeleton.GetJoint(25).Position.Y, sk.skeleton.GetJoint(25).Position.Z),
                                                 new Vector3(sk.skeleton.GetJoint(23).Position.X, sk.skeleton.GetJoint(23).Position.Y, sk.skeleton.GetJoint(23).Position.Z)
                                               - new Vector3(sk.skeleton.GetJoint(24).Position.X, sk.skeleton.GetJoint(24).Position.Y, sk.skeleton.GetJoint(24).Position.Z)) * Quaternion.Inverse(midRfoot);

        lhip.rotation = Quaternion.LookRotation(new Vector3(sk.skeleton.GetJoint(18).Position.X, sk.skeleton.GetJoint(18).Position.Y, sk.skeleton.GetJoint(18).Position.Z)
                                               - new Vector3(sk.skeleton.GetJoint(19).Position.X, sk.skeleton.GetJoint(19).Position.Y, sk.skeleton.GetJoint(19).Position.Z), forward) * Quaternion.Inverse(midLhip);
        lknee.rotation = Quaternion.LookRotation(new Vector3(sk.skeleton.GetJoint(19).Position.X, sk.skeleton.GetJoint(19).Position.Y, sk.skeleton.GetJoint(19).Position.Z)
                                               - new Vector3(sk.skeleton.GetJoint(20).Position.X, sk.skeleton.GetJoint(20).Position.Y, sk.skeleton.GetJoint(20).Position.Z), forward) * Quaternion.Inverse(midLknee);
        lfoot.rotation = Quaternion.LookRotation(new Vector3(sk.skeleton.GetJoint(20).Position.X, sk.skeleton.GetJoint(20).Position.Y, sk.skeleton.GetJoint(20).Position.Z)
                                               - new Vector3(sk.skeleton.GetJoint(21).Position.X, sk.skeleton.GetJoint(21).Position.Y, sk.skeleton.GetJoint(21).Position.Z),
                                               new Vector3(sk.skeleton.GetJoint(19).Position.X, sk.skeleton.GetJoint(19).Position.Y, sk.skeleton.GetJoint(19).Position.Z)
                                               - new Vector3(sk.skeleton.GetJoint(20).Position.X, sk.skeleton.GetJoint(20).Position.Y, sk.skeleton.GetJoint(20).Position.Z)) * Quaternion.Inverse(midLfoot);

        final.rotation = Quaternion.Euler(0, 0, 180) * final.rotation;
    }
}
