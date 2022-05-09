using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace imady.Message
{
    /// <summary>
    /// Hardware system level mouse input (e.g. click, right-click, drag.)
    /// </summary>
    public class MadYMouseInput : IMadYInput
    {
        [JsonProperty]
        public imadyInputTypeEnum InputType => imadyInputTypeEnum.Mouse;


        [JsonProperty]
        public string Content { get; set; }

        [JsonProperty]
        public string msg { get; set; }

        /// <summary>
        /// 鼠标拖拽 x - [0-1]；（相对于前一点）
        /// </summary>
        [JsonProperty]
        public float xDragDeltaRatio { get; set; }
        /// <summary>
        /// 鼠标拖拽 y - [0-1]；（相对于前一点）
        /// </summary>
        [JsonProperty]
        public float yDragDeltaRatio { get; set; }
        /// <summary>
        /// 鼠标拖拽 x - [0-1]；（相对于屏幕中心点）
        /// </summary>
        [JsonProperty]
        public float xDragCenterRatio { get; set; }
        /// <summary>
        /// 鼠标拖拽 y - [0-1]；（相对于屏幕中心点）
        /// </summary>
        [JsonProperty]
        public float yDragCenterRatio { get; set; }

        /// <summary>
        /// 鼠标滚轮 - [0-1]；
        /// </summary>
        [JsonProperty]
        public float scrollRatio { get; set; }
    }


}
