using System;
using System.Collections.Generic;
using System.Linq;
using Strada.Core.Editor.DataProviders;
using Strada.Core.Editor.DataProviders.Models;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Strada.Core.Editor.Graph
{
    /// <summary>
    /// GraphView-based interactive visualization of module dependencies.
    /// Displays modules as nodes with priority badges and dependency edges.
    /// Requirements: 6.1, 6.2
    /// </summary>
    public class ModuleGraphView : GraphView
    {
        private const float NODE_WIDTH = 220f;
        private const float NODE_HEIGHT = 100f;
        private const float HORIZONTAL_SPACING = 280f;
        private const float VERTICAL_SPACING = 140f;

        private readonly Dictionary<Type, ModuleNode> _nodeMap = new Dictionary<Type, ModuleNode>();
        private readonly List<ModuleNode> _allNodes = new List<ModuleNode>();
        private readonly List<DependencyEdgeView> _allEdges = new List<DependencyEdgeView>();

        private MiniMap _miniMap;
        private bool _hasCycle;
        private List<Type> _cyclePath;
        private List<Type> _initializationOrder;

        public event Action<ModuleNode> OnNodeSelected;
        public event Action<ModuleNode> OnNodeHovered;
        public event Action OnGraphRefreshed;

        /// <summary>
        /// Gets whether the graph has a circular dependency.
        /// </summary>
        public bool HasCycle => _hasCycle;

        /// <summary>
        /// Gets the cycle path if a circular dependency exists.
        /// </summary>
        public IReadOnlyList<Type> CyclePath => _cyclePath;

        /// <summary>
        /// Gets the initialization order of modules.
        /// </summary>
        public IReadOnlyList<Type> InitializationOrder => _initializationOrder;

        public ModuleGraphView()
        {
            SetupGraphView();
            SetupManipulators();
            SetupMiniMap();
            SetupStyleSheet();
            RegisterCallbacks();
        }

        private void SetupGraphView()
        {
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);

            var gridBackground = new GridBackground();
            Insert(0, gridBackground);
            gridBackground.StretchToParentSize();

            style.flexGrow = 1;
            style.flexShrink = 1;
        }

        private void SetupManipulators()
        {
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new ContentZoomer());
        }

        private void SetupMiniMap()
        {
            _miniMap = new MiniMap { anchored = true };
            _miniMap.SetPosition(new Rect(10, 30, 200, 140));
            Add(_miniMap);
        }

        private void SetupStyleSheet()
        {
            styleSheets.Add(Resources.Load<StyleSheet>("DependencyGraphStyles"));
        }

        private void RegisterCallbacks()
        {
            graphViewChanged += OnGraphViewChanged;
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (change.movedElements != null)
            {
                foreach (var element in change.movedElements)
                {
                    if (element is ModuleNode node)
                    {
                        OnNodeSelected?.Invoke(node);
                    }
                }
            }
            return change;
        }

        /// <summary>
        /// Populates the graph with data from the module data provider.
        /// </summary>
        public void PopulateFromProvider()
        {
            var provider = ModuleDataProvider.Instance;
            if (!provider.IsAvailable)
            {
                ShowNotAvailableMessage();
                return;
            }

            var modules = provider.GetModules();
            var graph = provider.BuildModuleGraph();
            PopulateFromModules(modules, graph);
        }

        /// <summary>
        /// Populates the graph from module data.
        /// </summary>
        public void PopulateFromModules(IReadOnlyList<ModuleInfoData> modules, DependencyGraph graph)
        {
            ClearGraph();

            if (modules == null || modules.Count == 0)
            {
                ShowEmptyMessage();
                return;
            }

            _hasCycle = graph?.HasCycle ?? false;
            _cyclePath = graph?.CyclePath;

            // Create nodes
            foreach (var moduleInfo in modules)
            {
                var node = CreateModuleNode(moduleInfo);
                _nodeMap[moduleInfo.ModuleType] = node;
                _allNodes.Add(node);
                AddElement(node);
            }

            // Create edges from graph
            if (graph != null)
            {
                foreach (var edgeData in graph.Edges)
                {
                    if (_nodeMap.TryGetValue(edgeData.Source, out var sourceNode) &&
                        _nodeMap.TryGetValue(edgeData.Target, out var targetNode))
                    {
                        var edge = CreateEdge(sourceNode, targetNode, edgeData.IsCircular);
                        _allEdges.Add(edge);
                        AddElement(edge);
                    }
                }
            }

            // Compute initialization order
            ComputeInitializationOrder(modules);

            // Layout nodes by initialization order
            LayoutNodesByInitOrder();

            OnGraphRefreshed?.Invoke();
        }

        private ModuleNode CreateModuleNode(ModuleInfoData moduleInfo)
        {
            var node = new ModuleNode(moduleInfo);

            node.RegisterCallback<MouseEnterEvent>(evt => OnNodeHovered?.Invoke(node));
            node.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.clickCount == 1)
                {
                    OnNodeSelected?.Invoke(node);
                }
            });

            return node;
        }

        private DependencyEdgeView CreateEdge(ModuleNode source, ModuleNode target, bool isCircular)
        {
            var edge = new DependencyEdgeView
            {
                output = source.OutputPort,
                input = target.InputPort,
                IsCircular = isCircular
            };

            source.OutputPort.Connect(edge);
            target.InputPort.Connect(edge);

            return edge;
        }

        private void ComputeInitializationOrder(IReadOnlyList<ModuleInfoData> modules)
        {
            _initializationOrder = new List<Type>();
            var visited = new HashSet<Type>();
            var sorted = new List<ModuleInfoData>();

            // Topological sort by dependencies and priority
            var remaining = new List<ModuleInfoData>(modules);
            
            while (remaining.Count > 0)
            {
                // Find modules with all dependencies satisfied
                var ready = remaining
                    .Where(m => m.Dependencies.All(d => visited.Contains(d) || !_nodeMap.ContainsKey(d)))
                    .OrderBy(m => m.Priority)
                    .ToList();

                if (ready.Count == 0)
                {
                    // Circular dependency - just add remaining in priority order
                    ready = remaining.OrderBy(m => m.Priority).ToList();
                }

                foreach (var module in ready)
                {
                    visited.Add(module.ModuleType);
                    sorted.Add(module);
                    remaining.Remove(module);
                    _initializationOrder.Add(module.ModuleType);
                }
            }
        }

        private void LayoutNodesByInitOrder()
        {
            if (_initializationOrder == null || _initializationOrder.Count == 0) return;

            // Layout in columns by initialization order
            var nodesPerColumn = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(_allNodes.Count)));
            
            for (int i = 0; i < _initializationOrder.Count; i++)
            {
                var type = _initializationOrder[i];
                if (_nodeMap.TryGetValue(type, out var node))
                {
                    var column = i / nodesPerColumn;
                    var row = i % nodesPerColumn;

                    var x = column * HORIZONTAL_SPACING + 50;
                    var y = row * VERTICAL_SPACING + 50;
                    node.SetPosition(new Rect(x, y, NODE_WIDTH, NODE_HEIGHT));

                    // Add initialization order label
                    var orderLabel = new Label($"#{i + 1}")
                    {
                        style =
                        {
                            position = Position.Absolute,
                            top = -20,
                            left = 0,
                            fontSize = 10,
                            color = new Color(0.5f, 0.5f, 0.5f)
                        }
                    };
                    node.Add(orderLabel);
                }
            }

            FrameAll();
        }

        /// <summary>
        /// Highlights a node and its dependencies/dependents.
        /// </summary>
        public void HighlightNodeDependencies(ModuleNode node)
        {
            foreach (var n in _allNodes)
            {
                n.RemoveFromClassList("highlighted");
                n.RemoveFromClassList("dependency");
                n.RemoveFromClassList("dependent");
            }

            foreach (var e in _allEdges)
            {
                e.RemoveFromClassList("highlighted");
            }

            if (node == null) return;

            node.AddToClassList("highlighted");

            // Highlight dependencies
            foreach (var edge in node.OutputPort.connections)
            {
                if (edge.input?.node is ModuleNode depNode)
                {
                    depNode.AddToClassList("dependency");
                    edge.AddToClassList("highlighted");
                }
            }

            // Highlight dependents
            foreach (var edge in node.InputPort.connections)
            {
                if (edge.output?.node is ModuleNode depNode)
                {
                    depNode.AddToClassList("dependent");
                    edge.AddToClassList("highlighted");
                }
            }
        }

        /// <summary>
        /// Clears all highlights from the graph.
        /// </summary>
        public void ClearHighlights()
        {
            HighlightNodeDependencies(null);
        }

        /// <summary>
        /// Searches for nodes matching the query.
        /// </summary>
        public List<ModuleNode> SearchNodes(string query)
        {
            var results = new List<ModuleNode>();
            if (string.IsNullOrWhiteSpace(query)) return results;

            var lowerQuery = query.ToLowerInvariant();
            foreach (var node in _allNodes)
            {
                var moduleName = node.ModuleName.ToLowerInvariant();
                var typeName = node.ModuleType.Name.ToLowerInvariant();

                if (moduleName.Contains(lowerQuery) || typeName.Contains(lowerQuery))
                {
                    results.Add(node);
                }
            }

            return results;
        }

        /// <summary>
        /// Focuses the view on a specific node.
        /// </summary>
        public void FocusOnNode(ModuleNode node)
        {
            if (node == null) return;

            ClearSelection();
            AddToSelection(node);
            FrameSelection();
        }

        private void ClearGraph()
        {
            foreach (var edge in _allEdges)
            {
                RemoveElement(edge);
            }
            foreach (var node in _allNodes)
            {
                RemoveElement(node);
            }

            _nodeMap.Clear();
            _allNodes.Clear();
            _allEdges.Clear();
            _hasCycle = false;
            _cyclePath = null;
            _initializationOrder = null;
        }

        private void ShowNotAvailableMessage()
        {
            ClearGraph();
            var label = new Label("Module registry not available. Enter Play Mode to view modules.")
            {
                style =
                {
                    position = Position.Absolute,
                    left = 20,
                    top = 20,
                    fontSize = 14,
                    color = new Color(0.7f, 0.7f, 0.7f)
                }
            };
            Add(label);
        }

        private void ShowEmptyMessage()
        {
            var label = new Label("No modules found in registry.")
            {
                style =
                {
                    position = Position.Absolute,
                    left = 20,
                    top = 20,
                    fontSize = 14,
                    color = new Color(0.7f, 0.7f, 0.7f)
                }
            };
            Add(label);
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return ports.ToList().Where(p =>
                p.direction != startPort.direction &&
                p.node != startPort.node).ToList();
        }
    }
}
