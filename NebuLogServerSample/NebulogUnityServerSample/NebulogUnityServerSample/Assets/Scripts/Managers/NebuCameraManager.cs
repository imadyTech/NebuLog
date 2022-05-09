using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using imady.Message;
using NebulogUnityServer.Common;
using imady.Event;
using NebulogUnityServer.Objects;

namespace NebulogUnityServer.Manager
{
    /// <summary>
    /// 负责摄像机的调整动作
    /// </summary>
    public class NebuCameraManager : MadYEventObjectBase,
        IMadYProvider<CameraScrollMsg>,//相机缩放倍数消息，通知物体对象反向缩放
        IMadYObserver<MadYUnityUIMessage<MadYUnityButtonInput>>,
        IMadYObserver<MadYUnityUIMessage<Nebu2DViewMsg>>, IMadYObserver<MadYUnityUIMessage<Nebu3DViewMsg>>,
        IMadYObserver<MadYUnityUIMessage<MouseDragMsg>>
    {
        public Camera mainCamera;
        public float testorPos;
        //public Camera sateSideCamera;//卫星追踪模式相机//这个要挂在SatelliteObject上
        //public Camera sateRearCamera;//卫星侧面模式相机//这个要挂在SatelliteObject上

        [Header("指示场景的3D/2D模式状态")]
        public string m_earthDisplayMode = "2D";
        
        [Header("默认的主相机视角FOV")]
        public int m_DefaultMainCameraFOV = 30;
        [Header("当前主相机3D视角FOV")]
        public float m_currentCameraFOV3D = 25;
        [Header("当前主相机2D视角FOV")]
        public float m_currentCameraFOV2D = 30;
        public float m_MouseDragFactor => NebulogAppConfiguration.defaultMouseDragRatio * mainCamera.orthographicSize / m_DefaultMainCameraFOV;
        public float m_MouseScrollFactor => NebulogAppConfiguration.defualtMouseScrollRatio;

        protected override void Awake()
        {
            base.Awake();

            mainCamera = Camera.main;
            mainCamera.fieldOfView = m_DefaultMainCameraFOV;
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = m_DefaultMainCameraFOV;
        }


        #region EVENTSYSTEM INTERFACE IMPLEMENTATIONS
        public void OnNext(MadYUnityUIMessage<MadYUnityButtonInput> message) { }
        public void OnNext(MadYUnityUIMessage<Nebu2DViewMsg> message)
        {
            m_earthDisplayMode = "2D";
            SetCamera2D();
        }
        public void OnNext(MadYUnityUIMessage<Nebu3DViewMsg> message)
        {
            m_earthDisplayMode = "3D";
            SetCamera3D();
        }


        private CameraScrollMsg scrollMsg = new CameraScrollMsg { CameraScale = 1 };
        public void OnNext(MadYUnityUIMessage<MouseDragMsg> message)
        {
            //Debug.Log($"[MouseDragMsg]: x:{message.messageBody.xDragRatio}, y:{message.messageBody.yDragRation}, scroll:{message.messageBody.scrollRatio}");
            if (m_earthDisplayMode == "3D" && ((message.messageBody.xDragDeltaRatio + message.messageBody.yDragDeltaRatio) != 0 || message.messageBody.scrollRatio != 0))
            {

                //处理Drag：相机绕着地球跑
                RotateCameraAroundCenter(message.messageBody.xDragDeltaRatio, message.messageBody.yDragDeltaRatio);
                //处理Scroll
                m_currentCameraFOV3D = Mathf.Clamp(
                    mainCamera.fieldOfView * (1 - (message.messageBody.scrollRatio * NebulogAppConfiguration.defualtMouseScrollRatio)),
                    2, 80);
                SetCamera3D();
                //通知需要反向缩放的物体
                scrollMsg.CameraScale = (float)Math.Sqrt(m_currentCameraFOV3D / m_DefaultMainCameraFOV);
                base.NotifyObservers(scrollMsg);
            }
            if (m_earthDisplayMode == "2D" && ((message.messageBody.xDragDeltaRatio + message.messageBody.yDragDeltaRatio) != 0 || message.messageBody.scrollRatio != 0))
            {
                //处理Drag：由2D地图自己完成拖拽
                //earth2D.Drag(message.messageBody.xDragDeltaRatio, message.messageBody.yDragDeltaRatio, m_MouseDragFactor);
                //处理Scroll
                m_currentCameraFOV2D = Mathf.Clamp(
                    mainCamera.orthographicSize * (1 - (message.messageBody.scrollRatio * NebulogAppConfiguration.defualtMouseScrollRatio)),
                    2, 40);
                SetCamera2D();

                //通知需要反向缩放的物体
                scrollMsg.CameraScale = (float)Math.Sqrt(m_currentCameraFOV2D / m_DefaultMainCameraFOV);//取平方根：FOV是二维度，而gameobject size参数是一维
                base.NotifyObservers(scrollMsg);
            }
        }

        private void RotateCameraAroundCenter(float xDragRatio, float yDragRatio)
        {
            mainCamera.transform.RotateAround(Vector3.zero, Vector3.up, xDragRatio * m_MouseDragFactor);
            mainCamera.transform.RotateAround(Vector3.zero, Vector3.forward, yDragRatio * m_MouseDragFactor);
            mainCamera.transform.LookAt(Vector3.zero);
        }

        private void SetCamera2D()
        {
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = m_currentCameraFOV2D;
        }
        private void SetCamera3D()
        {
            mainCamera.orthographic = false;
            mainCamera.fieldOfView = m_currentCameraFOV3D;
        }
        #endregion


    }
}