using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NebulogUnityServer.DataModels
{
    /// <summary>
    /// Indicate the type of view for a certain viewmodel
    /// 指示viewmodel所对应的view视图类型
    /// </summary>
    public class MadYViewModelTypeAttribute : System.Attribute
    {
        /// <summary>
        /// Note: this tells which view should the viewmodel be mapped
        /// </summary>
        public Type LeeViewType { get; set; }

        public MadYViewModelTypeAttribute(Type viewtype)
        {
            this.LeeViewType = viewtype;
        }
    }
}
