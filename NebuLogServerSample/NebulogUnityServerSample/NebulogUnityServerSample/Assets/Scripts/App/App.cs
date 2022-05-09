#region Copyrights imady
/*
 *Copyright(C) 2020 by imady Technology (Suzhou); All rights reserved.
 *Author:       Frank Shen
 *Date:         2020-07-31
 *Description:   
 */
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
//using Microsoft.AspNetCore.SignalR.Client;
using System.Threading.Tasks;
using imady.NebuLog;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using Newtonsoft.Json;
using NebulogUnityServer.Common;
using imady.Message;
using NebulogUnityServer.Manager;
using NebulogUnityServer.DataModels;
using imady.Event;
using NebulogUnityServer.Services;
using NebulogUnityServer.Objects;
using System.Data;
using System.Text;
using Microsoft.AspNetCore;
using System.IO;
using Microsoft.Extensions.DependencyInjection;

namespace NebulogUnityServer
{
    public class App : NebuSingleton<App>
    {
        #region GameObjects & Managers对象定义
        public static IUnityNebulog logger;
        //public static NebuMessengger messenger;

        //Nebu辅助开发工具，用于监控运行时进程
        public GameObject NebuEventMgrGO;
        public GameObject NebuSceneMgrGO;
        public GameObject NebuUiMgrGO;
        public GameObject MainCanvasObject;
        public GameObject NebuCameraMgrGO;

        public NebuUIManager uiManager;
        public NebuTheatreManager theatreManager;
        public MadYEventManager eventManager;
        public NebulogManager nebulogManager;
        #endregion

        #region MonoBehaviour Methods
        protected override void Awake()
        {
            base.Awake();

            //========================================
            //App对象及App.cs脚本必须自始至终都在场景中存在。
            //DontDestroyOnLoad(this);
            //========================================


            InitializeAppConfiguration();
            InitializeManagers(null, null);
            InitNebulog();
        }

        void Update()
        {
            // exit
            if (Input.GetKeyDown(KeyCode.Home) || Input.GetKeyDown(KeyCode.Escape))
            {
                Debug.Log("[NebulogAPP应用程序]: 应用程序已经退出。");
                Application.Quit();
            }

        }

        void OnApplicationQuit()
        {
            //停止 TODO: remove all listeners registered
        }
        #endregion

        private void InitializeAppConfiguration()
        {
        }

        private void InitNebulog()
        {
            logger = new UnityNebulogger();

            //注册到HubConnrvyion连接完成事件，进行业务模块加载
            logger.NebulogConnected += (sender, args) =>
            {
                //==================================================================
                //等待UnityNebuLogger初始化完成（HubConnection连接后）才加载业务逻辑
                //否则可能引起HubConnection未连接就被调用，而导致进程锁死
                NebuEventMgrGO.AddComponent<NebulogManager>();
                ////==================================================================
                //hubStatusText.text += "\nSignalR HubConnection连接完成。";
                Debug.Log("App initiation completed.");
            };

            ////================================ Server console 演示 =============================
            //NebuLogHub.OnILoggingMessageReceived += nebulogManager.OnLoggingMessageReceived;
            //NebuLogHub.OnAddStatRequestReceived += nebulogManager.OnAddStatRequestReceived;
            //NebuLogHub.OnRefreshStatRequestReceived += nebulogManager.OnRefreshStatRequestRecieved;
            ////================================ Server console 演示 =============================
        }


        /// <summary>
        /// Add the manager objects and components after the logger is ready
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void InitializeManagers(object sender, EventArgs e)
        {
            //初始化所有Manager。。。
            //eventManager必须在最先加载
            eventManager = NebuEventMgrGO
                .AddComponent<MadYEventManager>();

            /// 剧场管理器，负责管理被操作对象（NebuInteractable）的生成、销毁、入场、离场
            if (NebuSceneMgrGO != null) theatreManager = ((NebuTheatreManager)NebuSceneMgrGO
                .AddComponent<NebuTheatreManager>()
                .AddEventManager(this.eventManager))//这是NebuTheatreManager自己注册到eventsystem
                .AddPool(this.NebuSceneMgrGO.transform);
            //.AddDataService(satelliteService);
            Debug.Log("[Nebu剧场对象管理器]：NebuTheatreManager 初始化完成。");


            //if (mascotGO != null) mascot = (NebuAudioMascot)mascotGO
            //    .AddComponent<NebuAudioMascot>()
            //    .AddApp(this)
            //    .AddEventManager(this.eventManager);
            //Debug.Log("[Nebu场景内引导物管理器]：NebuMascot 吉祥物初始化完成。");


            //添加用户界面管理器
            if (NebuUiMgrGO != null) uiManager = ((NebuUIManager)NebuUiMgrGO
                .AddComponent<NebuUIManager>()
                .AddEventManager(this.eventManager))
                .AddPool(MainCanvasObject.transform)
                .Initialize(this.eventManager);
            uiManager.mainView.AddSystemLog("UI启动完成", this.name);
            Debug.Log("[Nebu用户界面管理器]：NebuUIMananger UI管理器初始化完成。");



            //加载多人协同管理器
            //networkManager = (NebuNetworkManager)this.gameObject
            //    .AddComponent<NebuNetworkManager>()
            //    .AddMessgenger(messenger)
            //    .AddApp(this)
            //    .AddEventManager(this.eventManager);
            //Debug.Log("[Nebu网络协同管理器]：网络管理器初始化完成。");

            //重要：EventSystem进行匹配subscribe。
            theatreManager.Initialize(this.eventManager);//这是为自己管理的对象注册到eventsystem
            uiManager.mainView.AddSystemLog("对象加载完成！", this.name);

            eventManager.MappingEventObjects();
            Debug.Log("[Nebu消息系统]：imadyEventManager初始化完成。");
            //Debug.Log(NebuStateObjectBase.SubscribeLog);

            //丢个加载完成的消息出去
            this.AfterNebuManagersInitialized(this, new EventArgs());
            Debug.Log("[Nebu应用管理器]: 应用程序加载完成！");
            uiManager.mainView.AddSystemLog("应用程序启动完成。", this.name);
        }

        /// <summary>
        /// 完成管理器加载后要处理的事务
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AfterNebuManagersInitialized(object sender, EventArgs e)
        {

        }
    }
}