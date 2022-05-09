using NebulogUnityServer.Manager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NebulogUnityServer.View
{
    /// <summary>
    /// 所有LeeView都需要实现的一些通用接口
    /// </summary>
    public interface IMadYView
    {
        GameObject gameObject { get; }

        /// <summary>
        /// 设置视图数据源viewmodel
        /// </summary>
        /// <param name="viewModel"></param>
        void SetViewModel(object viewModel);

        /// <summary>
        /// 将自身gameobject设置为视图模板
        /// </summary>
        /// <returns></returns>
        IMadYView SetTemplate();

        /// <summary>
        /// 设置视图模板
        /// </summary>
        /// <param name="template"></param>
        /// <returns></returns>
        IMadYView SetTemplate(GameObject template);

        /// <summary>
        /// 设置管理器（View类型对象管理器为LeeUIMananger）
        /// </summary>
        /// <param name="manager"></param>
        IMadYView SetManager(NebuUIManager manager);

        /// <summary>
        /// 设置父对象的transform（影响Hierarchy层级关系）
        /// </summary>
        /// <param name="parent"></param>
        void SetParent(Transform parent);

        /// <summary>
        /// 渲染视图（根据传入的Viewmodel与View对象层级中物体名称进行自动匹配）
        /// </summary>
        /// <returns></returns>
        GameObject RenderView();

        /// <summary>
        /// 隐藏自己（回调UIManager相应的对象池关闭方法，影响objectStack。
        /// </summary>
        void Hide();

        void Show();

        void ToggleOnOff();

        void Refresh(GameObject parent);
    }
}
