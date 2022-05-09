using NebulogUnityServer.DataModels;
using NebulogUnityServer.View;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NebulogUnityServer.View
{
    /// <summary>
    /// 一条系统消息的VM
    /// </summary>
    public class NebuMainSystemLogViewModel
    {
        [MadYViewProperty]
        public DateTime LeeVM_Time { get; set; }

        [MadYViewProperty]
        public string LeeVM_Loglevel { get; set; }

        [MadYViewProperty]
        public string LeeVM_Project { get; set; }

        [MadYViewProperty]
        public string LeeVM_Sender { get; set; }

        [MadYViewProperty]
        public string LeeVM_Message { get; set; }

        public NebuMainSystemLogViewModel()
        {

        }
    }

    [MadYResourcePath("MainCanvasCombo/")]
    public class NebuMainSystemLogViewItem : MadYViewBase, IMadYView
    {

    }
}