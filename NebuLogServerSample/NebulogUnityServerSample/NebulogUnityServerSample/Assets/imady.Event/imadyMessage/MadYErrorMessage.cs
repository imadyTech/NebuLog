using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace imady.Message
{
    public class MadYErrorMessage<T> : MadYMessageBase where T : class
    {
        public T messageBody { get; set; }

        public MadYErrorMessage(T message)
        {
            messageBody = message;
        }
    }
}
