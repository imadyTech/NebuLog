using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NebulogUnityServer.DataModels
{
    /// <summary>
    /// �޶��˸���Service�漰��������ģ��TClass��������ʽ
    /// </summary>
    /// <typeparam name="T">T���������ͣ�������Int64��GUID����string</typeparam>
    public interface IMadYServiceIndexable<T>
    {
        T objectIndex { get; set; }
    }
}
