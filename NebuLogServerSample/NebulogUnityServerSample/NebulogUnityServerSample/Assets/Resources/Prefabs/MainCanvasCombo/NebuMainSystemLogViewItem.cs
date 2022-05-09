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
        public string LeeVM_Id { get; set; }
        [MadYViewProperty]
        public string LeeVM_Sender { get; set; }
        [MadYViewProperty]
        public string LeeVM_Content { get; set; }

        public NebuMainSystemLogViewModel()
        {

        }
    }

    [MadYResourcePath("MainCanvasCombo/")]
    public class NebuMainSystemLogViewItem : MadYViewBase, IMadYView
    {

    }
}