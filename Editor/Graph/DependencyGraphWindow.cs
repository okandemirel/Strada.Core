using System;
using System.Collections.Generic;
using Strada.Core.DI;
using Strada.Core.Editor.DataProviders;
using Strada.Core.Editor.DataProviders.Models;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Strada.Core.Editor.Graph
{
    /// <summary>
    /// Editor window hosting the DependencyGraphView with toolbar, filtering, and cycle detection.
    /// Requirements: 2.1, 2.2, 2.5, 2.6
    /// </summary>
    public class DependencyGraphWindow : EditorWindow
    {
        private DependencyGraphView _graphView;
        private VisualElement _toolbar;
        private VisualElement _cycleWarningBanner;
        private VisualElement _statusBar;
        private DropdownField _lifetimeFilter;
        private TextField _searchField;
        private Label _statusLabel;
        private Label _nodeCountLabel;

        private float _refreshInterval = 1f;
        private double _lastRefreshTime;

        [MenuItem("Strada/Debugger/Dependency Graph", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<DependencyGraphWindow>();
            window.titleContent = new GUIContent("Dependency Graph", EditorGUIUtility.IconContent("d_SceneViewFx").image);
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

            // Create cycle warning banner (hidden by default)
            CreateCycleWarningBanner(root);

            // Create graph view
            _graphView = new DependencyGraphView();
            _graphView.style.flexGrow = 1;
            _graphView.OnNodeSelected += OnNodeSelected;
            _graphView.OnNodeHovered += OnNodeHovered;
            _graphView.OnGraphRefreshed += OnGraphRefreshed;
            root.Add(_graphView);

            // Create status bar
            CreateStatusBar(root);

            // Initial refresh
            RefreshGraph();
        }

        private void CreateToolbar(VisualElement root)
        {
            _toolbar = new VisualElement();
            _toolbar.AddToClassList("graph-toolbar");
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

            // Lifetime filter dropdown
            var filterLabel = new Label("Filter:");
            filterLabel.style.marginRight = 4;
            _toolbar.Add(filterLabel);

            _lifetimeFilter = new DropdownField(new List<string> 
            { 
                "All", 
                "Singleton", 
                "Transient", 
                "Scoped" 
            }, 0);
            _lifetimeFilter.style.minWidth = 100;
            _lifetimeFilter.style.marginRight = 16;
            _lifetimeFilter.RegisterValueChangedCallback(OnLifetimeFilterChanged);
            _toolbar.Add(_lifetimeFilter);

            // Frame All button
            var frameAllButton = new Button(() => _graphView?.FrameAll()) { text = "Frame All" };
            frameAllButton.style.marginRight = 8;
            _toolbar.Add(frameAllButton);

            // Clear Highlights button
            var clearHighlightsButton = new Button(() => _graphView?.ClearHighlights()) { text = "Clear Highlights" };
            clearHighlightsButton.style.marginRight = 8;
            _toolbar.Add(clearHighlightsButton);

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
            _cycleWarningBanner.AddToClassList("cycle-warning");
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

            var warningText = new Label("Circular Dependency Detected!");
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

        private void CreateStatusBar(VisualElement root)
        {
            _statusBar = new VisualElement();
            _statusBar.AddToClassList("status-bar");
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

            _nodeCountLabel = new Label("Nodes: 0 | Edges: 0");
            _nodeCountLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            _nodeCountLabel.style.fontSize = 11;

            _statusBar.Add(_statusLabel);
            _statusBar.Add(_nodeCountLabel);

            root.Add(_statusBar);
        }

        private void RefreshGraph()
        {
            var provider = ContainerDataProvider.Instance;
            
            if (!provider.IsAvailable)
            {
                _statusLabel.text = "Container not available - Enter Play Mode";
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

            // Update status
            _statusLabel.text = $"Last refresh: {DateTime.Now:HH:mm:ss}";
            
            // Update node count (we'd need to expose this from the graph view)
            var provider = ContainerDataProvider.Instance;
            if (provider.IsAvailable)
            {
                var graph = provider.BuildDependencyGraph();
                _nodeCountLabel.text = $"Nodes: {graph.Nodes.Count} | Edges: {graph.Edges.Count}";
            }
        }

        private void OnLifetimeFilterChanged(ChangeEvent<string> evt)
        {
            Lifetime? filter = evt.newValue switch
            {
                "Singleton" => Lifetime.Singleton,
                "Transient" => Lifetime.Transient,
                "Scoped" => Lifetime.Scoped,
                _ => null
            };

            _graphView.LifetimeFilter = filter;
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
                // Highlight all matching nodes
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
                : $"Found {results.Count} matching node(s)";
        }

        private void OnNodeSelected(ServiceNode node)
        {
            if (node != null)
            {
                _statusLabel.text = $"Selected: {node.ServiceType.Name} ({node.Lifetime})";
                _graphView.HighlightNodeDependencies(node);
            }
        }

        private void OnNodeHovered(ServiceNode node)
        {
            // Could show tooltip or update status
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                // Delay refresh to allow container initialization
                EditorApplication.delayCall += RefreshGraph;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                _statusLabel.text = "Exiting Play Mode...";
            }
        }

        private void Update()
        {
            // Auto-refresh during Play Mode
            if (Application.isPlaying && 
                EditorApplication.timeSinceStartup - _lastRefreshTime > _refreshInterval)
            {
                RefreshGraph();
            }
        }
    }
}
