using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MyLogger
{
    public interface IMyLogger: ILogger
    {
        void LogCustom( string sender, string message);
    }

    public interface IMyLogger<out TCategoryName> : IMyLogger
    {

    }

}
