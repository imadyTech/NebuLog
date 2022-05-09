using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NebulogUnityServer.Common;
using imady.Message;
using imady.Event;
using NebulogUnityServer.DataModels;
using NebulogUnityServer.Manager;

namespace NebulogUnityServer.View
{
    [MadYResourcePath("UIViews/")]
    public class NebuTopButtonGroupView : MadYViewBase
    {
        public new void Awake()
        {
            //Source = LeeOperationModeEnum.Scouting;

            BindButtonActions();
            base.Awake();
        }


        #region ---------- UIView包含的Button对象的引用声明 -- 请在unity editor中进行预赋值 ------------
        //---------- PlayMode Setting Button Group 场景模式---------//
        public Button Toggle_Start_Button;//Start nebulogServer process

        //---------- 3D/2D Button Group ---------//
        public Button Toggle_Earth_3D2D_Button;//3D-2D视图切换

        //---------- Data Play Button Group 数据控制---------//
        public Button Toggle_SpeedDown_Button;//首帧
        public Button Toggle_Play_Button;//播放
        public Button Toggle_Pause_Button;//暂停
        public Button Toggle_SpeedUp_Button;//末帧

        //---------- Setting Button Group 设置---------//
        public Button Toggle_Setting_Button;//打开设置面板
        #endregion


        public new NebuTopButtonGroupView SetManager(NebuUIManager uiManager)=> base.SetManager(uiManager) as NebuTopButtonGroupView;

        /// <summary>
        /// 绑定按钮对象实例与对应的动作
        /// </summary>
        protected override void BindButtonActions()
        {
            Toggle_Start_Button.onClick.AddListener(() => {
                m_manager.NotifyObservers(new MadYUnityUIMessage<NebulogServerInitiateMsg>());
            });

            //----------Satellite Data Play Button Group 卫星数据控制---------//
            Toggle_SpeedDown_Button.onClick.AddListener(this.OnToggle_SpeedDown_Clicked);//减速
            Toggle_Play_Button.onClick.AddListener(this.OnToggle_Play_Clicked);//播放
            Toggle_Pause_Button.onClick.AddListener(this.OnToggle_Pause_Clicked);//暂停
            Toggle_SpeedUp_Button.onClick.AddListener(this.OnToggle_SpeedUp_Clicked);//加速

            //----------Setting Button Group 设置---------//
            Toggle_Setting_Button.onClick.AddListener(this.OnToggle_Setting_Button_Clicked);
        }


        #region ----------///3D_2D///---------
        public void OnToggle_3D_2D_Button_Clicked()
        {
            Debug.Log("OnToggle_3D_2D_Button_Clicked: " + m_manager.m_earthDisplayMode);
            switch (m_manager.m_earthDisplayMode)
            {
                case "3D":
                    {
                        m_manager.m_earthDisplayMode = "2D"; 
                        m_manager.NotifyObservers(new MadYUnityUIMessage<Nebu2DViewMsg>());
                        break;
                    }
                case "2D":
                    {
                        m_manager.m_earthDisplayMode = "3D"; 
                        m_manager.NotifyObservers(new MadYUnityUIMessage<Nebu3DViewMsg>());
                        break;
                    }
            }
        }

        #endregion


        #region ---------- Play Button Group ---------
        public Text speedLabel;
        private void OnToggle_SpeedUp_Clicked()
        {
            var message = new MadYUnityUIMessage<MadYUnityButtonInput>()
            {
                timeSend = DateTime.Now,
                senderId = Guid.NewGuid(),
                messageBody = new MadYUnityButtonInput() { msg = "SateData_SpeedUp", actionItem = speedLabel }
            };
            m_manager.On_UnityButton_Clicked(message);
        }

        private void OnToggle_Pause_Clicked()
        {
            var message = new MadYUnityUIMessage<MadYUnityButtonInput>()
            {
                timeSend = DateTime.Now,
                senderId = Guid.NewGuid(),
                messageBody = new MadYUnityButtonInput() { msg = "SateData_Pause", actionItem = speedLabel }
            };
            m_manager.On_UnityButton_Clicked(message);
        }

        private void OnToggle_Play_Clicked()
        {
            var message = new MadYUnityUIMessage<MadYUnityButtonInput>()
            {
                timeSend = DateTime.Now,
                senderId = Guid.NewGuid(),
                messageBody = new MadYUnityButtonInput() { msg = "SateData_Play", actionItem = speedLabel }
            };
            m_manager.On_UnityButton_Clicked(message);
        }

        private void OnToggle_SpeedDown_Clicked()
        {
            var message = new MadYUnityUIMessage<MadYUnityButtonInput>()
            {
                timeSend = DateTime.Now,
                senderId = Guid.NewGuid(),
                messageBody = new MadYUnityButtonInput() { msg = "SateData_SpeedDown", actionItem = speedLabel }
            };
            m_manager.On_UnityButton_Clicked(message);
        }
        #endregion


        #region ----------///Setting Button Group 设置///---------
        /// <summary>
        /// Setting 设置
        /// </summary>
        public void OnToggle_Setting_Button_Clicked()
        {
            //通过UIManager提供的直接接口打开SettingView
            m_manager.OnToggle_SettingView_Button_Clicked();
        }
        #endregion
    }
}
