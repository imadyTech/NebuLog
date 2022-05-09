using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace imady.Message
{
    /// <summary>
    /// App level collision (e.g. mesh collision, mouse raycast collision)
    /// </summary>
    public class MadYUnityCollisionInput : IMadYInput
    {
        [JsonProperty]
        public imadyInputTypeEnum InputType => imadyInputTypeEnum.UnityCollision;

        [JsonProperty]
        public string Content { get; set; }

        [JsonProperty]
        public string msg { get; set; }
    }
}
