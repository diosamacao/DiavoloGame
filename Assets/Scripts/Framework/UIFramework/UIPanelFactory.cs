using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Framework.UIFramework
{
    public sealed class UIPanelFactory
    {
        public TPanel Instantiate<TPanel>(GameObject prefab, Transform parent) where TPanel : UIPanel
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            
            GameObject instance = Object.Instantiate(prefab, parent,false);
            
            instance.SetActive(false);

            if (!instance.TryGetComponent(out TPanel panel))
            {
                Object.Destroy(instance);
                throw new InvalidOperationException($"UI Prefab {prefab.name} 的根节点没有挂载 {typeof(TPanel).Name}。");
            }
            return panel;
        }
    }
}
