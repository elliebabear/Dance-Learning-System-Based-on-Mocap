using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Microsoft.Azure.Kinect.Sensor;
using Microsoft.Azure.Kinect.BodyTracking;

public class SkeletonGameObject {
    public GameObject root;
    public GameObject[] children;
    public float lastUpdateTime;
    public Vector3 lastPosition;
}

public class KinectSkeleton : MonoBehaviour {
    KinectController kinectController;

    List<SkeletonGameObject> skeletons = new List<SkeletonGameObject> ();
    // List<GameObject[]> skeletons = new List<GameObject[]> ();
    // Dictionary<GameObject, float> lastSkeletonActiveTimes;
    const float timeSkeletonCanBeDeactiveBeforeDelete = 3;
    const float maxDistanceToAssumeSamePersonReentry = 2;

    void Awake () {
        kinectController = FindObjectOfType<KinectController> ();
        if (kinectController == null) {
            print ("requires a kinect controller");
            return;
        }
    }

    /*
        Update checks for any skeletons?
        Found a skeleton! Checks if we have a real skeleton whos name is bodyID?
            Yes: Update that skeletons joints
            No: Check if we have any recently deactivated skeletons that are close in position to this new one (maybe its the same?)
    */

    void Update () {
        if (kinectController == null) return;

        List<SkeletonGameObject> toDelete = new List<SkeletonGameObject> ();
        // Queue the expired skeletons for deletion - user probably walked away
        foreach (SkeletonGameObject skeleton in skeletons) {
            if (Time.time > skeleton.lastUpdateTime + timeSkeletonCanBeDeactiveBeforeDelete) {
                print ("deleting skeleton " + skeleton.root.name);
                toDelete.Add (skeleton);
            }
        }

        // Do the actual delete
        foreach (SkeletonGameObject skeleton in toDelete) {
            GameObject.Destroy (skeleton.root);
            skeletons.Remove (skeleton);
        }
        toDelete.Clear ();

        lock (kinectController.m_bufferLock) { 
            List<SkeletonInfo> skeles = kinectController.m_currentSkeletons;
            foreach(SkeletonInfo sk in skeles) {
          
                uint bodyId = sk.id;

                // Check if skeleton already exists with same ID
                SkeletonGameObject existingSkeleton = skeletons.FirstOrDefault (skeleton => skeleton.root?.name == bodyId.ToString ());

                // Recognises this skeleton
                if (existingSkeleton != null) {
                    ApplyJointDataToSkeleton (sk.skeleton, existingSkeleton);
                } else { // Unidentified skeleton
                    // Is there a recently disappeared one that was close to this new one? ie the same person?
                    foreach (SkeletonGameObject skeleton in skeletons) {
                        if (Time.time > skeleton.lastUpdateTime + .5f) {
                            if (Vector3.Distance (skeleton.lastPosition,
                                    new Vector3 (sk.skeleton.GetJoint(0).Position.X,
                                        sk.skeleton.GetJoint(0).Position.Y,
                                        sk.skeleton.GetJoint(0).Position.Z)) < maxDistanceToAssumeSamePersonReentry) {
                                skeleton.root.name = bodyId.ToString ();
                                return;
                            }
                        }
                    }
                    // Else it must really be a new person
                    CreateDebugSkeletons (bodyId.ToString ());
                }
            }
        }
    }

    void ApplyJointDataToSkeleton (Skeleton skeletonData, SkeletonGameObject realSkeleton) {
        // Do joint moves
        for (var i = 0; i <(int)JointId.Count; i++) {
            var joint = skeletonData.GetJoint(i);
            var pos = joint.Position;
            var rot = joint.Quaternion;
            var v = new Vector3 (pos.X, -pos.Y, pos.Z) * 0.001f;
            var r = new Quaternion (rot.X, rot.Y, rot.Z, rot.W);
            realSkeleton.children[i].transform.localPosition = v;
            realSkeleton.children[i].transform.localRotation = r;
        }
        realSkeleton.lastUpdateTime = Time.time;
        realSkeleton.lastPosition = new Vector3 (skeletonData.GetJoint(0).Position.X, skeletonData.GetJoint(0).Position.Y, skeletonData.GetJoint(0).Position.Z);
    }

    void CreateDebugSkeletons (string rootName) {
        SkeletonGameObject newSkeleton = new SkeletonGameObject ();
        GameObject[] joints = new GameObject[(int)JointId.Count];
        GameObject skeletonRoot = new GameObject (rootName);
        for (int joint = 0; joint < (int)JointId.Count; joint++) {
            var cube = GameObject.CreatePrimitive (PrimitiveType.Cube);
            cube.name = System.Enum.ToObject(typeof(JointId), joint).ToString();
            cube.transform.localPosition = Vector3.zero;
            cube.transform.localScale = Vector3.one * 0.05f;
            cube.transform.parent = skeletonRoot.transform;
            joints[joint] = cube;
        }
        newSkeleton.root = skeletonRoot;
        newSkeleton.children = joints;
        newSkeleton.lastUpdateTime = Time.time;

        skeletons.Add (newSkeleton);
        print ("created a skeleton " + skeletonRoot.name);
    }
}