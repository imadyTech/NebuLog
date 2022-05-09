using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NebulogUnityServer.DataModels
{
    /// <summary>
    /// 限定了各种Service涉及到的数据模型TClass的索引方式
    /// </summary>
    /// <typeparam name="T">T是索引类型，可以是Int64，GUID或者string</typeparam>
    public interface IMadYServiceIndexable<T>
    {
        T objectIndex { get; set; }
    }
}
