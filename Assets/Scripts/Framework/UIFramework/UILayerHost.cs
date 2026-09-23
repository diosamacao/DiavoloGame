using System;
using UnityEngine;

namespace Framework.UIFramework
{
    public sealed class UILayerHost : MonoBehaviour
    {
        [SerializeField] private RectTransform hudLayer;
        [SerializeField] private RectTransform screenLayer;
        [SerializeField] private RectTransform toastLayer;
        [SerializeField] private RectTransform modalLayer;

        public RectTransform RequireLayerRoot(UILayer layer)
        {
            RectTransform layerRoot = layer switch
            {
                UILayer.Hud => hudLayer,
                UILayer.Modal => modalLayer,
                UILayer.Screen => screenLayer,
                UILayer.Toast => toastLayer,
                _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, "未知的 UI 层级。")
            };

            if (layerRoot == null)
                throw new InvalidOperationException($"UI 层级 {layer} 没有绑定 RectTransform。");
            
            return layerRoot;
        }
    }
}
