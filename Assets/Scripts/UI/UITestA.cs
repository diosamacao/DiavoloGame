
using Framework.UIFramework;
using UnityEngine;

namespace UI
{
    /// <summary>UI 框架示例面板，记录创建、打开、刷新和释放生命周期。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "UI", "Assembly-CSharp", "UITestA")]
    public sealed class UITestA : UIPanel
    {
        protected override void OnCreate()
        {
            Debug.Log($"{name}: OnCreate");
        }

        protected override void OnOpen()
        {
            Debug.Log($"{name}: OnOpen");
        }

        protected override void OnRefresh()
        {
            Debug.Log($"{name}: OnRefresh");
        }

        protected override void OnClose()
        {
            Debug.Log($"{name}: OnClose");
        }

        protected override void OnRelease()
        {
            Debug.Log($"{name}: OnRelease");
        }
    }
}
