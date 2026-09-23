using System.Collections.Generic;
using Framework.UIFramework;
using UnityEngine;

namespace UI
{
    public class UITestController : MonoBehaviour
    {
        [SerializeField] private UIPanelRegistry panelRegistry;
        [SerializeField] private UILayerHost layerHost;
        [SerializeField] private GameObject testPanelAPrefab;
        [SerializeField] private GameObject testPanelBPrefab;
        private readonly UIPanelKey<UITestA> _testAKey =
            new(new UIPanelId("TestA"));

        private readonly UIPanelKey<UITestA> _testBKey =
            new(new UIPanelId("TestB"));

        private UIManager _uiManager;

        private void Awake()
        {
            Dictionary<UIPanelId, GameObject> prefabs = new()
            {
                {
                    new UIPanelId("TestA"),
                    testPanelAPrefab
                },
                {
                    new UIPanelId("TestB"),
                    testPanelBPrefab
                }
            };

            MemoryPanelAssetProvider assetProvider =
                new(prefabs);

            UIPanelFactory panelFactory =
                new();

            _uiManager = new UIManager(
                panelRegistry,
                panelFactory,
                layerHost,
                assetProvider);
        }

        public async void OpenA()
        {
            await _uiManager.OpenAsync(_testAKey);
        }

        public async void OpenB()
        {
            await _uiManager.OpenAsync(_testBKey);
        }

        public void Back()
        {
            bool handled = _uiManager.Back();
            Debug.Log($"UI Back handled: {handled}");
        }
    }
}
