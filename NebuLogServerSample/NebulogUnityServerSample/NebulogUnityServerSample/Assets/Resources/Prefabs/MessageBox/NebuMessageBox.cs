using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using imady.Message;
using NebulogUnityServer.Common;
using NebulogUnityServer.Manager;
using NebulogUnityServer.DataModels;
using Newtonsoft.Json;

namespace NebulogUnityServer.View
{
    /// <summary>
    /// 简单的消息提示MessageBox
    /// </summary>
    [MadYResourcePath("MessageBox/")]
    public class NebuMessageBox : MadYViewBase, IMadYView
    {
        public Button OKButton;

        private NebuMessageBoxViewModel _source;
        public override object ViewModel
        {
            get => _source;
            set
            {
                _source = (NebuMessageBoxViewModel)value;
                RenderView();
            }
        }


        protected override void Awake()
        {
            //OK按钮的事件由激活消息盒的程序添加
            //OKButton.onClick.AddListener(base.Hide);
            base.Awake();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public override void SetViewModel(object source)
        {
            ViewModel = source as NebuMessageBoxViewModel;
        }
    }



    /// <summary>
    /// 对应于LeeMessageBox的视图模型
    /// </summary>
    [MadYViewModelType(typeof(NebuMessageBox))]
    public class NebuMessageBoxViewModel
    {
        [JsonProperty]
        public string promptTitle { get; set; }

        [JsonProperty]
        public string promptMsg { get; set; }


        public NebuMessageBoxViewModel()
        {
            promptTitle = "title";
            promptMsg = "message";
        }
        public NebuMessageBoxViewModel(string title, string msg)
        {
            promptTitle = title;
            promptMsg = msg;
        }
    }

}