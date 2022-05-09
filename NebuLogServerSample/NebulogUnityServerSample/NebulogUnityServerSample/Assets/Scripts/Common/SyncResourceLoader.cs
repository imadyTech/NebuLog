using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace NebulogUnityServer.Common
{
    public class SyncResourceLoader: IDisposable
    {
        /// <summary>
        /// 加载项目路径下的prefabb
        /// </summary>
        /// <param name="resourceInfo">prefabs存储的完整路径</param>
        /// <returns></returns>
        public GameObject LoadPrefab(string resourceInfo)
        {
            if (!string.IsNullOrEmpty(resourceInfo))
            {
                try
                {
                    var result = Resources.Load<GameObject>(resourceInfo);
                    return result;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            Debug.LogException(new ArgumentNullException($"Full path of prefab must be provided!"));
            return null;
        }


        private bool disposed = false;
        ~SyncResourceLoader()
        {
            Dispose(false);
        }
        /// 执行与释放或重置非托管资源关联的应用程序定义的任务。
        public void Dispose()
        {
            Dispose(true);
        }
        private void Dispose(bool disposing)
        {
            if (!disposed)
            {
                //....
            }
            disposed = true;
        }
    }
}
