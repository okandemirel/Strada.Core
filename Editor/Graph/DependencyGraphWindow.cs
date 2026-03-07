using System;
using System.Collections.Generic;
using Strada.Core.DI;
using Strada.Core.Editor.DataProviders;
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
        private VisualElement _cycleWarningBanner;
        private DropdownField _lifetimeFilter;
        private Label _statusLabel;
        private Label _nodeCountLabel;

        private float _refreshInterval = 1f;
        private double _lastRefreshTime;

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

            CreateToolbar(root);

            _cycleWarningBanner = GraphWindowHelper.CreateCycleWarningBanner("Circular Dependency Detected!");
            root.Add(_cycleWarningBanner);

            _graphView = new DependencyGraphView();
            _graphView.style.flexGrow = 1;
            _graphView.OnNodeSelected += OnNodeSelected;
            _graphView.OnGraphRefreshed += OnGraphRefreshed;
            root.Add(_graphView);

            var (statusBar, statusLabel, countLabel) = GraphWindowHelper.CreateStatusBar("Nodes: 0 | Edges: 0");
            _statusLabel = statusLabel;
            _nodeCountLabel = countLabel;
            root.Add(statusBar);

            RefreshGraph();
        }

        private void CreateToolbar(VisualElement root)
        {
            var toolbar = GraphWindowHelper.CreateToolbarBase();

            toolbar.Add(GraphWindowHelper.CreateToolbarButton("Refresh", RefreshGraph));

            var filterLabel = new Label("Filter:");
            filterLabel.style.marginRight = 4;
            toolbar.Add(filterLabel);

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
            toolbar.Add(_lifetimeFilter);

            toolbar.Add(GraphWindowHelper.CreateToolbarButton("Frame All", () => _graphView?.FrameAll()));
            toolbar.Add(GraphWindowHelper.CreateToolbarButton("Clear Highlights", () => _graphView?.ClearHighlights()));
            toolbar.Add(GraphWindowHelper.CreateToolbarSpacer());

            var (searchContainer, searchField) = GraphWindowHelper.CreateSearchSection();
            searchField.RegisterValueChangedCallback(OnSearchChanged);
            toolbar.Add(searchContainer);

            root.Add(toolbar);
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

            _statusLabel.text = $"Last refresh: {DateTime.Now:HH:mm:ss}";

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
