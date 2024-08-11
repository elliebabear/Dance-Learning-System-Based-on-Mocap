using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.Azure.Kinect.Sensor;
using Microsoft.Azure.Kinect.BodyTracking;
using System.Net.Sockets;
using System;
using System.Threading;

public class KinectNetworkPointCloud : MonoBehaviour
{

    Material _renderMaterial;
    List<GameObject> _cloudGameObjs;
    Texture2D _depthTexture;
    Texture2D _colorTexture;
    Texture2D _bodyIndexTexture;
    bool _texturesInitialized;
    bool _networkInitialized;

    //ConcurrentVariables
    int _depthWidth;
    int _depthHeight;
    float[] _calibrationTable;

    const int _nOfBufferFrames = 5;
    Stack<byte[]> _colorFramesEmpty;
    Stack<byte[]> _depthFramesEmpty;
    Stack<byte[]> _bodyIndexFramesEmpty;
    Queue<byte[]> _colorFrames;
    Queue<byte[]> _depthFrames;
    Queue<byte[]> _bodyIndexFrames;

    object _framesLock;

    TcpClient _clientSocket;
    NetworkStream _clientStream;
    Thread _networkThread;
    bool _running;

    public bool remesh;
    public bool hideNonSkeletonPixels;
    public string KinectMachineIP;
    public int KinectMachinePort;

    // Start is called before the first frame update
    void Start()
    {

        _texturesInitialized = false;
        _cloudGameObjs = new List<GameObject>();
        _networkInitialized = false;
        _colorFramesEmpty = new Stack<byte[]>();
        _depthFramesEmpty = new Stack<byte[]>();
        _bodyIndexFramesEmpty = new Stack<byte[]>();
        _colorFrames = new Queue<byte[]>();
        _depthFrames = new Queue<byte[]>();
        _bodyIndexFrames = new Queue<byte[]>();
        _framesLock = new object();
        _running = true;
        _networkThread = new Thread(networkLoop);
        _networkThread.Start();
    }

    private void networkLoop()
    {
        try {
            print("Trying to connect to " + KinectMachineIP + " at port " + KinectMachinePort);
            _clientSocket = new TcpClient(KinectMachineIP, KinectMachinePort);
            print("Connected!");
            _clientStream = _clientSocket.GetStream();
        }catch(Exception e)
        {
            print("Error at connection:" + e.Message);
        }
        //read first message and configurations
        //int width, int height, 
        //int calibrationSize
        //float[calibrationSize] calibrationTable
        byte[] intBuffer = new byte[sizeof(int)];
        byte[] floatBuffer = new byte[sizeof(float)];
        _clientStream.Read(intBuffer, 0, sizeof(int));
        if (!BitConverter.IsLittleEndian)
            Array.Reverse(intBuffer);
        _depthHeight = BitConverter.ToInt32(intBuffer, 0);
        _clientStream.Read(intBuffer, 0, sizeof(int));
        if (!BitConverter.IsLittleEndian)
            Array.Reverse(intBuffer);
        _depthWidth = BitConverter.ToInt32(intBuffer, 0);
        _clientStream.Read(intBuffer, 0, sizeof(int));
        if (!BitConverter.IsLittleEndian)
            Array.Reverse(intBuffer);
        int calibrationSize = BitConverter.ToInt32(intBuffer, 0);
        _calibrationTable = new float[calibrationSize];
        for (int i = 0; i < calibrationSize; i++)
        {
            _clientStream.Read(floatBuffer, 0, sizeof(float));
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(floatBuffer);
            _calibrationTable[i] = BitConverter.ToSingle(floatBuffer, 0);
        }
        print("Read configuration info: "+ _depthHeight + " " + _depthWidth);
        _networkInitialized = true;
        //Now the other thread can start trying to render the buffers

        int colorByteSize = _depthWidth * _depthHeight * 4;
        int depthByteSize = _depthWidth * _depthHeight * 2;
        int playerIndexByteSize = _depthWidth * _depthHeight;

        for(int i = 0; i < _nOfBufferFrames; i++)
        {
            byte[] buffer = new byte[colorByteSize];
            _colorFramesEmpty.Push(buffer);
            byte[] dbuffer = new byte[depthByteSize];
            _depthFramesEmpty.Push(dbuffer);
            byte[] pbuffer = new byte[playerIndexByteSize];
            _bodyIndexFramesEmpty.Push(pbuffer);

        }
        while (_clientSocket.Connected && _running)
        {
            byte[] buffer;
            byte[] dbuffer;
            byte[] pbuffer;
            lock (_framesLock) {
                if (_colorFramesEmpty.Count == 0)
                    refillEmptyStack();
                buffer= _colorFramesEmpty.Pop();
                dbuffer = _depthFramesEmpty.Pop();
                pbuffer = _bodyIndexFramesEmpty.Pop();
            }
            try { 
                readBytesFromSocket(_clientStream, buffer, colorByteSize);
                readBytesFromSocket(_clientStream, dbuffer, depthByteSize);
                readBytesFromSocket(_clientStream, pbuffer, playerIndexByteSize);
            }
            catch (Exception e)
            {
                print("Error at reading data:" + e.Message);
                return;
            }

            lock (_framesLock)
            {
                _colorFrames.Enqueue(buffer);
                _depthFrames.Enqueue(dbuffer);
                _bodyIndexFrames.Enqueue(pbuffer);
            }

        }
        _clientSocket.Close();
    }

    void refillEmptyStack()
    {
        for(int i = 0; i < _colorFrames.Count - 1; i++)
        {
            _colorFramesEmpty.Push(_colorFrames.Dequeue());
            _depthFramesEmpty.Push(_depthFrames.Dequeue());
            _bodyIndexFramesEmpty.Push(_bodyIndexFrames.Dequeue());
        }

    }
    private void readBytesFromSocket(NetworkStream stream, byte[] buffer,int nBytes)
    {
        int bytesRead = 0;
        while(bytesRead < nBytes) {
            bytesRead += _clientStream.Read(buffer, bytesRead, nBytes - bytesRead);
        }
    }
    private void initializePointCloudData()
    {
        _renderMaterial = Resources.Load("Materials/cloudmatDepth") as Material;

        List<Vector3> points = new List<Vector3>();
        List<int> ind = new List<int>();
        int n = 0;
        int i = 0;

        for (float w = 0; w < _depthWidth; w++)
        {
            for (float h = 0; h < _depthHeight; h++)
            {
                Vector3 p = new Vector3(w / _depthWidth, h / _depthHeight, 0);
                points.Add(p);
                ind.Add(n);
                n++;

                if (n == 65000)
                {
                    GameObject a = new GameObject("cloud" + i);
                    MeshFilter mf = a.AddComponent<MeshFilter>();
                    MeshRenderer mr = a.AddComponent<MeshRenderer>();
                    mr.material = _renderMaterial;
                    mf.mesh = new Mesh();
                    mf.mesh.vertices = points.ToArray();
                    mf.mesh.SetIndices(ind.ToArray(), MeshTopology.Points, 0);
                    mf.mesh.bounds = new Bounds(new Vector3(0, 0, 4.5f), new Vector3(5, 5, 5));
                    a.transform.parent = this.gameObject.transform;
                    a.transform.localPosition = Vector3.zero;
                    a.transform.localRotation = Quaternion.identity;
                    a.transform.localScale = new Vector3(1, 1, 1);
                    n = 0;
                    i++;
                    _cloudGameObjs.Add(a);
                    points = new List<Vector3>();
                    ind = new List<int>();
                }
            }
        }
        GameObject afinal = new GameObject("cloud" + i);
        MeshFilter mfinal = afinal.AddComponent<MeshFilter>();
        MeshRenderer mrfinal = afinal.AddComponent<MeshRenderer>();
        mrfinal.material = _renderMaterial;
        mfinal.mesh = new Mesh();
        mfinal.mesh.vertices = points.ToArray();
        mfinal.mesh.SetIndices(ind.ToArray(), MeshTopology.Points, 0);
        afinal.transform.parent = this.gameObject.transform;
        afinal.transform.localPosition = Vector3.zero;
        afinal.transform.localRotation = Quaternion.identity;
        afinal.transform.localScale = new Vector3(1, 1, 1);
        n = 0;
        i++;
        _cloudGameObjs.Add(afinal);
    }

    #region mesh

    void initializeMeshData()
    {
        _renderMaterial = Resources.Load("Materials/meshmatDepth") as Material;

        List<Vector3> points = new List<Vector3>();
        List<int> ind = new List<int>();

        int h = 0;
        int submeshes;

        for (submeshes = 0; submeshes < 4; submeshes++)
        {
            h = createSubmesh(h, _depthHeight / 4, submeshes);

        }
        createStitchingMesh(_depthHeight / 4, submeshes);
    }

    int createSubmesh(int h, int submeshHeight, int id)
    {
        List<Vector3> points = new List<Vector3>();
        //  List<int> ind = new List<int>();
        List<int> tri = new List<int>();
        int n = 0;

        for (int k = 0; k < submeshHeight; k++, h++)
        {
            for (int w = 0; w < _depthWidth; w++)
            {
                Vector3 p = new Vector3(w / (float)_depthWidth, h / (float)_depthHeight, 0);
                points.Add(p);
                // ind.Add(n);

                // Skip the last row/col
                if (w != (_depthWidth - 1) && k != (submeshHeight - 1))
                {
                    int topLeft = n;
                    int topRight = topLeft + 1;
                    int bottomLeft = topLeft + _depthWidth;
                    int bottomRight = bottomLeft + 1;

                    tri.Add(topLeft);
                    tri.Add(topRight);
                    tri.Add(bottomLeft);
                    tri.Add(bottomLeft);
                    tri.Add(topRight);
                    tri.Add(bottomRight);
                }
                n++;
            }
        }

        GameObject a = new GameObject("cloud" + id);
        MeshFilter mf = a.AddComponent<MeshFilter>();
        MeshRenderer mr = a.AddComponent<MeshRenderer>();
        mr.material = _renderMaterial;
        mf.mesh = new Mesh();
        mf.mesh.vertices = points.ToArray();
        //  mf.mesh.SetIndices(ind.ToArray(), MeshTopology.Triangles, 0);
        mf.mesh.SetTriangles(tri.ToArray(), 0);
        mf.mesh.bounds = new Bounds(new Vector3(0, 0, 4.5f), new Vector3(5, 5, 5));
        a.transform.parent = this.gameObject.transform;
        a.transform.localPosition = Vector3.zero;
        a.transform.localRotation = Quaternion.identity;
        a.transform.localScale = new Vector3(1, 1, 1);
        n = 0;
        _cloudGameObjs.Add(a);

        return h;
    }

    void createStitchingMesh(int submeshHeight, int id)
    {
        List<Vector3> points = new List<Vector3>();
        //  List<int> ind = new List<int>();
        List<int> tri = new List<int>();
        int n = 0;

        for (int h = submeshHeight - 1; h < _depthHeight; h += submeshHeight)
        {
            for (int i = 0; i < 2; i++)
            {
                for (int w = 0; w < _depthWidth; w++)
                {
                    Vector3 p = new Vector3(w / (float)_depthWidth, (h + i) / (float)_depthHeight, 0);

                    points.Add(p);
                    // ind.Add(n);

                    // Skip the last row/col
                    if (w != (_depthWidth - 1) && i == 0)
                    {
                        int topLeft = n;
                        int topRight = topLeft + 1;
                        int bottomLeft = topLeft + _depthWidth;
                        int bottomRight = bottomLeft + 1;

                        tri.Add(topLeft);
                        tri.Add(topRight);
                        tri.Add(bottomLeft);
                        tri.Add(bottomLeft);
                        tri.Add(topRight);
                        tri.Add(bottomRight);
                    }
                    n++;
                }
            }
        }

        GameObject a = new GameObject("cloud" + id);
        MeshFilter mf = a.AddComponent<MeshFilter>();
        MeshRenderer mr = a.AddComponent<MeshRenderer>();
        mr.material = _renderMaterial;
        mf.mesh = new Mesh();
        mf.mesh.vertices = points.ToArray();
        //  mf.mesh.SetIndices(ind.ToArray(), MeshTopology.Triangles, 0);
        mf.mesh.SetTriangles(tri.ToArray(), 0);
        mf.mesh.bounds = new Bounds(new Vector3(0, 0, 4.5f), new Vector3(5, 5, 5));
        a.transform.parent = this.gameObject.transform;
        a.transform.localPosition = Vector3.zero;
        a.transform.localRotation = Quaternion.identity;
        a.transform.localScale = new Vector3(1, 1, 1);
        n = 0;
        _cloudGameObjs.Add(a);
    }
    #endregion

    public void hide()
    {
        foreach (GameObject a in _cloudGameObjs)
            a.SetActive(false);
    }

    public void show()
    {
        foreach (GameObject a in _cloudGameObjs)
            a.SetActive(true);
    }
    void PostKinectInit()
    {
        _depthTexture = new Texture2D(_depthWidth, _depthHeight, TextureFormat.RG16, false);
        _colorTexture = new Texture2D(_depthWidth, _depthHeight, TextureFormat.BGRA32, false);
        _bodyIndexTexture = new Texture2D(_depthWidth, _depthHeight, TextureFormat.Alpha8, false);
        _depthTexture.filterMode = FilterMode.Point;
        _colorTexture.filterMode = FilterMode.Point;
        if (remesh)
        {
            initializeMeshData();
        }
        else
        {
            initializePointCloudData();

        }
        _renderMaterial.SetFloatArray("camera_calibration", _calibrationTable);
        _renderMaterial.SetFloat("camera_width", _depthWidth);
        _renderMaterial.SetFloat("camera_height", _depthHeight);
        _texturesInitialized = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (!_networkInitialized) return;
   
        if (_networkInitialized && !_texturesInitialized)
        {
            PostKinectInit();
        }
        

        lock (_framesLock)
        {
            refillEmptyStack();
            if (_colorFrames.Count > 0) {

                byte[] buffer = _colorFrames.Dequeue();
                byte[] dbuffer = _depthFrames.Dequeue();
                byte[] pbuffer = _bodyIndexFrames.Dequeue();
                _colorTexture.LoadRawTextureData(buffer);
                _depthTexture.LoadRawTextureData(dbuffer);
                _bodyIndexTexture.LoadRawTextureData(pbuffer);
                _colorFramesEmpty.Push(buffer);
                _depthFramesEmpty.Push(dbuffer);
                _bodyIndexFramesEmpty.Push(pbuffer);
            }
        }

        _colorTexture.Apply();
        _depthTexture.Apply();
        _bodyIndexTexture.Apply();
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer mr = renderers[i];
            mr.material.SetInt("_RemoveBackground", hideNonSkeletonPixels ? 1 : 0);
            mr.material.SetTexture("_ColorTex", _colorTexture);
            mr.material.SetTexture("_DepthTex", _depthTexture);
            mr.material.SetTexture("_BodyIndexTex", _bodyIndexTexture);

        }
    }

    private void OnApplicationQuit()
    {
        _running = false;
        _networkThread.Join();
    }
}
