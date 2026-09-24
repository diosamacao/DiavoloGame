
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;


namespace Framework.UIFramework
{
    public sealed class UIManager
    {
        private readonly UIPanelRegistry _registry;
        private readonly IUIPanelAssetProvider _assetProvider;
        private readonly UIPanelFactory _panelFactory;
        private readonly UILayerHost _layerHost;
        
        private readonly Dictionary<UIPanelId,PanelEntry> _uiPanels = new Dictionary<UIPanelId, PanelEntry>();
        
        private readonly Stack<UIPanelId> _screenStack = new Stack<UIPanelId>();
        private readonly Stack<UIPanelId> _modalStack = new Stack<UIPanelId>();

        public UIManager(UIPanelRegistry registry, UIPanelFactory panelFactory, UILayerHost layerHost, IUIPanelAssetProvider assetProvider)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _panelFactory = panelFactory ?? throw new ArgumentNullException(nameof(panelFactory));
            _layerHost = layerHost ?? throw new ArgumentNullException(nameof(layerHost));
            _assetProvider = assetProvider ?? throw new ArgumentNullException(nameof(assetProvider));
        }
        
        private sealed class PanelEntry
        {
            public UIPanelDescriptor Descriptor { get; }
            public UIPanelAssetLease AssetLease { get; }
            public UIPanel Panel { get; }

            public PanelEntry(UIPanelDescriptor descriptor, UIPanelAssetLease assetLease, UIPanel panel)
            {
                Descriptor = descriptor;
                AssetLease = assetLease;
                Panel = panel;
            }
        }

        public async Task<TPanel> OpenAsync<TPanel>(UIPanelKey<TPanel> panelKey,CancellationToken cancellationToken = default) where TPanel : UIPanel
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (!panelKey.Id.IsValid)
                throw new ArgumentException("UIPanelKey中的Id无效。", nameof(panelKey));

            //尝试获取缓存中的PanelEntry里的Panel实例
            if (_uiPanels.TryGetValue(panelKey.Id, out PanelEntry cachedPanelEntry))
            {
                if (cachedPanelEntry.Panel == null)
                {
                    throw new InvalidOperationException(
                        $"Panel {panelKey.Id} 的缓存实例已经被销毁。");
                }
                
                //缓存中的具体 Panel 子类，是否与本次 UIPanelKey<TPanel> 请求的类型一致
                if (cachedPanelEntry.Panel is not TPanel cachedPanel)
                {
                    throw new InvalidOperationException(
                        $"Panel {panelKey.Id} 的实际类型是 " +
                        $"{cachedPanelEntry.Panel.GetType().Name}，" +
                        $"请求类型是 {typeof(TPanel).Name}。");
                }
                
                //打开界面
                OpenPanel(cachedPanelEntry);
                
                return cachedPanel;
            }


            UIPanelAssetLease lease = null;
            TPanel uiPanel = null;
            bool added = false;

            try
            {
                //_registry从注册列表获取UI
                UIPanelDescriptor descriptor = _registry.Require(panelKey.Id);
                if (descriptor.PanelLifeTime != UIPanelLifeTime.SingletonCached)
                    throw new NotSupportedException($"当前暂不支持 {descriptor.PanelLifeTime}。");

                //_assetProvider异步加载资源，使用await字段
                lease = await _assetProvider.LoadAsync(descriptor, cancellationToken);
                if (lease == null)
                    throw new InvalidOperationException($"Provider加载 {panelKey.Id} 时返回了空Lease。");
            
                //_layerHost获取UI挂载的层级
                RectTransform layerRoot = _layerHost.RequireLayerRoot(descriptor.Layer);
            
                //_panelFactory将UI预制体实例化出来并挂载到场景中，然后将PanelEntry缓存到_uiPanels中
                uiPanel = _panelFactory.Instantiate<TPanel>(lease.Prefab,layerRoot);
                PanelEntry panelEntry = new PanelEntry(descriptor, lease, uiPanel);
                _uiPanels.Add(panelKey.Id, panelEntry);
                added = true;
            
                //打开界面
                OpenPanel(panelEntry);
                
                return uiPanel;
            }
            catch
            {
                if (added)
                    _uiPanels.Remove(panelKey.Id);

                if (uiPanel != null)
                {
                    uiPanel.Release();
                    Object.Destroy(uiPanel.gameObject);
                }
                lease?.Dispose();
                throw;
            }
        }

        public bool Close(UIPanelId panelId)
        {
            if (!panelId.IsValid)
                throw new ArgumentException(
                    "UIPanelKey 中的 Id 无效。",
                    nameof(panelId));

            if (!_uiPanels.TryGetValue(
                    panelId,
                    out PanelEntry panelEntry))
            {
                throw new InvalidOperationException(
                    $"Panel {panelId} 尚未加载。");
            }

            if (panelEntry.Panel == null)
            {
                throw new InvalidOperationException(
                    $"Panel {panelId} 的缓存实例已经被销毁。");
            }
            
            if (!panelEntry.Panel.IsOpen)
                return false;

            switch (panelEntry.Descriptor.Layer)
            {
                case UILayer.Modal:
                    CloseStackTop(panelId, panelEntry.Panel, _modalStack);
                    RestoreStackTop(_modalStack);
                    break;

                case UILayer.Screen:
                    CloseStackTop(panelId, panelEntry.Panel, _screenStack);
                    RestoreStackTop(_screenStack);
                    break;

                case UILayer.Hud:
                case UILayer.Toast:
                    panelEntry.Panel.Close();
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            return true;
        }
        
        public bool ClosePanel<TPanel>(UIPanelKey<TPanel> panelKey)
            where TPanel : UIPanel
        {
            return Close(panelKey.Id);
        }

        public bool Back()
        {
            if (_modalStack.Count>0)
                return Close(_modalStack.Peek());
            if (_screenStack.Count>0)
                return Close(_screenStack.Peek());
            return false;
        }
        
        private void CloseStackTop(UIPanelId panelId, UIPanel panel, Stack<UIPanelId> stack)
        {
            if (stack.Count == 0 || !stack.Peek().Equals(panelId))
            {
                throw new InvalidOperationException(
                    $"Panel {panelId} 不是当前栈顶界面。");
            }

            stack.Pop();
            panel.Close();
        }

        private void RestoreStackTop(Stack<UIPanelId> stack)
        {
            if (stack.Count == 0)
                return;

            UIPanelId previousId = stack.Peek();

            if (!_uiPanels.TryGetValue(
                    previousId,
                    out PanelEntry previousEntry))
            {
                throw new InvalidOperationException(
                    $"栈顶 Panel {previousId} 不存在于缓存中。");
            }

            previousEntry.Panel.transform.SetAsLastSibling();
            previousEntry.Panel.Open();
        }

        private void OpenPanel(PanelEntry panelEntry)
        {
            if (panelEntry.Panel == null)
                throw new ArgumentNullException("panelEntry的Panel为空"+panelEntry);
            
            switch (panelEntry.Descriptor.Layer)
            {
                case UILayer.Modal:
                    OpenModalPanel(panelEntry.Descriptor.PanelId, panelEntry.Panel);
                    break;
                case UILayer.Screen:
                    OpenScreenPanel(panelEntry.Descriptor.PanelId, panelEntry.Panel);
                    break;
                case UILayer.Hud:
                case UILayer.Toast:
                    panelEntry.Panel.Open();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        //Screen界面只能打开一个，因此需要先隐藏之前的Screen界面。但是为了保留界面导航历史，关闭的界面不用出栈
        private void OpenScreenPanel(UIPanelId panelId, UIPanel panel)
        {
            if (_screenStack.Count > 0 && _screenStack.Peek().Equals(panelId))
            {
                panel.transform.SetAsLastSibling();
                panel.Open();
                return;
            }

            if (_screenStack.Contains(panelId))
            {
                PopToScreen(panelId);
                panel.transform.SetAsLastSibling();
                panel.Open();
                return;
            }
            
            //会把 Panel 移到其父节点所有子物体的最后一个位置。
            panel.transform.SetAsLastSibling();
            panel.Open();

            if (_screenStack.Count > 0)
            {
                UIPanelId previousPanel = _screenStack.Peek();
                if (_uiPanels.TryGetValue(previousPanel, out PanelEntry panelEntry))
                {
                    panelEntry.Panel.Close();
                }
            }
            
            _screenStack.Push(panelId);
        }

        private void PopToScreen(UIPanelId targetPanelId)
        {
            if (!targetPanelId.IsValid)
                throw new ArgumentException();

            if (!_screenStack.Contains(targetPanelId))
                throw new InvalidOperationException($"Screen 栈中不存在 Panel {targetPanelId}。");

            while (!_screenStack.Peek().Equals(targetPanelId))
            {
                UIPanelId removePanelId = _screenStack.Peek();
                if (!_uiPanels.TryGetValue(removePanelId, out PanelEntry removeEntry))
                {
                    throw new InvalidOperationException($"Screen 栈中的 Panel {removePanelId} 不存在于缓存中。");
                }
                _screenStack.Pop();
                removeEntry.Panel.Close();
            }
        }
        
        //Modal 不关闭下面的 Screen，也可以暂时不关闭下面的 Modal，只把新弹窗放到最前面。
        private void OpenModalPanel(UIPanelId panelId, UIPanel panel)
        {
            if (_modalStack.Count > 0 &&
                _modalStack.Peek().Equals(panelId))
            {
                panel.Open();
                return;
            }

            panel.transform.SetAsLastSibling();
            panel.Open();

            _modalStack.Push(panelId);
        }
        
    }
}
