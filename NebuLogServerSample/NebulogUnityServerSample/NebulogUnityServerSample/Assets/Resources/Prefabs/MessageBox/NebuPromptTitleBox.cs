using UnityEngine;
using NebulogUnityServer.DataModels;
using System.Collections;

namespace NebulogUnityServer.View
{
    /// <summary>
    /// 顶部消息提示（当前场景）
    /// </summary>
    [MadYResourcePath("MessageBox/")]
    public class NebuPromptTitleBox : MadYViewBase, IMadYView
    {
        public override void SetViewModel(object source)
        {
            base.SetViewModel(source);
            Timing(3);//默认3秒关闭自己
        }


        public NebuPromptTitleBox Timing(float seconds)
        {
            StartCoroutine(SetTimer(seconds));
            return this;
            
            IEnumerator SetTimer(float seconds)
            {
                yield return new WaitForSeconds(seconds);
                this.Hide();
            }
        }

    }
}