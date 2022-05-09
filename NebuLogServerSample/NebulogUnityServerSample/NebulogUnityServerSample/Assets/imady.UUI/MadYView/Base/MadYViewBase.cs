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
        /// view/sub view����ֱ��ͨ��UIManager����
        /// </summary>
        protected NebuUIManager m_manager;

        #region ��ͼ��������
        /// <summary>
        /// �Ƿ�ɾ�����ŵ�C#�ű�������������Ч��
        /// </summary>
        public bool isRemoveScriptAfterRendering = false;

        ///// <summary>
        ///// ���ϽǼ�ʱ��
        ///// </summary>
        //public Text timeText;


        /// <summary>
        /// ��ͼģ�� (Ӧ�����ظ�ʹ�õ�view�ؼ�����combo�����δͨ��SetTemplate����ģ�壬����Ϊʹ�ø��ӵ�gameobject��
        /// </summary>
        GameObject _viewTemplate;
        GameObject ViewTemplate
        {
            get => _viewTemplate == null ? this.gameObject : _viewTemplate;
            set => _viewTemplate = value;
        }
        #endregion


        /// <summary>
        /// ����Դ
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
        /// ����UIView������Դ����
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
            ///ʵ�������Զ��󶨣�����
            RenderView();
        }

        /// <summary>
        /// ����UIView��һ��prefab����Ϊģ�壨����View-CS�󶨵Ķ�����˵��templateĬ�Ͼ���gameobject��
        /// </summary>
        /// <param name="template">Ҳ����Ĭ����transform.gameObject��Ϊtemplate����Ҫ��ǰ��NglViewBase����ű��ҵ�ģ��prefab�ϣ�</param>
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
        /// ���Ӷ�UIManager��ֱ�����ã��Ա�UIView֮����н���
        /// ð�ݷ�ʽ̫�����ˣ����� ~~~ 
        /// </summary>
        public virtual IMadYView SetManager(NebuUIManager manager)
        {
            this.m_manager = manager;
            return this;
        }

        /// <summary>
        /// ָ����ǰView�ĸ���ͼ
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
        /// ������datasource-��ͼ����һһ��Ӧ��ϵʱʹ�ã������ö�����ͻ��߼�����������Դ���븲�Ǳ�������
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
                    //��Ԫ���ͻ����ַ���
                    if (property.PropertyType.IsPrimitive || property.PropertyType.IsValueType || property.PropertyType == typeof(string))
                    {
                        var temp = property.GetValue(viewmodel).ToString();
                        var temp0 = property.Name;
                        BindingText(template, property.Name, property.GetValue(viewmodel).ToString());

                    }
                    //�����Ͷ���
                    if (property.PropertyType.IsClass && property.PropertyType != typeof(string))
                        BindCombo(template, property.GetValue(viewmodel));
                    //���϶���-> ListView����ʱ�޷�ʵ�֣���ΪView Templateû��ָ��List View layout������
                    //if(property.PropertyType.IsInterface && property.PropertyType.IsGenericType && property.PropertyType==typeof(IEnumerable<>))
                    //{
                    //    RenderListview(property.GetValue(viewmodel) as IEnumerable<object>);
                    //}
                    //Debug.Log($"[NglViewBase��ͼģ��] ��Ⱦ��{property.Name}={property.GetValue(source)}");
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
                    Debug.Log($"[NglViewBase��ͼģ��] ��Ⱦ��{property.Name}={property.GetValue(viewmodel)}");
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
        /// �����ṩ��ViewModel����Դ����һ���յ�ListView����������Ⱦ��
        /// </summary>
        /// <typeparam name="T">type of a certain ViewModel</typeparam>
        /// <param name="sourceVMs">����ԴVM���ϣ�VM�������LeeViewModelTypeAttribute��ǩָ��Viewģ��</param>
        /// <param name="container">ListView����</param>
        /// <param name="resultViewList">��Ⱦ��Ľ��</param>
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
            //��UIManager����view�����ء���ʾ���й���
            m_manager.HibernateView(this.gameObject);
        }

        public virtual void Show()
        {
            //��UIManager����view�����ء���ʾ���й���
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
