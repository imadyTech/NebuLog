using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NebuLogServer
{
    public class LogInfo
    {
        /// <summary>
        /// 发送信息的主体
        /// </summary>
        public string Sender { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; set; }

        public DateTime TimeOfLog { get; set; }
    }
}
