using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using NebulogUnityServer.Common;
using imady.Message;
using imady.Event;
using NebulogUnityServer.DataModels;
using NebulogUnityServer.Manager;

namespace NebulogUnityServer.View
{
    [MadYResourcePath("UIViews/")]
    public class NebuMainCanvasView : MadYViewBase
    {
        /// <summary>
        /// 指向MainSystemLogPanel系统消息面板
        /// </summary>
        public NebuMainSystemLogPanel mainSystemLogPanel;


        protected override void Awake()
        {
            base.Awake();
        }
        public NebuMainCanvasView Init(MadYEventManager eventmanager)
        {
            mainSystemLogPanel.AddEventManager(eventmanager);

            mainSystemLogPanel.Init();
            return this;
        }

        public new NebuMainCanvasView SetManager(NebuUIManager uiManager)=> base.SetManager(uiManager) as NebuMainCanvasView;


        public void AddSystemLog(string content, string sender)
        {
            mainSystemLogPanel.AddLog(content, sender);
        }

        public override void ToggleOnOff()
        {
            //MainCanvasView不通过viewPool管理，因此需要覆盖基类方法
            base.isOnOff = !base.isOnOff;
            if (base.isOnOff)
                this.gameObject.SetActive(true);
            else
                this.gameObject.SetActive(false);

        }

    }
}
