using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Framework.UIFramework
{
    [Serializable]
    public sealed class UIPanelDescriptor
    {
        [SerializeField] private UIPanelId panelId;
        [SerializeField] private string bundleName;
        [SerializeField] private string assetName;
        [SerializeField] private UILayer layer;
        [FormerlySerializedAs("lifeTime")] [SerializeField] private UIPanelLifeTime panelLifeTime;
        [SerializeField] private int prewarmCount;
        [SerializeField] private int maxPoolSize;

        public UIPanelId PanelId => panelId;
        public string BundleName => bundleName;
        public string AssetName => assetName;
        public UILayer Layer => layer;
        public UIPanelLifeTime PanelLifeTime => panelLifeTime;
        public int PrewarmCount => prewarmCount;
        public int MaxPoolSize => maxPoolSize;
        
    }
}
