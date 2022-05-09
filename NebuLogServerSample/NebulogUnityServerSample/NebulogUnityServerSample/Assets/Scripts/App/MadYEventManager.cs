using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Collections;
using NebulogUnityServer;
using imady.Event;

namespace NebulogUnityServer
{
    /// <summary>
    /// 事件系统管理器
    /// </summary>
    public class MadYEventManager : NebuSingleton<MadYEventManager>
    {
        private List<IMadYEventObjectBase> eventSystemMembers;

        //同一消息类型，不允许多个provider提供
        private List<Tuple<Type, IMadYEventObjectBase>> providers;
        public int ProvidersCount { get => providers.Count; }
        //同一消息类型，可以被多个observer监听
        private List<Tuple<Type, IMadYEventObjectBase>> observers;
        public int ObserversCount { get => observers.Count; }

        public MadYEventManager()
        {
            providers = new List<Tuple<Type, IMadYEventObjectBase>>();
            observers = new List<Tuple<Type, IMadYEventObjectBase>>();
            eventSystemMembers = new List<IMadYEventObjectBase>();
        }

        /// <summary>
        /// 预登记providers/observers（此时不区分提供者、监听者）
        /// </summary>
        /// <param name="member"></param>
        public void Register(IMadYEventObjectBase member)
        {
            try
            {
                if (member.isObserver || member.isProvider)//冗余测试
                    eventSystemMembers.Add(member);
            }
            catch (Exception e)
            {
                throw new WarningException (e.Message);
            }
        }
        public void MappingEventObjects()
        {
            var providers = eventSystemMembers.Where(a => a.isProvider).ToArray();
            var observers = eventSystemMembers.Where(a => a.isObserver).ToArray();
            //依次扫描providers
            foreach (var provider in providers)
            {
                TrySubscribe(provider, observers);
            }
            void TrySubscribe(IMadYEventObjectBase provider, IMadYEventObjectBase[] obs)
            {
                foreach (var ob in obs)
                {
                    if (provider == ob) continue;
                    //执行Provider的Subscribe()方法
                    var p = provider;
                    try
                    {
                        p.Subscribe(ob);
                        //Debug.Log(p.ToString() + " subscribing " + ob.ToString());
                    }
                    catch (Exception e)
                    {
                        //Debug.Log("Subscribe Failed!");
                        throw new WarningException($"[MappingError] #PRVD#:{p.GetType().Name} #OBSV#:{ob.GetType().Name}: \n{e}");
                    }
                }
            }
        }

        /// <summary>
        /// 根据已注册对象IIMadYProvider/IIMadYObserver接口声明进行provder/observer匹配
        /// </summary>
        public void MappingEventObjectsByInterfaces()
        {
            var providers = eventSystemMembers.Where(a => a.isProvider).ToArray();
            var observers = eventSystemMembers.Where(a => a.isObserver).ToArray();
            //依次扫描providers
            foreach (var provider in providers)
            {
                TrySubscribe(provider, observers);
            }
            void TrySubscribe(IMadYEventObjectBase provider, IMadYEventObjectBase[] obs)
            {
                foreach (var ob in obs)
                {
                    if (provider == ob) continue;
                    //执行Provider的Subscribe()方法
                    var p = provider;
                    try
                    {
                        p.Subscribe(ob);
                        //Debug.Log(p.ToString() + " subscribing " + ob.ToString());
                    }
                    catch (Exception e)
                    {
                        //Debug.Log("Subscribe Failed!");
                        throw new WarningException($"[EventSystem Mapping Error] #PRVD#:{p.GetType().Name} #OBSV#:{ob.GetType().Name}: \n{e}");
                    }
                }
            }
        }

        [Obsolete]
        public void MappingObserversAndObservables()
        {
            //扫描providers的类型
            foreach (Tuple<Type, IMadYEventObjectBase> provider in providers)
            {
                //扫描observer的类型找出需要监听消息的对象
                //自动匹配消息type与observer监听类型
                var interestedObservers = (from o in observers
                                           where provider.Item1 == o.Item1
                                           select o.Item2)
                                          .ToList();
                //注册
                foreach (IMadYEventObjectBase observer in interestedObservers)
                {
                    var p = provider.Item2;
                    var o = observer;
                    // dynamic类型，需要确保代码无错误
                    try
                    {
                        p.Subscribe(o);
                        //Debug.Log($"{provider.Item2} {observer}成功！");
                    }
                    catch (Exception e)
                    {
                        throw new WarningException(e.Message);
                    }

                }
            }
            //Debug.Log("[NGL消息管理器]: 全部消息提供者-收听者匹配成功！");
        }
    }
}