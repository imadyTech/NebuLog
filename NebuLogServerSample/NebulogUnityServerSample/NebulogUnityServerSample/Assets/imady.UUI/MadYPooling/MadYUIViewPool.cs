using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using System.Collections;
using imady.Message;
using NebulogUnityServer.Manager;
using System.Reflection;
using NebulogUnityServer.DataModels;

namespace NebulogUnityServer.View
{
    /// <summary>
    /// UIView对象池子，基于ObjectPoolBase实现；
    /// ViewPool层实现UIView的Stack管理。
    /// </summary>
    public class MadYUIViewPool : MadYObjectPoolBase
    {
        /// <summary>
        /// 用于实现navigation功能
        /// </summary>
        private Stack<GameObject> viewStack = new Stack<GameObject>();
        /// <summary>
        /// 返回最顶层的view
        /// </summary>
        private GameObject currentObject => viewStack == null ? null : viewStack.Peek();
        public IMadYView currentView { get => currentObject.GetComponent<IMadYView>(); }


        public new void OnDestroy()
        {
            viewStack.Clear();
            base.OnDestroy();
        }




        /*
        public GameObject WakeNglView(NglCourse message)
        {
            var currentObject = WakeNglView(typeof(NglCourseView));
            currentObject.GetComponent<NglCourseView>()
                .SetTemplate(currentObject)
                .SetDataSource(message);
            base.currentObject = currentObject;
            return currentObject;
        }

        public GameObject WakeNglView(NglCourseStage message)
        {
            var view = WakeNglView(typeof(NglCourseStageView));
            view.GetComponent<NglCourseStageView>()
            .SetTemplate(view)
            .SetDataSource(message);
            base.currentObject = view;
            return view;
        }

        /// <summary>
        /// 激活InGameView，也就是一个NglCourseStepView
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public GameObject WakeNglView(NglCourseStep message)
        {
            var view = WakeNglView(typeof(LeeCourseStepView));
            view.GetComponent<LeeCourseStepView>()
            .SetTemplate(view)
            .SetDataSource(message)
            .SetState(imadyApp.imadyObjectState.FullOn);
            base.currentObject = view;

            //注意：InGameView在此应用中代指NglCourseStepView
            inGameView = view.GetComponent<NglViewBase>();
            return view;
        }

        /// <summary>
        /// 激活课程选择界面
        /// </summary>
        /// <returns></returns>
        public NglCourseSelectionView WakeCourseSelectionView()
        {
            var view = WakeNglView(typeof(NglCourseSelectionView))
                .GetComponent<NglCourseSelectionView>();
            view.SetState(imadyApp.imadyObjectState.FullOn);
            return view;
        }

        public LeeUserLoginView WakeUserLoginView(NglVRPlayer player)
        {
            var view = WakeNglView(typeof(LeeUserLoginView))
                .GetComponent<LeeUserLoginView>();
            view.SetNglVRPlayer(player)
                .Refresh(null)
                .SetState(imadyApp.imadyObjectState.FullOn);
            return view;
        }

        internal LeeUserRegisterView WakeUserRegisterView(NglVRPlayer player)
        {
            var view = WakeNglView(typeof(LeeUserRegisterView))
                .GetComponent<LeeUserRegisterView>();
            view.SetNglVRPlayer(player)
                .Refresh(null)
                .SetState(imadyApp.imadyObjectState.FullOn);
            return view;
        }
        */

        #region PUBLIC METHODS
        MadYResourcePathAttribute attributeCache = null;
        /// <summary>
        /// 根据ViewType激活NglView
        /// </summary>
        /// <param name="leeViewType">NglViewBase类型</param>
        internal GameObject WakeView(Type leeViewType, Transform parent, bool isNewInstance)
        {
            try
            {
                attributeCache = leeViewType.GetCustomAttribute<MadYResourcePathAttribute>();
                var path = $"{NebulogAppConfiguration.defaultUITemplatePrefabPath}{attributeCache.LeePrefabSubPath}{leeViewType.Name}";
                var objectResult = base.WakeNglObject(path, parent, isNewInstance);
                if(!isNewInstance) viewStack.Push(objectResult);
                Debug.Log($"<<- ViewStack PUSH : {viewStack.Peek()} : Count: {viewStack.Count}");
                return objectResult;
            }
            catch (Exception e)
            {
                //throw new ArgumentException("The type of LeeView may not include an attribute of 'LeeViewResourcePath'. Please check again.");
                Debug.LogException(e);
                Debug.LogError($"[ERROR] The type of {leeViewType.Name} did not include an attribute of 'LeeViewResourcePathAttribute'.");
                return null;
            }

        }

        /// <summary>
        /// 根据View的层级名称（完整的Resources下路径+名称）激活UIView实例
        /// </summary>
        /// <param name="leeViewType">leeView类型</param>
        internal GameObject WakeView(string viewFullName, Transform parent, bool isNewInstance)
        {
            return base.WakeNglObject(NebulogAppConfiguration.defaultUITemplatePrefabPath + viewFullName, parent, isNewInstance);
        }

        internal void HibernateView()
        {
            if (currentObject != null)
            {
                currentObject.SetActive(false);
                Debug.Log($"->> ViewStack POP: {viewStack.Peek()} : Count: {viewStack.Count}");
                viewStack.Pop();

                //currentView.SetState(imadyApp.imadyObjectState.Cooling);
            }
        }

        //public void ClearPrevious()
        //{
        //    if (currentObject != null)
        //    {
        //        currentObject.SetActive(false);
        //        objectStack.Pop();
        //        //currentView.SetState(imadyApp.imadyObjectState.Cooling);
        //    }
        //}
        #endregion
    }
}
