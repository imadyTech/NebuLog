using UnityEngine;
using UnityEngine.UI;
using NebulogUnityServer.DataModels;
using NebulogUnityServer.Manager;

namespace NebulogUnityServer.View
{
    [MadYResourcePath("UIViews/")]
    public class NebulogSettingView : MadYViewBase
    {
        #region 子层级视图combo控件的引用
        public GameObject satellitePropertyPanel;
        public GameObject carrierPropertyPanel;
        public GameObject destroyerPropertyPanel;
        public GameObject supplierPropertyPanel;
        public GameObject civilshipPropertyPanel;
        public GameObject missilelauncherPropertyPanel;
        public GameObject commCenterPropertyPanel;
        #endregion

        #region Unity Button对象引用 -- 请在UnityEditor中预赋值设置
        public Button Toggle_Satellite_Button;
        public Button Toggle_Carrier_Button;
        public Button Toggle_Destroyer_Button;
        public Button Toggle_Supplier_Button;
        public Button Toggle_CivilShip_Button;
        public Button Toggle_MissileLauncher_Button;
        public Button Toggle_CommCenter_Button;
        #endregion




        #region Inherited functions from ViewBase
        public new NebulogSettingView SetManager(NebuUIManager uiManager) => base.SetManager(uiManager) as NebulogSettingView;


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
