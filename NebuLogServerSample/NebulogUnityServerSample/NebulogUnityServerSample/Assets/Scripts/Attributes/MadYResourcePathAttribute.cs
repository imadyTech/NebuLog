using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NebulogUnityServer.DataModels
{
    /// <summary>
    /// Indicate the sub path of a view/object under the LeeAppConfiguration.defaultUITemplatePrefabPath
    /// or LeeAppConfiguration.defaultLeeObjectPrefabPath (usually the "Resources/Prefabs" folder).
    /// </summary>
    public class MadYResourcePathAttribute : System.Attribute
    {
        /// <summary>
        /// Note: Do not include object name!!!
        /// </summary>
        public string LeePrefabSubPath { get; set; }

        public MadYResourcePathAttribute(string subpath)
        {
            this.LeePrefabSubPath = subpath;
        }
    }
}
