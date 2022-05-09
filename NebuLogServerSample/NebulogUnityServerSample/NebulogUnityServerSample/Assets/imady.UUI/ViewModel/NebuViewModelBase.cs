using NebulogUnityServer.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NebulogUnityServer.View
{
    public class NebuViewModelBase
    {
        /// <summary>
        /// 数据模型自动匹配字段
        /// </summary>
        /// <param name="viewModel"></param>
        /// <returns></returns>
        public virtual T TryMap<T>(object viewModel) where T: NebuViewModelBase
        {
            var properties = viewModel.GetType().GetProperties()
                .Where(a => a.GetCustomAttribute<MadYViewPropertyAttribute>() != null);
            var thisProperties = this.GetType().GetProperties()
                .Where(a => a.GetCustomAttribute<MadYViewPropertyAttribute>() != null);
            foreach (var property in properties)
            {
                foreach (var thisProperty in thisProperties)
                {
                    if (thisProperty.Name.Equals(property.Name))
                        thisProperty.SetValue(this, property.GetValue(viewModel));
                }
            }
            return this as T;
        }

    }
}
