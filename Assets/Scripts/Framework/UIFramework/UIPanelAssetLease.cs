using System;
using UnityEngine;

namespace Framework.UIFramework
{
    public sealed class UIPanelAssetLease : IDisposable
    {
        private Action _release;
        public GameObject Prefab { get; }
        public string AssetVersion { get; }
        public bool IsReleased { get; private set; }
        

        public UIPanelAssetLease(GameObject prefab, string assetVersion, Action release)
        {
            Prefab = prefab!=null ? prefab : throw new ArgumentNullException(nameof(prefab));
            AssetVersion = !string.IsNullOrWhiteSpace(assetVersion) ? assetVersion : 
                throw new ArgumentException(
                "资源版本不能为空。",
                nameof(assetVersion));
            _release = release!=null ? release : throw new ArgumentNullException(nameof(release));
        }
        
        //Dispose() 表示“我用完了，请结束生命周期并释放所拥有的资源”。实现接口的价值不只是函数名统一，还在于 using、异常安全和通用资源管理。
        public void Dispose()
        {
            if (IsReleased)
                return;
            
            IsReleased = true;
            
            Action release = _release;
            _release = null;
            release.Invoke();
        }
    }
}
