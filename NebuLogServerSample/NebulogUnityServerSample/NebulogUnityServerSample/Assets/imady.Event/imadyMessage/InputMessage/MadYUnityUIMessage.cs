using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace imady.Message
{
    /// <summary>
    /// Messages from Unity UI actions
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class MadYUnityUIMessage<T> : MadYMessageBase where T : IMadYInput
    {
        public virtual T messageBody { get; set; }


        public MadYUnityUIMessage() { }

        public MadYUnityUIMessage(T messageBody)
        {
            this.messageBody = messageBody;
        }
    }


    #region 具象消息的封装定义 - 非扩展需求下此框架冗余，抽象需求下此框架有利于消息响应调度（简单需求慎用）
    #region 场景模式切换相关消息体
    public class SimulationStartMsg : MadYUnityButtonInput, IMadYInput
    {
        public SimulationStartMsg() { msg = "SimulationStartMsg"; }
    }
    public class SimulationEndMsg : MadYUnityButtonInput, IMadYInput
    {
        public SimulationEndMsg() { msg = "SimulationEndMsg"; }
    }
   #endregion

    #region 视角切换相关消息体
    public class RearViewMsg : MadYUnityButtonInput, IMadYInput
    {
        public RearViewMsg() { Content = ""; msg = ""; }
    }
    public class SideViewMsg : MadYUnityButtonInput, IMadYInput
    {
        public SideViewMsg() { Content = ""; msg = ""; }
    }
    public class FirstViewMsg : MadYUnityButtonInput, IMadYInput
    {
        public FirstViewMsg() { Content = ""; msg = "SateEarthView"; }
    }
    #endregion

    #region Play控制相关消息体
    public class PauseMsg : MadYUnityButtonInput, IMadYInput
    {
        public int SateSpeedX = 0;
        public PauseMsg() { }
    }
    public class PlayMsg : MadYUnityButtonInput, IMadYInput
    {
        public PlayMsg() { }
    }

    #endregion

    #region 3D/2D相关消息体
    public class Nebu3DViewMsg : MadYUnityButtonInput, IMadYInput
    {
        public Nebu3DViewMsg() { Content = ""; msg = "3D"; }
    }
    public class Nebu2DViewMsg : MadYUnityButtonInput, IMadYInput
    {
        public Nebu2DViewMsg() { Content = ""; msg = "2D"; }
    }
    #endregion

    #region 键盘、鼠标操作相关消息体
    public class MouseDragMsg: MadYMouseInput, IMadYInput
    {
        public MouseDragMsg() { }
        public MouseDragMsg(float deltaX, float deltaY, float screenX, float screenY) 
        { 
            base.xDragDeltaRatio = deltaX; 
            base.yDragDeltaRatio = deltaY;
            base.xDragCenterRatio = screenX;
            base.yDragCenterRatio = screenY;
        }
        public MouseDragMsg(float deltaX, float deltaY) { base.xDragDeltaRatio = deltaX; base.yDragDeltaRatio = deltaY; }
        public MouseDragMsg(float scroll) { base.scrollRatio = scroll; }
    }

    public class CameraScrollMsg : MadYMessageBase, IMadYInput
    {
        [JsonProperty]
        public imadyInputTypeEnum InputType => imadyInputTypeEnum.App;

        [JsonProperty]
        public float CameraScale { get; set; }

        [JsonProperty]
        public string Content { get; set; }

        [JsonProperty]
        public string msg { get; set; }
    }

    #endregion

    #endregion
}
