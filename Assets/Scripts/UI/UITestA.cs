
using Framework.UIFramework;
using UnityEngine;

namespace UI
{
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
