using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace imady.Message
{
    /// <summary>
    /// Hardware system level keyboard input (e.g. click, right-click, drag.)
    /// </summary>
    public class MadYKeyboardInput : IMadYInput
    {
        [JsonProperty]
        public imadyInputTypeEnum InputType => imadyInputTypeEnum.Key;

        /// <summary>
        /// indicating the specific key clicked
        /// </summary>
        [JsonProperty]
        public string Content { get; set; }

        [JsonProperty]
        public string msg { get; set; }
    }


}
