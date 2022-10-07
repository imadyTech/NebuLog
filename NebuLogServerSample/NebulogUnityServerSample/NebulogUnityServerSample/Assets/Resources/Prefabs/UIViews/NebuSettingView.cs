using UnityEngine;
using UnityEngine.UI;
using NebulogUnityServer.DataModels;
using NebulogUnityServer.Manager;

namespace NebulogUnityServer.View
{
    [MadYResourcePath("UIViews/")]
    public class NebuSettingView : MadYViewBase
    {
        #region Inherited functions from ViewBase
        public new NebuSettingView SetManager(NebuUIManager uiManager) => base.SetManager(uiManager) as NebuSettingView;


        //public LeeSettingView SetCurrentUser(LeeUser user)
        //{
        //    LeeMainCanvasViewModel.currentUser = user;
        //    RenderView();
        //    return this;
        //}
        #endregion

        public new void Awake()
        {
            //Source = new LeeSettingViewModel();

            BindButtonActions();
            base.Awake();
        }
        public override void Hide()
        {
            //在view被关闭的时候把当前活动的子层级view关闭
            if (currentActivePanel != null)
            {
                currentActivePanel.Hide();
                currentActivePanel = null;
            }

            base.Hide();
        }


        protected override void BindButtonActions()
        {

            base.BindButtonActions();

        }

        #region 开放给UIView内部button控件的接口
        private IMadYView currentActivePanel; //当前被打开的对象属性面板

        #endregion
    }
}
