using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace imady.Message
{
    /// <summary>
    /// App level virtual button (gameobject attched with a 'Button' component)
    /// </summary>
    public class MadYUnityButtonInput : IMadYInput
    {
        [JsonProperty]
        public imadyInputTypeEnum InputType => imadyInputTypeEnum.UnityButton;


        [JsonProperty]
        public string Content { get; set; }

        [JsonProperty]
        public string msg { get; set; }

        public object actionItem { get; set; }
    }
}
