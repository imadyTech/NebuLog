using NebulogUnityServer;
using imady.Message;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;


namespace imady.Event
{
    /// <summary>
    /// 场景中的活动物体的基类
    /// Any gameobject inheritated from MadYEventObjectBase will capable for communicate in imadyEventSystem.
    /// </summary>
    public abstract class MadYEventObjectBase : MonoBehaviour,  IMadYEventObjectBase
    {
        public static string SubscribeLog = string.Empty;

        public static string MessageLog = string.Empty;

        protected MadYEventManager eventsystemCache;

        protected MadYEventObjectBase()
        {
            observers = new List<IMadYEventObjectBase>();
        }

        protected virtual void Awake()
        {
            //TODO: adding to the event system and dynamically mapping by the eventManager...
        }




        #region imadyEventSystem METHODS IMPLEMENTATION
        //--------------------------------

        public virtual MadYEventObjectBase AddEventManager(MadYEventManager eventSystem)
        {
            eventSystem.Register(this);
            eventsystemCache = eventSystem;
            return this;
        }

        /// <summary>
        /// IOBSERVER接口的实现。（因为考虑到子类需要实现提供或者监听多种类型的消息，因此父类中不以泛型方式实现接口）
        /// </summary>
        public virtual void OnCompleted()
        {

        }

        public virtual void OnError(Exception ex)
        {

        }

        /// <summary>
        /// Unsubscriber实际上还没实现
        /// </summary>
        /// <typeparam name="T"></typeparam>
        protected class Unsubscriber<T> : IDisposable where T : IMadYEventObjectBase
        {
            private List<T> _observers;
            private T _observer;

            public Unsubscriber(List<T> observers, T observer)
            {
                this._observers = observers;
                this._observer = observer;
            }

            public void Dispose()
            {
                if (_observer != null && _observers.Contains(_observer))
                    _observers.Remove(_observer);
            }
        }
        #endregion


        #region IMadYEventObjectBase supporting features 
        public bool isProvider => providerInterfaces.Count() > 0;

        public bool isObserver => observerInterfaces.Count() > 0;

        public IEnumerable<Type> providerInterfaces => GetType().GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMadYProvider<>));

        public IEnumerable<Type> observerInterfaces => GetType().GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMadYObserver<>));

        public Func<Type, bool> providerTester => new Func<Type, bool>(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IMadYProvider<>));

        public Func<Type, bool> observerTester => new Func<Type, bool>(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IMadYObserver<>));

        public bool isObservingMessage(Type observingMessageType)
        {
            var result = observerInterfaces.SelectMany(i => i.GetGenericArguments()).Contains(observingMessageType);
            return result;
        }
        #endregion


        #region IMadYEventObjectBase Subscribe/Notify
        protected List<IMadYEventObjectBase> observers;
        public void Subscribe(IMadYEventObjectBase observer)
        {
            //遍历自己的providers
            foreach (var providerInterface in providerInterfaces.SelectMany(i => i.GetGenericArguments()))
            {
                //Debug.Log($"[EventObject]: {this.name}: {providerInterface.Name} TYR: {observer.GetType().Name}");
                //oberver是否监听消息
                if (observer.isObservingMessage(providerInterface) && !observers.Contains(observer))
                {
                    SubscribeLog += ($"[Subscribe]: {this.ToString()}: {providerInterface.Name} <--> {observer.GetType().Name}\n");
                    //Debug.Log($"[Subscribe] <--> {observer.GetType().Name}");
                    //如果是则添加到List
                    this.observers.Add(observer);
                }
            }
        }

        /// <summary>
        /// 通知消息所有的相关监听者
        /// TODO: 设计MessagePool以提升运行效率。
        /// The message data is better to be managed by a MessagePool for higher efficiency (as messages are frequently used object.)
        /// </summary>
        /// <param name="message"></param>
        public void NotifyObservers(MadYMessageBase message)
        {
            Type messaggeType = message.GetType();
            var prospectedObservers = observers.Where(o => o.isObservingMessage(messaggeType)).ToList();

            foreach (var observer in prospectedObservers)
            {
                //Debug.Log($"测试 {this.GetType()}是否监听：{observer.GetType()}");
                MethodInfo methodinfo = observer.GetType().GetMethod("OnNext", new Type[] { messaggeType });
                if (methodinfo != null)
                    methodinfo.Invoke(observer, new object[] { message });
            }
        }
        #endregion
    }
}
