using System;
using System.Collections.Generic;

namespace Framework.UIFramework
{
    public readonly struct UIPanelKey<TPanel> where TPanel : UIPanel
    {
        public UIPanelId Id { get; }

        public UIPanelKey(UIPanelId id)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException( "UIPanelId 不能为空。", nameof(id));
            }
            Id = id;
        }
    }
}
