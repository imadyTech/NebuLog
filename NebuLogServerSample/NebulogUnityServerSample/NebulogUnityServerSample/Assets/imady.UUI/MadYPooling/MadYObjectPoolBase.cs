using System.Collections.Generic;
using UnityEngine;
using NebulogUnityServer.Common;
using System.Linq;
using System;

namespace NebulogUnityServer.Manager
{
    /// <summary>
    /// MadYObjectPoolBase，因此可以实现对象的SetActive、Instantiate、Destroy等方法。
    /// </summary>
    public class MadYObjectPoolBase : MonoBehaviour
    {
        /// <summary>
        /// 缓存所有对象
        /// </summary>
        private List<GameObject> objectPool = new List<GameObject>();

        private SyncResourceLoader loader = new SyncResourceLoader();


        #region CONSTRUCTOR & DESTRUCTOR
        public virtual void OnDestroy()
        {
            foreach (GameObject obj in objectPool)
            {
                Destroy(obj);
            }
            objectPool.Clear();
        }
        #endregion

        //todo: 目前的objectpool不实现销毁功能

        /// <summary>
        /// 根据定义好的prefab名称（可能包含‘/’字符）查找对象（如果pool中已经存在，则只是取出而不重新实例化）
        /// </summary>
        /// <param name="objectPrefabPath">要求传入完整的prefab路径（例如“/Prefabs/UITemplate/xxxx”）</param>
        /// <returns></returns>
        protected virtual GameObject WakeNglObject(string objectPrefabPath, Transform parentTransform, bool isNewInstance)
        {
            if (string.IsNullOrEmpty(objectPrefabPath))
                return null;
            var gameobject = objectPool
                .Where(o => o.name.Equals(objectPrefabPath))
                .FirstOrDefault();
            if (isNewInstance || gameobject == null)//cache missing, instantiate a new object
            {
                gameobject = CreateObject(objectPrefabPath, parentTransform);
                if (gameobject == null)
                {
                    Debug.LogWarning($"NglObject对象 '{objectPrefabPath}' 加载失败！");
                    return null;
                }
            }

            gameobject.SetActive(true);
            return gameobject;
        }

        /// <summary>
        /// 生成指定类型的NglObject对象实例（可能生成多重实例）
        /// </summary>
        /// <typeparam name="SourceType"></typeparam>
        /// <param name="objectPrefabPath">完整的prefab资源路径</param>
        protected GameObject CreateObject(string objectPrefabPath, Transform parentTransform)
        {
            if (string.IsNullOrEmpty(objectPrefabPath)) return null;

            try
            {
                var gameobject = loader.LoadPrefab(objectPrefabPath);
                if (gameobject == null) return null;

                var go = UnityEngine.Object.Instantiate(gameobject);
                go.name = objectPrefabPath;
                if (parentTransform != null) go.transform.SetParent(parentTransform);
                objectPool.Add(go);//入池
                return go;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }

        protected virtual void HibernateObject(GameObject obj)
        {
            if (objectPool.Contains(obj))
                obj.SetActive(false);
        }
    }
}