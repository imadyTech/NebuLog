using UnityEngine;

namespace NebulogUnityServer
{
    public class NebuSingleton<T> : MonoBehaviour where T : NebuSingleton<T>
    {
        public static T Instance { get; private set; }

        protected virtual void Awake()
        {
            if (Instance == null)
            {
                Instance = (T)this;
                DontDestroyOnLoad(this.gameObject);
            }
            else
            {
                Destroy(this.gameObject);
            }
        }
    }
}
