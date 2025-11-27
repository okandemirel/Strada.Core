using System;
using System.Collections.Generic;
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
        private VisualElement _toolbar;
        private VisualElement _cycleWarningBanner;
        private VisualElement _statusBar;
        private VisualElement _initOrderPanel;
        private TextField _searchField;
        private Label _statusLabel;
        private Label _moduleCountLabel;

        private float _refreshInterval = 1f;
        private double _lastRefreshTime;

        [MenuItem("Strada/Debugger/Module Graph", false, 101)]
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

            // Create toolbar
            CreateToolbar(root);

            // Create cycle warning banner
            CreateCycleWarningBanner(root);

            // Main content area
            var mainContent = new VisualElement();
            mainContent.style.flexDirection = FlexDirection.Row;
            mainContent.style.flexGrow = 1;

            // Create graph view
            _graphView = new ModuleGraphView();
            _graphView.style.flexGrow = 1;
            _graphView.OnNodeSelected += OnNodeSelected;
            _graphView.OnNodeHovered += OnNodeHovered;
            _graphView.OnGraphRefreshed += OnGraphRefreshed;
            mainContent.Add(_graphView);

            // Create initialization order panel
            CreateInitOrderPanel(mainContent);

            root.Add(mainContent);

            // Create status bar
            CreateStatusBar(root);

            // Initial refresh
            RefreshGraph();
        }

        private void CreateToolbar(VisualElement root)
        {
            _toolbar = new VisualElement();
            _toolbar.style.flexDirection = FlexDirection.Row;
            _toolbar.style.alignItems = Align.Center;
            _toolbar.style.paddingLeft = 8;
            _toolbar.style.paddingRight = 8;
            _toolbar.style.paddingTop = 4;
            _toolbar.style.paddingBottom = 4;
            _toolbar.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
            _toolbar.style.borderBottomWidth = 1;
            _toolbar.style.borderBottomColor = new Color(0.1f, 0.1f, 0.1f);

            // Refresh button
            var refreshButton = new Button(RefreshGraph) { text = "Refresh" };
            refreshButton.style.marginRight = 8;
            _toolbar.Add(refreshButton);

            // Validate All button
            var validateButton = new Button(ValidateModules) { text = "Validate All" };
            validateButton.style.marginRight = 8;
            _toolbar.Add(validateButton);

            // Frame All button
            var frameAllButton = new Button(() => _graphView?.FrameAll()) { text = "Frame All" };
            frameAllButton.style.marginRight = 8;
            _toolbar.Add(frameAllButton);

            // Clear Highlights button
            var clearHighlightsButton = new Button(() => _graphView?.ClearHighlights()) { text = "Clear Highlights" };
            clearHighlightsButton.style.marginRight = 8;
            _toolbar.Add(clearHighlightsButton);

            // Toggle Init Order Panel
            var toggleInitOrderButton = new Button(ToggleInitOrderPanel) { text = "Toggle Init Order" };
            toggleInitOrderButton.style.marginRight = 8;
            _toolbar.Add(toggleInitOrderButton);

            // Spacer
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            _toolbar.Add(spacer);

            // Search field
            var searchContainer = new VisualElement();
            searchContainer.style.flexDirection = FlexDirection.Row;
            searchContainer.style.alignItems = Align.Center;

            var searchLabel = new Label("Search:");
            searchLabel.style.marginRight = 4;
            searchContainer.Add(searchLabel);

            _searchField = new TextField();
            _searchField.style.minWidth = 180;
            _searchField.RegisterValueChangedCallback(OnSearchChanged);
            searchContainer.Add(_searchField);

            _toolbar.Add(searchContainer);

            root.Add(_toolbar);
        }

        private void CreateCycleWarningBanner(VisualElement root)
        {
            _cycleWarningBanner = new VisualElement();
            _cycleWarningBanner.style.backgroundColor = new Color(0.3f, 0.15f, 0.15f);
            _cycleWarningBanner.style.borderTopWidth = 2;
            _cycleWarningBanner.style.borderBottomWidth = 2;
            _cycleWarningBanner.style.borderLeftWidth = 2;
            _cycleWarningBanner.style.borderRightWidth = 2;
            _cycleWarningBanner.style.borderTopColor = new Color(0.8f, 0.2f, 0.2f);
            _cycleWarningBanner.style.borderBottomColor = new Color(0.8f, 0.2f, 0.2f);
            _cycleWarningBanner.style.borderLeftColor = new Color(0.8f, 0.2f, 0.2f);
            _cycleWarningBanner.style.borderRightColor = new Color(0.8f, 0.2f, 0.2f);
            _cycleWarningBanner.style.paddingLeft = 12;
            _cycleWarningBanner.style.paddingRight = 12;
            _cycleWarningBanner.style.paddingTop = 8;
            _cycleWarningBanner.style.paddingBottom = 8;
            _cycleWarningBanner.style.display = DisplayStyle.None;

            var warningIcon = new Label("⚠");
            warningIcon.style.fontSize = 16;
            warningIcon.style.color = new Color(1f, 0.4f, 0.4f);
            warningIcon.style.marginRight = 8;

            var warningText = new Label("Circular Module Dependency Detected!");
            warningText.name = "cycle-warning-text";
            warningText.style.color = new Color(1f, 0.6f, 0.6f);
            warningText.style.unityFontStyleAndWeight = FontStyle.Bold;

            var cyclePathLabel = new Label();
            cyclePathLabel.name = "cycle-path-label";
            cyclePathLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
            cyclePathLabel.style.marginTop = 4;
            cyclePathLabel.style.fontSize = 11;

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.Add(warningIcon);
            headerRow.Add(warningText);

            _cycleWarningBanner.Add(headerRow);
            _cycleWarningBanner.Add(cyclePathLabel);

            root.Add(_cycleWarningBanner);
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

        private void CreateStatusBar(VisualElement root)
        {
            _statusBar = new VisualElement();
            _statusBar.style.flexDirection = FlexDirection.Row;
            _statusBar.style.justifyContent = Justify.SpaceBetween;
            _statusBar.style.paddingLeft = 8;
            _statusBar.style.paddingRight = 8;
            _statusBar.style.paddingTop = 4;
            _statusBar.style.paddingBottom = 4;
            _statusBar.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            _statusBar.style.borderTopWidth = 1;
            _statusBar.style.borderTopColor = new Color(0.1f, 0.1f, 0.1f);

            _statusLabel = new Label("Ready");
            _statusLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            _statusLabel.style.fontSize = 11;

            _moduleCountLabel = new Label("Modules: 0");
            _moduleCountLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            _moduleCountLabel.style.fontSize = 11;

            _statusBar.Add(_statusLabel);
            _statusBar.Add(_moduleCountLabel);

            root.Add(_statusBar);
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
            // Update cycle warning
            if (_graphView.HasCycle && _graphView.CyclePath != null)
            {
                _cycleWarningBanner.style.display = DisplayStyle.Flex;
                var pathLabel = _cycleWarningBanner.Q<Label>("cycle-path-label");
                if (pathLabel != null)
                {
                    var pathStr = string.Join(" → ",
                        System.Linq.Enumerable.Select(_graphView.CyclePath, t => t.Name));
                    pathLabel.text = $"Cycle: {pathStr}";
                }
            }
            else
            {
                _cycleWarningBanner.style.display = DisplayStyle.None;
            }

            // Update initialization order panel
            UpdateInitOrderPanel();

            // Update status
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

                // Alternate background
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
                _statusLabel.text = "✓ All modules validated successfully";
                EditorUtility.DisplayDialog("Module Validation", 
                    "All modules passed validation.", "OK");
            }
            else
            {
                var issues = string.Join("\n", 
                    System.Linq.Enumerable.Select(result.Issues, i => $"• {i.Message}"));
                _statusLabel.text = $"✗ Validation failed: {result.Issues.Count} issue(s)";
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

        private void OnNodeHovered(ModuleNode node)
        {
            // Could show tooltip or update status
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
