using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace imady.Message
{
    public class MadYMessage<T> : MadYMessageBase where T : class
    {
        public virtual T messageBody { get; set; }

        public MadYMessage(T message)
        {
            messageBody = message;
        }
    }
}

