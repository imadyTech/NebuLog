using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Collections;
using imady.Message;
using imady.Event;
using NebulogUnityServer.Manager;
using NebulogUnityServer.DataModels;
using System.Reflection;

namespace NebulogUnityServer
{
    /// <summary>
    /// APP里涉及的模拟对象池子，基于ObjectPoolBase实现；
    /// </summary>
    public class MadYObjectPool : MadYObjectPoolBase
    {
        public new void OnDestroy()
        {
            base.OnDestroy();
        }


        #region PUBLIC METHODS
        MadYResourcePathAttribute attributeCache = null;
        /// <summary>
        /// 根据object type激活实例（默认会产生对象的克隆体）
        /// </summary>
        /// <param name="leeObjectType">leeObjectType类型</param>
        internal GameObject WakeObject(Type leeObjectType, Transform parent)
        {
            try
            {
                attributeCache = leeObjectType.GetCustomAttribute<MadYResourcePathAttribute>();
                if (attributeCache == null) 
                    throw new ArgumentException("The object parameter did not includes 'LeeResourcePathAttribute'.");
                var path = $"{NebulogAppConfiguration.defaultMadYObjectPrefabPath}{attributeCache.LeePrefabSubPath}{leeObjectType.Name}";
                //注意：所有对象可能会有克隆体 - 与ViewPool方式不同。
                var objectResult = base.WakeNglObject(path, parent, true);
                return objectResult;
            }
            catch (Exception e)
            {
                //throw new ArgumentException("The type of LeeView may not include an attribute of 'LeeViewResourcePath'. Please check again.");
                Debug.LogError($"[GAMEOBJECT INSTANTIATE ERROR] '{this.gameObject.name}' : {e}");
                return null;
            }

        }

        /// <summary>
        /// 根据对象的层级名称（完整的Resources下路径+名称）激活实例
        /// </summary>
        /// <param name="leeObjectType">leeObjectType类型</param>
        internal GameObject WakeObject(string objectFullName, Transform parent)
        {
            return base.WakeNglObject(NebulogAppConfiguration.defaultMadYObjectPrefabPath + objectFullName, parent, true);
        }

        internal void HibernateObject(MadYEventObjectBase Object)
        {
            base.HibernateObject(Object.gameObject);
        }
        #endregion
    }
}
