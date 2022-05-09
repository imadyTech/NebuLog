using System.Collections.Generic;
using UnityEngine;
using NebulogUnityServer.Common;
using System.Linq;
using System;

namespace NebulogUnityServer.Manager
{
    /// <summary>
    /// MadYObjectPoolBase����˿���ʵ�ֶ����SetActive��Instantiate��Destroy�ȷ�����
    /// </summary>
    public class MadYObjectPoolBase : MonoBehaviour
    {
        /// <summary>
        /// �������ж���
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

        //todo: Ŀǰ��objectpool��ʵ�����ٹ���

        /// <summary>
        /// ���ݶ���õ�prefab���ƣ����ܰ�����/���ַ������Ҷ������pool���Ѿ����ڣ���ֻ��ȡ����������ʵ������
        /// </summary>
        /// <param name="objectPrefabPath">Ҫ����������prefab·�������硰/Prefabs/UITemplate/xxxx����</param>
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
                    Debug.LogWarning($"NglObject���� '{objectPrefabPath}' ����ʧ�ܣ�");
                    return null;
                }
            }

            gameobject.SetActive(true);
            return gameobject;
        }

        /// <summary>
        /// ����ָ�����͵�NglObject����ʵ�����������ɶ���ʵ����
        /// </summary>
        /// <typeparam name="SourceType"></typeparam>
        /// <param name="objectPrefabPath">������prefab��Դ·��</param>
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
                objectPool.Add(go);//���
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