using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.Azure.Kinect.Sensor;
using Microsoft.Azure.Kinect.BodyTracking;

public class KinectPointCloud : MonoBehaviour
{

    Material _renderMaterial;
    KinectController _kinectController;
    List<GameObject> _cloudGameObjs;
    Texture2D _depthTexture;
    Texture2D _colorTexture;
    Texture2D _bodyIndexTexture;
    bool _texturesInitialized;

    public bool remesh;
    public bool hideNonSkeletonPixels;

    // Start is called before the first frame update
    void Start()
    {
        _kinectController = FindObjectOfType<KinectController>();
        if (_kinectController == null)
        {
            print("requires a kinect controller");
            return;
        }

        _texturesInitialized = false;
        _cloudGameObjs = new List<GameObject>();
    }

    private void initializePointCloudData()
    {
        _renderMaterial = Resources.Load("Materials/cloudmatDepth") as Material;

        List<Vector3> points = new List<Vector3>();
        List<int> ind = new List<int>();
        int n = 0;
        int i = 0;

        for (float w = 0; w < _kinectController.depthWidth; w++)
        {
            for (float h = 0; h < _kinectController.depthHeight; h++)
            {
                Vector3 p = new Vector3(w / _kinectController.depthWidth, h / _kinectController.depthHeight, 0);
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
            h = createSubmesh(h, _kinectController.depthHeight / 4, submeshes);

        }
        createStitchingMesh(_kinectController.depthHeight / 4, submeshes);
    }

    int createSubmesh(int h, int submeshHeight, int id)
    {
        List<Vector3> points = new List<Vector3>();
        //  List<int> ind = new List<int>();
        List<int> tri = new List<int>();
        int n = 0;

        for (int k = 0; k < submeshHeight; k++, h++)
        {
            for (int w = 0; w < _kinectController.depthWidth; w++)
            {
                Vector3 p = new Vector3(w / (float)_kinectController.depthWidth, h / (float)_kinectController.depthHeight, 0);
                points.Add(p);
                // ind.Add(n);

                // Skip the last row/col
                if (w != (_kinectController.depthWidth - 1) && k != (submeshHeight - 1))
                {
                    int topLeft = n;
                    int topRight = topLeft + 1;
                    int bottomLeft = topLeft + _kinectController.depthWidth;
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

        for (int h = submeshHeight - 1; h < _kinectController.depthHeight; h += submeshHeight)
        {
            for (int i = 0; i < 2; i++)
            {
                for (int w = 0; w < _kinectController.depthWidth; w++)
                {
                    Vector3 p = new Vector3(w / (float)_kinectController.depthWidth, (h + i) / (float)_kinectController.depthHeight, 0);

                    points.Add(p);
                    // ind.Add(n);

                    // Skip the last row/col
                    if (w != (_kinectController.depthWidth - 1) && i == 0)
                    {
                        int topLeft = n;
                        int topRight = topLeft + 1;
                        int bottomLeft = topLeft + _kinectController.depthWidth;
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
        _depthTexture = new Texture2D(_kinectController.depthWidth, _kinectController.depthHeight, TextureFormat.RG16, false);
        _colorTexture = new Texture2D(_kinectController.depthWidth, _kinectController.depthHeight, TextureFormat.BGRA32, false);
        _bodyIndexTexture = new Texture2D(_kinectController.depthWidth, _kinectController.depthHeight, TextureFormat.Alpha8, false);
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
        _renderMaterial.SetFloatArray("camera_calibration", _kinectController.calibrationTable);
        _renderMaterial.SetFloat("camera_width", _kinectController.depthWidth);
        _renderMaterial.SetFloat("camera_height", _kinectController.depthHeight);
        _texturesInitialized = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (!_kinectController.kinectInitialized)
        {
            print("Kinect not Initialized");
            if (Input.GetKeyUp(KeyCode.R))
            {
                Start();
            }
            else
            {
                return;
            }
        }

        if (_kinectController.kinectInitialized && !_texturesInitialized)
        {
            PostKinectInit();
        }

        lock (_kinectController.m_bufferLock)
        {
            _colorTexture.LoadRawTextureData(_kinectController.m_colorImage);
            _depthTexture.LoadRawTextureData(_kinectController.m_depthImage);
            _bodyIndexTexture.LoadRawTextureData(_kinectController.m_bodyIndexMap);
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
}
