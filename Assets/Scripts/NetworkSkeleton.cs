using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Microsoft.Azure.Kinect.Sensor;
using Microsoft.Azure.Kinect.BodyTracking;
using System.Net.Sockets;
using System.Threading;
using System;


public struct NetworkJoint
{
    public Vector3 position;
    public Quaternion rotation;
}
public struct NetworkSkeletonInfo
{
    public NetworkJoint[] joints;
    public int id;

    public NetworkSkeletonInfo(int count,int i)
    {
        joints = new NetworkJoint[count];
        id = i;
    }
}
public class NetworkSkeleton : MonoBehaviour {

    List<SkeletonGameObject> skeletons = new List<SkeletonGameObject> ();
    // List<GameObject[]> skeletons = new List<GameObject[]> ();
    // Dictionary<GameObject, float> lastSkeletonActiveTimes;
    const float timeSkeletonCanBeDeactiveBeforeDelete = 3;
    const float maxDistanceToAssumeSamePersonReentry = 2;

    public string KinectMachineIP;
    public int KinectMachinePort;

    string[] _jointNames;
    object _skeleslock;

    TcpClient _clientSocket;
    NetworkStream _clientStream;
    Thread _networkThread;
    bool _running;
    List<NetworkSkeletonInfo> m_currentSkeletons;

    void Awake () 
    {
        _running = true;
        _skeleslock = new object();
        m_currentSkeletons = new List<NetworkSkeletonInfo>();
        _networkThread = new Thread(networkLoop);
        _networkThread.Start();
        
    }

    /*
        Update checks for any skeletons?
        Found a skeleton! Checks if we have a real skeleton whos name is bodyID?
            Yes: Update that skeletons joints
            No: Check if we have any recently deactivated skeletons that are close in position to this new one (maybe its the same?)
    */

     private NetworkSkeletonInfo readNetworkSkeleton(int id)
    {
        byte[] floatBuffer = new byte[sizeof(float)];
        NetworkSkeletonInfo res = new NetworkSkeletonInfo(_jointNames.Length, id);
        for(int i = 0; i < _jointNames.Length; i++)
        {
            NetworkJoint j = new NetworkJoint();
            _clientStream.Read(floatBuffer, 0, sizeof(float));
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(floatBuffer);
            j.position.x = BitConverter.ToSingle(floatBuffer, 0);

            _clientStream.Read(floatBuffer, 0, sizeof(float));
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(floatBuffer);
            j.position.y = BitConverter.ToSingle(floatBuffer, 0);

            _clientStream.Read(floatBuffer, 0, sizeof(float));
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(floatBuffer);
            j.position.z = BitConverter.ToSingle(floatBuffer, 0);

            _clientStream.Read(floatBuffer, 0, sizeof(float));
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(floatBuffer);
            j.rotation.x = BitConverter.ToSingle(floatBuffer, 0);

            _clientStream.Read(floatBuffer, 0, sizeof(float));
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(floatBuffer);
            j.rotation.y = BitConverter.ToSingle(floatBuffer, 0);

            _clientStream.Read(floatBuffer, 0, sizeof(float));
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(floatBuffer);
            j.rotation.z = BitConverter.ToSingle(floatBuffer, 0);

            _clientStream.Read(floatBuffer, 0, sizeof(float));
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(floatBuffer);
            j.rotation.w = BitConverter.ToSingle(floatBuffer, 0);
            res.joints[i] = j;
        }
        return res;

    }
    private void networkLoop()
    {
        try
        {
            print("Trying to connect to " + KinectMachineIP + " at port " + KinectMachinePort);
            _clientSocket = new TcpClient(KinectMachineIP, KinectMachinePort);
            print("Connected!");
            _clientStream = _clientSocket.GetStream();
        }
        catch (Exception e)
        {
            print("Error at connection:" + e.Message);
        }

        byte[] intBuffer = new byte[sizeof(int)];

        //read first message

        //Joint names lenght
        _clientStream.Read(intBuffer, 0, 4);
        if (!BitConverter.IsLittleEndian)
            Array.Reverse(intBuffer);
        int messageSize = BitConverter.ToInt32(intBuffer, 0);
        byte[] stringbuffer = new byte[messageSize];
        _clientStream.Read(stringbuffer, 0, messageSize);

        string s = System.Text.Encoding.UTF8.GetString(stringbuffer, 0, stringbuffer.Length);

        _jointNames= s.Split(',');

        List<NetworkSkeletonInfo> networkSkeletons = new List<NetworkSkeletonInfo>();
        //Setup Buffers
        while (_clientSocket.Connected && _running)
        {
            networkSkeletons.Clear();
            //read info
            //how many skeletons?
            _clientStream.Read(intBuffer, 0, sizeof(int));
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(intBuffer);
            int nsk = BitConverter.ToInt32(intBuffer, 0);
            int id = 0;
            while(id < nsk)
            {
                NetworkSkeletonInfo ski = readNetworkSkeleton(id);
                networkSkeletons.Add(ski);
                id++;
            }

            lock (_skeleslock) 
            {
                m_currentSkeletons.Clear();
                m_currentSkeletons.AddRange(networkSkeletons);
            }

        }
        _clientSocket.Close();
    }





    void Update () {

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

        lock (_skeleslock) { 
            List<NetworkSkeletonInfo> skeles = m_currentSkeletons;
            foreach(NetworkSkeletonInfo sk in skeles) {
          
                int bodyId = sk.id;

                // Check if skeleton already exists with same ID
                SkeletonGameObject existingSkeleton = skeletons.FirstOrDefault (skeleton => skeleton.root?.name == bodyId.ToString ());

                // Recognises this skeleton
                if (existingSkeleton != null) {
                    ApplyJointDataToNetworkSkeleton (sk.joints, existingSkeleton);
                } else { // Unidentified skeleton
                    // Is there a recently disappeared one that was close to this new one? ie the same person?
                    foreach (SkeletonGameObject skeleton in skeletons) {
                        if (Time.time > skeleton.lastUpdateTime + .5f) {
                            if (Vector3.Distance (skeleton.lastPosition,
                                    sk.joints[0].position) < maxDistanceToAssumeSamePersonReentry) {
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

    void ApplyJointDataToNetworkSkeleton (NetworkJoint[] joints, SkeletonGameObject realSkeleton) {
        // Do joint moves
        for (int i = 0; i <(int)JointId.Count; i++) {
            Vector3 pos = joints[i].position;
            pos.y *= -1;
            pos *= 0.001f;
            realSkeleton.children[i].transform.localPosition = pos;
            realSkeleton.children[i].transform.localRotation = joints[i].rotation;
        }
        realSkeleton.lastUpdateTime = Time.time;
        realSkeleton.lastPosition = joints[0].position;
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

    private void OnApplicationQuit()
    {
        _running = false;
        _networkThread.Join();
    }
}