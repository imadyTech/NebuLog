

using System;
using System.Collections.Generic;
using System.Reflection;

namespace imady.Event
{
    public interface IMadYEventObjectBase
    {
        bool isProvider { get; }
        bool isObserver { get; }

        IEnumerable<Type> providerInterfaces { get; }
        IEnumerable<Type> observerInterfaces { get; }

        Func<Type, bool> providerTester { get; }
        Func<Type, bool> observerTester { get; }

        bool isObservingMessage(Type Interface);


        /// <summary>
        /// 2021-07-22 Update: 改进了过去由dynamic方式执行消息注册的缺陷
        /// </summary>
        /// <param name="observer"></param>
        void Subscribe(IMadYEventObjectBase observer);
    }
}