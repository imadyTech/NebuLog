using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace imady.Message
{
    public class MadYServiceMsgBase<T> 
    {
        public bool success { get; set; }

        public T msgBody { get; set; }

        public string msg { get; set; }
    }

}
