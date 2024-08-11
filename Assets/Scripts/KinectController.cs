using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using Microsoft.Azure.Kinect.Sensor;
using Microsoft.Azure.Kinect.BodyTracking;
    
using System.Threading;

public struct SkeletonInfo
{
    public Skeleton skeleton;
    public uint id;

    public SkeletonInfo(Skeleton s, uint i)
    {
        skeleton = s;
        id = i;
    }
}
public class KinectController : MonoBehaviour
{

    //MULTITHREAD VARIABLES
    public byte[] m_depthImage;
    public byte[] m_colorImage;
    public byte[] m_bodyIndexMap;
    public List<SkeletonInfo> m_currentSkeletons;
    public object m_bufferLock;
    Thread _kinectThread;


    bool _running;
    
    
    public bool kinectInitialized;
    public int depthWidth;
    public int depthHeight;
    public float[] calibrationTable;




    //Set through custom inspector

    public string depthMode;


    void kinectTask()
    {
        using (Device device = Device.Open(0))
        {
            DepthMode d = DepthMode.WFOV_2x2Binned;
            print(depthMode);
            switch (depthMode)
            {
                case "WFOV_2x2Binned":
                    d = DepthMode.WFOV_2x2Binned;
                    break;
                case "NFOV_2x2Binned":
                    d = DepthMode.NFOV_2x2Binned;
                    break;
                case "WFOV_Unbinned":
                    d = DepthMode.WFOV_Unbinned;
                    break;
                case "NFOV_Unbinned":
                    d = DepthMode.NFOV_Unbinned;
                    break;
            }
            device.StartCameras(new DeviceConfiguration
            {
                ColorFormat = ImageFormat.ColorBGRA32,
                ColorResolution = ColorResolution.R720p,
                DepthMode = d,
                SynchronizedImagesOnly = true,
                CameraFPS = FPS.FPS30,
            }); ;

            depthWidth = device.GetCalibration().DepthCameraCalibration.ResolutionWidth;
            depthHeight = device.GetCalibration().DepthCameraCalibration.ResolutionHeight;

            print(depthHeight + " "+ depthWidth);
            //------------- calibration table
           Calibration c=  device.GetCalibration();
           calibrationTable = c.DepthCameraCalibration.Intrinsics.Parameters;

            //------------- end calibration table

            m_depthImage = new byte[depthWidth * depthHeight * 4];
            m_colorImage = new byte[depthWidth * depthHeight * 4];
            m_bodyIndexMap = new byte[depthWidth * depthHeight];
            using (Image transformedColor = new Image(ImageFormat.ColorBGRA32, depthWidth, depthHeight))
            using (Transformation transform = device.GetCalibration().CreateTransformation())
            {
                kinectInitialized = true;
                try
                {
                    using (Tracker tracker = Tracker.Create(c, new TrackerConfiguration() { ProcessingMode = TrackerProcessingMode.Cuda, SensorOrientation = SensorOrientation.Default }))
                    {
                        while (_running)
                        {
                            using (Capture sensorCapture = device.GetCapture())
                            {
                                tracker.EnqueueCapture(sensorCapture);
                            }
                            using (Frame frame = tracker.PopResult(TimeSpan.Zero, throwOnTimeout: false))
                            {
                                if (frame == null) continue;
                                transform.ColorImageToDepthCamera(frame.Capture, transformedColor);
                                lock (m_bufferLock)
                                {
                                    m_currentSkeletons.Clear();
                                    for (int body = 0; body < frame.NumberOfBodies; body++)
                                      m_currentSkeletons.Add(new SkeletonInfo(frame.GetBodySkeleton((uint)body),frame.GetBodyId((uint)body)));
                                    m_colorImage = transformedColor.Memory.ToArray();
                                    m_depthImage = frame.Capture.Depth.Memory.ToArray();
                                    m_bodyIndexMap = frame.BodyIndexMap.Memory.ToArray();
                                }
                            }
                        }
                    }
                }
                catch (TimeoutException e)
                {
                    print(e.Message);
                    kinectInitialized = false;
                    return;
                }
            }
        }
    }

    void Start()
    {
        kinectInitialized = false;
        _running = true;
        m_currentSkeletons = new List<SkeletonInfo>();
        m_bufferLock = new object();
        _kinectThread = new Thread(kinectTask);
        _kinectThread.Start();
    }

  
        // Update is called once per frame
    void Update()
    {
        if (Device.GetInstalledCount() == 0)
        {
            print("No Kinect installed");
            return;
        }
    }


    private void OnApplicationQuit()
    {
        _running = false;
        _kinectThread.Join();
    }
}
