using imady.Event;
using imady.Message;
using imady.NebuLog;
using NebulogUnityServer.DataModels;
using NebulogUnityServer.View;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


namespace NebulogUnityServer.View
{
    [MadYResourcePath("LeeMainCanvasCombo/")]
    public class NebuMainSystemLogPanel : MadYViewBase, IMadYView,
        IMadYObserver<NebuLogMsg>
    {
        public NebuMainSystemLogViewItem itemTemplate; //单一记录的视图模板，在unity editor中预赋值
        public VerticalLayoutGroup systemLogContainer;//容器，在unity editor中预赋值
        public int maxLogNumber = 100; //最大保留的日志条数（超出后移除最早入列的记录）
        private int logCountIndex = 0;

        Queue<NebuMainSystemLogViewModel> m_logVMs;//日志VM集合
        Queue<IMadYView> m_logView;//每个Log信息的ItemView对象集合
        private Vector3 logContainerHeight => new Vector3(0, systemLogContainer.preferredHeight, 0f);
        private Vector3 logItemHeight = new Vector3(0, 50, 0f);//一条记录viewItem的单位行高度
        private Vector3 logViewCurrentPos;//用于滚动ListView

        protected override void Awake()
        {
            base.Awake();
        }
        public void Init()
        {
            
            m_logVMs = new Queue<NebuMainSystemLogViewModel>();
            m_logView = new Queue<IMadYView>();
            logViewCurrentPos = systemLogContainer.GetComponent<RectTransform>().anchoredPosition3D;
        }

        /// <summary>
        /// 传入数据，VM驱动视图方式渲染
        /// </summary>
        /// <param name="logVMs"></param>
        /// <returns></returns>
        public NebuMainSystemLogPanel SetViewModel(Queue<NebuMainSystemLogViewModel> logVMs)
        {
            m_logVMs = logVMs;//缓存引用
            this.RenderListview<NebuMainSystemLogViewModel>(m_logVMs, systemLogContainer);
            return this;
        }

        /// <summary>
        /// 这个方法隐藏base的方法以免引起错误
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public new NebuMainSystemLogPanel SetViewModel(object source)
        {
            try
            {
                this.SetViewModel(source as Queue<NebuMainSystemLogViewModel>);
            }
            catch (Exception e)
            {
                Debug.LogError($"{this.name} is not able to cast object to List<MainSystemLogViewModel>.\n{e}");
            }
            return this;
        }

        /// <summary>
        /// 实例化单条记录的viewItem
        /// </summary>
        /// <param name="viewmodel"></param>
        /// <param name="container"></param>
        /// <returns></returns>
        protected virtual IMadYView RenderItem(NebuMainSystemLogViewModel viewmodel, LayoutGroup container)
        {
            var itemView = GameObject.Instantiate(itemTemplate, container.transform, false)
                    .GetComponent<IMadYView>();
            itemView.SetViewModel(viewmodel);
            return itemView;
        }

        /// <summary>
        /// 批量添加
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sourceViewModel">批量VM</param>
        /// <param name="container">包含item的容器</param>
        /// <returns></returns>
        protected Queue<IMadYView> RenderListview<T>(Queue<T> sourceViewModel, LayoutGroup container)
        {
            foreach (NebuMainSystemLogViewModel viewmodel in sourceViewModel as Queue<NebuMainSystemLogViewModel>)
            {
                m_logVMs.Enqueue(viewmodel);
                m_logView.Enqueue(RenderItem(viewmodel, container));
            }
            return m_logView;
        }

        /// <summary>
        /// 单条添加viewItem
        /// </summary>
        /// <param name="content"></param>
        /// <param name="sender"></param>
        public void AddLog(DateTime time, string projectname, string sendername, string loglevel, string message)
        {
            if (m_logVMs == null)
                throw new InvalidOperationException("MainSystemLogPanel is not initialized yet!");
            var itemVM = new NebuMainSystemLogViewModel()
            {
                LeeVM_Time = time,
                LeeVM_Loglevel = loglevel,
                LeeVM_Project = projectname,
                LeeVM_Sender = sendername,
                LeeVM_Message = message
            };
            m_logVMs.Enqueue(itemVM);
            m_logView.Enqueue(RenderItem(itemVM, this.systemLogContainer));
            logCountIndex++;
            if (m_logVMs.Count > maxLogNumber)//超出最大数量后移除最早入列的对象
            {
                m_logVMs.Dequeue();
                var earliestItem = m_logView.Dequeue();
                Destroy(earliestItem.gameObject);
            }

            systemLogContainer.GetComponent<RectTransform>().anchoredPosition3D = logViewCurrentPos += logItemHeight;//自动滚动一行
            //Debug.Log($"logViewCurrentPos {content} - Pos:{logViewCurrentPos}");
        }

        public void OnNext(NebuLogMsg message)
        {
            AddLog(message.TimeOfLog, message.ProjectName, message.SenderName, message.LogLevel, message.LoggingMessage);
        }
    }

}