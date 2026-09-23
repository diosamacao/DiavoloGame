using System.Collections.Generic;
using UnityEngine;

namespace Framework.UIFramework
{
    [CreateAssetMenu(fileName = "UIPanelRegistry", menuName = "UIFramework/UIPanelRegistry")]
    public sealed class UIPanelRegistry : ScriptableObject
    {
        [SerializeField]
        private List<UIPanelDescriptor> panels = new List<UIPanelDescriptor>();
        
        public IReadOnlyList<UIPanelDescriptor> Panels => panels;

        public UIPanelDescriptor Require(UIPanelId id)
        {
            if(TryGet(id, out UIPanelDescriptor descriptor))
                return descriptor;
            
            throw new KeyNotFoundException($"没有找到 UIPanel 注册记录：{id}");
        }
        
        public bool TryGet(UIPanelId id, out UIPanelDescriptor panelDescriptor)
        {
            panelDescriptor = null;
            if (!id.IsValid)
            {
                return false;
            }

            foreach (UIPanelDescriptor descriptor in panels)
            {
                if (descriptor!=null && descriptor.PanelId.Equals(id))
                {
                    panelDescriptor = descriptor;
                    return true;
                }
            }
            return false;
        }
    }
}
