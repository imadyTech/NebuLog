using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NebulogUnityServer.DataModels;
using NebulogUnityServer.Manager;
using imady.Event;


namespace NebulogUnityServer.View
{
    public abstract class MadYViewBase : MadYEventObjectBase, IMadYView
    {
        protected bool isOnOff = true;

        /// <summary>
        /// view/sub view可以直接通过UIManager交互
        /// </summary>
        protected NebuUIManager m_manager;

        #region 视图对象引用
        /// <summary>
        /// 是否删除附着的C#脚本组件以提高运行效率
        /// </summary>
        public bool isRemoveScriptAfterRendering = false;

        ///// <summary>
        ///// 右上角计时器
        ///// </summary>
        //public Text timeText;


        /// <summary>
        /// 视图模板 (应用于重复使用的view控件或者combo；如果未通过SetTemplate传入模板，则认为使用附加的gameobject）
        /// </summary>
        GameObject _viewTemplate;
        GameObject ViewTemplate
        {
            get => _viewTemplate == null ? this.gameObject : _viewTemplate;
            set => _viewTemplate = value;
        }
        #endregion


        /// <summary>
        /// 数据源
        /// </summary>
        private object _viewModel;
        public virtual object ViewModel
        {
            get => _viewModel;
            set
            {
                _viewModel = value;
            }
        }

        /// <summary>
        /// 缓存UIView的数据源对象
        /// </summary>
        /// <param name="viewModel"></param>
        public virtual void SetViewModel(object viewModel)
        {
            ViewModel = viewModel;
            if(viewModel==null)
            {
                Debug.LogWarning($"[{this.name}] The viewmodel provided is null and won't go on view rendering.");
                return;
            }
            if (ViewTemplate == null)
                Debug.LogError("You must set the ViewTemplate object in advance to setting the datasource.");
            ///实现数据自动绑定（单向）
            RenderView();
        }

        /// <summary>
        /// 缓存UIView（一个prefab）作为模板（对于View-CS绑定的对象来说，template默认就是gameobject）
        /// </summary>
        /// <param name="template">也可以默认以transform.gameObject作为template（需要提前将NglViewBase子类脚本挂到模板prefab上）</param>
        public virtual IMadYView SetTemplate(GameObject template)
        {
            ViewTemplate = template;
            return this as IMadYView;
        }
        public virtual IMadYView SetTemplate()
        {
            SetTemplate(this.gameObject);
            return this as IMadYView;
        }


        /// <summary>
        /// 增加对UIManager的直接引用，以便UIView之间进行交互
        /// 冒泡方式太复杂了，不好 ~~~ 
        /// </summary>
        public virtual IMadYView SetManager(NebuUIManager manager)
        {
            this.m_manager = manager;
            return this;
        }

        /// <summary>
        /// 指定当前View的父视图
        /// </summary>
        /// <param name="parent"></param>
        public virtual void SetParent(Transform parent)
        {
            this.transform.SetParent(parent);
        }

        protected virtual void BindButtonActions()
        {
        }

        /// <summary>
        /// 适用于datasource-视图存在一一对应关系时使用；如果是枚举类型或者集合类型数据源，请覆盖本方法。
        /// </summary>
        /// <param name="source"></param>
        public virtual GameObject RenderView()
        {
            RenderView(this.ViewTemplate, this.ViewModel);
            return this.ViewTemplate;
        }
        protected virtual GameObject RenderView(GameObject template, object viewmodel)
        {
            var properties = viewmodel.GetType().GetProperties();
            foreach (PropertyInfo property in properties)
            {
                try
                {
                    //基元类型或者字符串
                    if (property.PropertyType.IsPrimitive || property.PropertyType.IsValueType || property.PropertyType == typeof(string))
                    {
                        var temp = property.GetValue(viewmodel).ToString();
                        var temp0 = property.Name;
                        BindingText(template, property.Name, property.GetValue(viewmodel).ToString());

                    }
                    //复合型对象
                    if (property.PropertyType.IsClass && property.PropertyType != typeof(string))
                        BindCombo(template, property.GetValue(viewmodel));
                    //集合对象-> ListView（暂时无法实现，因为View Template没有指定List View layout容器）
                    //if(property.PropertyType.IsInterface && property.PropertyType.IsGenericType && property.PropertyType==typeof(IEnumerable<>))
                    //{
                    //    RenderListview(property.GetValue(viewmodel) as IEnumerable<object>);
                    //}
                    //Debug.Log($"[NglViewBase视图模板] 渲染：{property.Name}={property.GetValue(source)}");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"View Rendering Error: {property.Name}: {e}");
                }
            }
            return template;
        }
        protected virtual GameObject RenderView_TMPText(GameObject template, object viewmodel)
        {
            //var properties = typeof(TDataSource).GetProperties();
            var properties = viewmodel.GetType().GetProperties();
            foreach (PropertyInfo property in properties)
            {
                try
                {
                    BindingText(template, property.Name, property.GetValue(viewmodel).ToString());
                    Debug.Log($"[NglViewBase视图模板] 渲染：{property.Name}={property.GetValue(viewmodel)}");
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                void BindingText(GameObject parentGameObject, string filedName, string content)
                {
                    var text = parentGameObject.GetComponent<TMP_Text>();
                    if (text != null && !String.IsNullOrEmpty(text.name) && text.name == filedName)
                        text.SetText(content);
                    foreach (Transform child in parentGameObject.transform)
                    {
                        BindingText(child.gameObject, filedName, content);
                    }
                }
            }
            return template;
        }
        public virtual List<IMadYView> RenderListview(IEnumerable<object> sourceVMCollection, Type template, LayoutGroup container)
        {
            //ClearLayoutContainer(container);
            var resultViewList = new List<IMadYView>();

            foreach (object item in sourceVMCollection)
            {
                var viewItem = m_manager.WakeView(template, container.gameObject, true);// true: create new viewitem instance
                viewItem.SetViewModel(item);
                resultViewList.Add(viewItem);
            }
            return resultViewList;
        }

        /// <summary>
        /// 根据提供的ViewModel数据源，对一个空的ListView容器进行渲染。
        /// </summary>
        /// <typeparam name="T">type of a certain ViewModel</typeparam>
        /// <param name="sourceVMs">数据源VM集合，VM项需带有LeeViewModelTypeAttribute标签指明View模板</param>
        /// <param name="container">ListView容器</param>
        /// <param name="resultViewList">渲染后的结果</param>
        /// <returns></returns>
        protected virtual List<IMadYView> RenderListview<T>(List<T> sourceVMs, LayoutGroup container)
        {
            //ClearLayoutContainer(container);
            var resultViewList = new List<IMadYView>();

            foreach (T item in sourceVMs)
            {
                var viewtype = item.GetType().GetCustomAttribute<MadYViewModelTypeAttribute>().LeeViewType;
                var viewItem = m_manager.WakeView(viewtype, container.gameObject, true);// true: create new viewitem instance
                viewItem.SetViewModel(item);
                resultViewList.Add(viewItem);
            }
            return resultViewList;
        }


        protected void BindingText(GameObject targetGameObject, string fieldName, string content)
        {
            var text = targetGameObject.GetComponent<Text>();
            if (text != null && !String.IsNullOrEmpty(text.name) && text.name == fieldName)
                text.text = content;
            foreach (Transform child in targetGameObject.transform)
            {
                BindingText(child.gameObject, fieldName, content);
            }
        }
        protected void BindCombo(GameObject targetGameObject, object propertyValue) { }

        protected void SetBackground(GameObject targetGameObject, string fieldName, Image background)
        {
            var image = targetGameObject.GetComponent<Image>();
            if (image != null && image.name == fieldName)
            {
                image.sprite = background != null ? background.sprite : null;
            }
            foreach (Transform child in targetGameObject.transform)
            {
                SetBackground(child.gameObject, fieldName, background);
            }

        }


        public virtual void Hide()
        {
            //由UIManager来对view的隐藏、显示进行管理
            m_manager.HibernateView(this.gameObject);
        }

        public virtual void Show()
        {
            //由UIManager来对view的隐藏、显示进行管理
            m_manager.WakeView(this.GetType(), this.transform.parent.gameObject, false);
        }

        protected virtual void ClearLayoutContainer(LayoutGroup container)
        {
            for (int i = 0; i < container.transform.childCount; i++)
            {
                Destroy(container.transform.GetChild(i).gameObject);
            }
        }

        public virtual void Refresh(GameObject parent)
        {
            Transform transform;
            for (int i = 0; i < parent.transform.childCount; i++)
            {
                transform = parent.transform.GetChild(i);
                GameObject.Destroy(transform.gameObject);
            }
        }

        public virtual void ToggleOnOff()
        {
            isOnOff = !isOnOff;
            if (isOnOff)
                Show();
            else
                Hide();
        }
    }





}
