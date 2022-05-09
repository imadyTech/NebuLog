using UnityEngine;
using System;
using imady.Message;
using imady.Event;
using NebulogUnityServer.View;
using NebulogUnityServer.Objects;
using NebulogUnityServer.Services;
using NebulogUnityServer.DataModels;
using UnityEngine.UI;

namespace NebulogUnityServer.Manager
{
    /// <summary>
    /// 负责管理场景中的被操作对象（MadYObjects）的生成、销毁、入场、离场
    /// </summary>
    public class NebuTheatreManager : MadYEventObjectBase,
        IMadYProvider<MadYUnityUIMessage<PlayMsg>>, IMadYProvider<MadYUnityUIMessage<PauseMsg>>,
        IMadYObserver<MadYUnityUIMessage<MadYUnityButtonInput>>,
        IMadYObserver<MadYUnityUIMessage<SimulationStartMsg>>, IMadYObserver<MadYUnityUIMessage<SimulationEndMsg>>
    {
        private MadYObjectPool objectPool;

        protected override void Awake()
        {
            base.Awake();
        }

        /// <summary>
        /// 根据预定方案实例化场景钟最初的imadyObjects
        /// </summary>
        /// <returns></returns>
        public NebuTheatreManager Initialize(MadYEventManager eventmanager)
        {
            Initialize();//必须首先将需要加入EventSystem的对象都生成出来

            #region 场景中的对象在初始化时期加入EventSystem
            //m_earth2d.AddEventManager(eventmanager);
            App.Instance.uiManager.mainView.AddSystemLog("地球对象加载完成！", this.name);

            #endregion

            return this;
        }
        private NebuTheatreManager Initialize()
        {
            if (objectPool == null)
            {
                Debug.LogException(new Exception("MadYObjectPool视窗池子还没完成初始化。"));
                return this;
            }

            //2D 高精度地球
            //if (App.Instance.Earth2DObject != null)
            //    m_earth2d = ((Earth2D)App.Instance.Earth2DObject
            //        .GetComponent<Earth2D>()
            //        .SetManager(this))
            //        .AddCityMap();
            App.Instance.uiManager.mainView.AddSystemLog("地球对象初始化完成！", this.name);
            return this;
        }

        /// <summary>
        /// 初始化一个MadYObjectPool(MadYTheatreManager在App中刚刚被加载时还找不到AppInstance实例，所以要置后调用此方法)
        /// </summary>
        /// <param name="parent"></param>
        /// <returns></returns>
        public NebuTheatreManager AddPool(Transform parent)
        {
            objectPool = this.gameObject.AddComponent<MadYObjectPool>();
            return this;
        }

        #region IN-SCENE UNITY OBJECTS MANAGEMENT
        public IMadYObject WakeObject(Type objectType, Transform parent)
        {
            var MadYObj = objectPool
                .WakeObject(objectType, parent);
            if (MadYObj == null)
                Debug.LogWarning($"[TheatreManager WakeObject Error] {objectType.Name} GAMEOBJECT is not instantiated.");

            var iMadYobjComponent = MadYObj.GetComponent<IMadYObject>();
            if (iMadYobjComponent != null)
            {
                iMadYobjComponent.SetParent(parent);
                return iMadYobjComponent;
            }
            else
            {
                Debug.LogWarning($"[WakeObject Error] {objectType.Name} IMadYObject script is not attached.");
                return null;
            }
        }

        /// <summary>
        /// 用于激活、休眠非UI类型的视图对象
        /// </summary>
        /// <param name="imadyObject"></param>
        protected void HibernateObject(MadYEventObjectBase imadyObject)
        {
            objectPool.HibernateObject(imadyObject);
        }
        #endregion


        #region EVENTSYSTEM INTERFACE IMPLEMENTATIONS
        public void OnNext(MadYUnityUIMessage<MadYUnityButtonInput> message)
        {
            switch (message.messageBody.msg)
            {
                case "Data_Play":
                    {
                        base.NotifyObservers(new MadYUnityUIMessage<PlayMsg>() { });
                        break;
                    }
                case "Data_Pause":
                    {
                        base.NotifyObservers(new MadYUnityUIMessage<PauseMsg>() { });
                        break;
                    }
            }
        }

        /// <summary>
        /// UI界面按下“start”按钮 -> 启动模拟
        /// </summary>
        /// <param name="message"></param>
        public void OnNext(MadYUnityUIMessage<SimulationStartMsg> message)
        {

        }
        /// <summary>
        /// UI界面按下“end”按钮 -> 停止模拟
        /// </summary>
        /// <param name="message"></param>

        public void OnNext(MadYUnityUIMessage<SimulationEndMsg> message)
        {
        }
        #endregion

    }
}