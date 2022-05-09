using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace imady.Message
{
    public class MadYServiceMsg : MadYServiceMsgBase<object>
    {
        public MadYServiceMsg()
        {
            base.msg = "";
            base.msgBody = null;
            base.success = false;
        }

        public MadYServiceMsg(bool success): this()
        {
            base.success = success;
        }
        public MadYServiceMsg(string msg) : this()
        {
            base.msg = msg;
        }
        public MadYServiceMsg(string msg, bool success): this(msg)
        {
            base.success = success;
        }

        public MadYServiceMsg(object value) : this()
        {
            base.msgBody = value;
            base.success = true;
        }

        public MadYServiceMsg(object value, string msg) : this(msg)
        {
            base.msgBody = value;
            base.success = true;
        }

    }
}
