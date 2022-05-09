using System;
using UnityEngine;

namespace NebulogUnityServer
{
    public class NebulogAppConfiguration
    {
        /// <summary>
        /// MadYObject prefab预制体的存储路径
        /// </summary>
        public static string defaultMadYObjectPrefabPath = "Prefabs/";

        /// <summary>
        /// MadYView prefab预制体的存储路径
        /// </summary>
        public readonly static string defaultUITemplatePrefabPath = "Prefabs/";

        public static float defaultMouseDragRatio = 32f;//鼠标拖拽速度调节

        public static float defualtMouseScrollRatio = 0.05f;//鼠标滚轮缩放速度调节

        public readonly static Guid defaultSenderId = Guid.Empty;

        public readonly static Vector2 defaultScreenSize = new Vector2(Screen.width, Screen.height);

    }
}
