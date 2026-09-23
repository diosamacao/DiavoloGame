using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Framework.UIFramework
{
    public sealed class MemoryPanelAssetProvider : IUIPanelAssetProvider
    {
        private readonly IReadOnlyDictionary<UIPanelId, GameObject> _prefabs;

        public MemoryPanelAssetProvider(IReadOnlyDictionary<UIPanelId, GameObject> prefabs)
        {
            _prefabs = prefabs ?? throw new ArgumentNullException(nameof(prefabs));
        }
        
        public Task<UIPanelAssetLease> LoadAsync(UIPanelDescriptor descriptor, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));

            if (!_prefabs.TryGetValue(
                    descriptor.PanelId,
                    out GameObject prefab) ||
                prefab == null)
            {
                throw new KeyNotFoundException(
                    $"没有找到 Panel Prefab：{descriptor.PanelId}");
            }
            
            UIPanelAssetLease panelLease = new UIPanelAssetLease(prefab,"memory",()=>{});
            return Task.FromResult(panelLease);
        }
    }
}
