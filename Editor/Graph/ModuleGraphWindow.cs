using System;
using Strada.Core.Editor.DataProviders;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Strada.Core.Editor.Graph
{
    /// <summary>
    /// Editor window hosting the ModuleGraphView with toolbar and validation.
    /// Requirements: 6.1, 6.2, 6.3, 6.4
    /// </summary>
    public class ModuleGraphWindow : EditorWindow
    {
        private ModuleGraphView _graphView;
        private VisualElement _cycleWarningBanner;
        private VisualElement _initOrderPanel;
        private Label _statusLabel;
        private Label _moduleCountLabel;

        private float _refreshInterval = 1f;
        private double _lastRefreshTime;

        public static void ShowWindow()
        {
            var window = GetWindow<ModuleGraphWindow>();
            window.titleContent = new GUIContent("Module Graph", EditorGUIUtility.IconContent("d_Prefab Icon").image);
            window.minSize = new Vector2(600, 400);
        }

        private void OnEnable()
        {
            CreateUI();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void CreateUI()
        {
            var root = rootVisualElement;
            root.Clear();

            CreateToolbar(root);

            _cycleWarningBanner = GraphWindowHelper.CreateCycleWarningBanner("Circular Module Dependency Detected!");
            root.Add(_cycleWarningBanner);

            var mainContent = new VisualElement();
            mainContent.style.flexDirection = FlexDirection.Row;
            mainContent.style.flexGrow = 1;

            _graphView = new ModuleGraphView();
            _graphView.style.flexGrow = 1;
            _graphView.OnNodeSelected += OnNodeSelected;
            _graphView.OnGraphRefreshed += OnGraphRefreshed;
            mainContent.Add(_graphView);

            CreateInitOrderPanel(mainContent);

            root.Add(mainContent);

            var (statusBar, statusLabel, countLabel) = GraphWindowHelper.CreateStatusBar("Modules: 0");
            _statusLabel = statusLabel;
            _moduleCountLabel = countLabel;
            root.Add(statusBar);

            RefreshGraph();
        }

        private void CreateToolbar(VisualElement root)
        {
            var toolbar = GraphWindowHelper.CreateToolbarBase();

            toolbar.Add(GraphWindowHelper.CreateToolbarButton("Refresh", RefreshGraph));
            toolbar.Add(GraphWindowHelper.CreateToolbarButton("Validate All", ValidateModules));
            toolbar.Add(GraphWindowHelper.CreateToolbarButton("Frame All", () => _graphView?.FrameAll()));
            toolbar.Add(GraphWindowHelper.CreateToolbarButton("Clear Highlights", () => _graphView?.ClearHighlights()));
            toolbar.Add(GraphWindowHelper.CreateToolbarButton("Toggle Init Order", ToggleInitOrderPanel));
            toolbar.Add(GraphWindowHelper.CreateToolbarSpacer());

            var (searchContainer, searchField) = GraphWindowHelper.CreateSearchSection();
            searchField.RegisterValueChangedCallback(OnSearchChanged);
            toolbar.Add(searchContainer);

            root.Add(toolbar);
        }

        private void CreateInitOrderPanel(VisualElement parent)
        {
            _initOrderPanel = new VisualElement();
            _initOrderPanel.style.width = 200;
            _initOrderPanel.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            _initOrderPanel.style.borderLeftWidth = 1;
            _initOrderPanel.style.borderLeftColor = new Color(0.1f, 0.1f, 0.1f);
            _initOrderPanel.style.display = DisplayStyle.None;

            var header = new Label("Initialization Order");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.paddingLeft = 8;
            header.style.paddingTop = 8;
            header.style.paddingBottom = 8;
            header.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            _initOrderPanel.Add(header);

            var scrollView = new ScrollView();
            scrollView.name = "init-order-list";
            scrollView.style.flexGrow = 1;
            _initOrderPanel.Add(scrollView);

            parent.Add(_initOrderPanel);
        }

        private void RefreshGraph()
        {
            var provider = ModuleDataProvider.Instance;

            if (!provider.IsAvailable)
            {
                _statusLabel.text = "Module registry not available - Enter Play Mode";
                _cycleWarningBanner.style.display = DisplayStyle.None;
                return;
            }

            _graphView.PopulateFromProvider();
            _lastRefreshTime = EditorApplication.timeSinceStartup;
        }

        private void OnGraphRefreshed()
        {
            if (_graphView.HasCycle && _graphView.CyclePath != null)
            {
                _cycleWarningBanner.style.display = DisplayStyle.Flex;
                var pathLabel = _cycleWarningBanner.Q<Label>("cycle-path-label");
                if (pathLabel != null)
                {
                    var pathStr = string.Join(" \u2192 ",
                        System.Linq.Enumerable.Select(_graphView.CyclePath, t => t.Name));
                    pathLabel.text = $"Cycle: {pathStr}";
                }
            }
            else
            {
                _cycleWarningBanner.style.display = DisplayStyle.None;
            }

            UpdateInitOrderPanel();

            _statusLabel.text = $"Last refresh: {DateTime.Now:HH:mm:ss}";

            var provider = ModuleDataProvider.Instance;
            if (provider.IsAvailable)
            {
                var modules = provider.GetModules();
                _moduleCountLabel.text = $"Modules: {modules.Count}";
            }
        }

        private void UpdateInitOrderPanel()
        {
            var scrollView = _initOrderPanel.Q<ScrollView>("init-order-list");
            if (scrollView == null) return;

            scrollView.Clear();

            var initOrder = _graphView.InitializationOrder;
            if (initOrder == null) return;

            for (int i = 0; i < initOrder.Count; i++)
            {
                var type = initOrder[i];
                var item = new Label($"{i + 1}. {type.Name}");
                item.style.paddingLeft = 8;
                item.style.paddingTop = 4;
                item.style.paddingBottom = 4;
                item.style.fontSize = 11;
                item.style.color = new Color(0.8f, 0.8f, 0.8f);

                if (i % 2 == 1)
                {
                    item.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
                }

                scrollView.Add(item);
            }
        }

        private void ValidateModules()
        {
            var provider = ModuleDataProvider.Instance;
            if (!provider.IsAvailable)
            {
                _statusLabel.text = "Cannot validate - Enter Play Mode first";
                return;
            }

            var result = provider.ValidateModules();
            if (result.IsValid)
            {
                _statusLabel.text = "\u2713 All modules validated successfully";
                EditorUtility.DisplayDialog("Module Validation",
                    "All modules passed validation.", "OK");
            }
            else
            {
                var issues = string.Join("\n",
                    System.Linq.Enumerable.Select(result.Issues, i => $"\u2022 {i.Message}"));
                _statusLabel.text = $"\u2717 Validation failed: {result.Issues.Count} issue(s)";
                EditorUtility.DisplayDialog("Module Validation Failed",
                    $"Found {result.Issues.Count} issue(s):\n\n{issues}", "OK");
            }
        }

        private void ToggleInitOrderPanel()
        {
            _initOrderPanel.style.display = _initOrderPanel.style.display == DisplayStyle.None
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private void OnSearchChanged(ChangeEvent<string> evt)
        {
            var results = _graphView.SearchNodes(evt.newValue);

            if (results.Count == 1)
            {
                _graphView.FocusOnNode(results[0]);
                _graphView.HighlightNodeDependencies(results[0]);
            }
            else if (results.Count > 1)
            {
                _graphView.ClearHighlights();
                foreach (var node in results)
                {
                    node.AddToClassList("highlighted");
                }
            }
            else
            {
                _graphView.ClearHighlights();
            }

            _statusLabel.text = string.IsNullOrEmpty(evt.newValue)
                ? "Ready"
                : $"Found {results.Count} matching module(s)";
        }

        private void OnNodeSelected(ModuleNode node)
        {
            if (node != null)
            {
                _statusLabel.text = $"Selected: {node.ModuleName} (Priority: {node.Priority})";
                _graphView.HighlightNodeDependencies(node);
            }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                EditorApplication.delayCall += RefreshGraph;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                _statusLabel.text = "Exiting Play Mode...";
            }
        }

        private void Update()
        {
            if (Application.isPlaying &&
                EditorApplication.timeSinceStartup - _lastRefreshTime > _refreshInterval)
            {
                RefreshGraph();
            }
        }
    }
}
