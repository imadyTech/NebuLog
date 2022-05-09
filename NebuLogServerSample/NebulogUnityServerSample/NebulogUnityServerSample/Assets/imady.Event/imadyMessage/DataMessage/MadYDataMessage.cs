using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace imady.Message
{
    /// <summary>
    /// Messages from DataService 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class MadYDataMessage<T> : MadYMessageBase 
    {
        public virtual T messageBody { get; set; }


        public MadYDataMessage() { }

        public MadYDataMessage(T messageBody)
        {
            this.messageBody = messageBody;
        }
    }

}
