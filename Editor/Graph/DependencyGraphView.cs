using System;
using System.Collections.Generic;
using System.Linq;
using Strada.Core.DI;
using Strada.Core.Editor.DataProviders;
using Strada.Core.Editor.DataProviders.Models;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Strada.Core.Editor.Graph
{
    /// <summary>
    /// GraphView-based interactive visualization of DI container dependencies.
    /// Provides zoom, pan, minimap, and node selection capabilities.
    /// Requirements: 2.1, 2.5
    /// </summary>
    public class DependencyGraphView : GraphView
    {
        private const float NODE_WIDTH = 220f;
        private const float NODE_HEIGHT = 80f;
        private const float HORIZONTAL_SPACING = 280f;
        private const float VERTICAL_SPACING = 120f;

        private readonly Dictionary<Type, ServiceNode> _nodeMap = new Dictionary<Type, ServiceNode>();
        private readonly List<ServiceNode> _allNodes = new List<ServiceNode>();
        private readonly List<DependencyEdgeView> _allEdges = new List<DependencyEdgeView>();
        
        private MiniMap _miniMap;
        private Lifetime? _lifetimeFilter;
        private bool _hasCycle;
        private List<Type> _cyclePath;
        
        public event Action<ServiceNode> OnNodeSelected;
        public event Action<ServiceNode> OnNodeHovered;
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
        /// Gets or sets the lifetime filter for displaying nodes.
        /// </summary>
        public Lifetime? LifetimeFilter
        {
            get => _lifetimeFilter;
            set
            {
                if (_lifetimeFilter != value)
                {
                    _lifetimeFilter = value;
                    ApplyLifetimeFilter();
                }
            }
        }

        public DependencyGraphView()
        {
            SetupGraphView();
            SetupManipulators();
            SetupMiniMap();
            SetupStyleSheet();
            RegisterCallbacks();
        }

        private void SetupGraphView()
        {
            // Enable zoom and pan
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            
            // Set background
            var gridBackground = new GridBackground();
            Insert(0, gridBackground);
            gridBackground.StretchToParentSize();

            // Set default size
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
            _miniMap = new MiniMap
            {
                anchored = true
            };
            _miniMap.SetPosition(new Rect(10, 30, 200, 140));
            Add(_miniMap);
        }

        private void SetupStyleSheet()
        {
            // Apply custom styles
            styleSheets.Add(Resources.Load<StyleSheet>("DependencyGraphStyles"));
        }

        private void RegisterCallbacks()
        {
            // Handle selection changes
            graphViewChanged += OnGraphViewChanged;
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            // Handle node selection
            if (change.movedElements != null)
            {
                foreach (var element in change.movedElements)
                {
                    if (element is ServiceNode node)
                    {
                        OnNodeSelected?.Invoke(node);
                    }
                }
            }
            return change;
        }

        /// <summary>
        /// Populates the graph with data from the container data provider.
        /// </summary>
        public void PopulateFromProvider()
        {
            var provider = ContainerDataProvider.Instance;
            if (!provider.IsAvailable)
            {
                ShowNotAvailableMessage();
                return;
            }

            var graph = provider.BuildDependencyGraph();
            PopulateFromGraph(graph);
        }

        /// <summary>
        /// Populates the graph from a DependencyGraph data model.
        /// </summary>
        public void PopulateFromGraph(DependencyGraph graph)
        {
            ClearGraph();

            if (graph == null || graph.Nodes.Count == 0)
            {
                ShowEmptyMessage();
                return;
            }

            _hasCycle = graph.HasCycle;
            _cyclePath = graph.CyclePath;

            // Create nodes
            foreach (var nodeData in graph.Nodes)
            {
                var node = CreateServiceNode(nodeData);
                _nodeMap[nodeData.ServiceType] = node;
                _allNodes.Add(node);
                AddElement(node);
            }

            // Create edges
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

            // Layout nodes
            LayoutNodes();

            // Apply current filter
            ApplyLifetimeFilter();

            OnGraphRefreshed?.Invoke();
        }

        private ServiceNode CreateServiceNode(DependencyNode nodeData)
        {
            var node = new ServiceNode(nodeData.ServiceType, nodeData.ImplementationType, nodeData.Lifetime);
            
            // Setup hover callback
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

        private DependencyEdgeView CreateEdge(ServiceNode source, ServiceNode target, bool isCircular)
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

        private void LayoutNodes()
        {
            if (_allNodes.Count == 0) return;

            // Use topological sort for layout
            var levels = ComputeNodeLevels();
            var levelCounts = new Dictionary<int, int>();

            foreach (var node in _allNodes)
            {
                var level = levels.TryGetValue(node.ServiceType, out var l) ? l : 0;
                var countInLevel = levelCounts.TryGetValue(level, out var c) ? c : 0;
                levelCounts[level] = countInLevel + 1;

                var x = level * HORIZONTAL_SPACING + 50;
                var y = countInLevel * VERTICAL_SPACING + 50;
                node.SetPosition(new Rect(x, y, NODE_WIDTH, NODE_HEIGHT));
            }

            // Frame all content
            FrameAll();
        }

        private Dictionary<Type, int> ComputeNodeLevels()
        {
            var levels = new Dictionary<Type, int>();
            var visited = new HashSet<Type>();

            // Find root nodes (no incoming edges)
            var hasIncoming = new HashSet<Type>();
            foreach (var edge in _allEdges)
            {
                if (edge.input?.node is ServiceNode targetNode)
                {
                    hasIncoming.Add(targetNode.ServiceType);
                }
            }

            var roots = _allNodes.Where(n => !hasIncoming.Contains(n.ServiceType)).ToList();
            if (roots.Count == 0 && _allNodes.Count > 0)
            {
                roots.Add(_allNodes[0]); // Fallback if all nodes have incoming edges (cycle)
            }

            // BFS to assign levels
            var queue = new Queue<(Type type, int level)>();
            foreach (var root in roots)
            {
                queue.Enqueue((root.ServiceType, 0));
            }

            while (queue.Count > 0)
            {
                var (type, level) = queue.Dequeue();
                if (visited.Contains(type)) continue;
                visited.Add(type);

                levels[type] = Math.Max(levels.TryGetValue(type, out var existing) ? existing : 0, level);

                if (_nodeMap.TryGetValue(type, out var node))
                {
                    foreach (var edge in node.OutputPort.connections)
                    {
                        if (edge.input?.node is ServiceNode targetNode && !visited.Contains(targetNode.ServiceType))
                        {
                            queue.Enqueue((targetNode.ServiceType, level + 1));
                        }
                    }
                }
            }

            return levels;
        }

        private void ApplyLifetimeFilter()
        {
            foreach (var node in _allNodes)
            {
                var visible = !_lifetimeFilter.HasValue || node.Lifetime == _lifetimeFilter.Value;
                node.visible = visible;
            }

            foreach (var edge in _allEdges)
            {
                var sourceVisible = edge.output?.node?.visible ?? false;
                var targetVisible = edge.input?.node?.visible ?? false;
                edge.visible = sourceVisible && targetVisible;
            }
        }

        /// <summary>
        /// Highlights a node and its dependencies/dependents.
        /// </summary>
        public void HighlightNodeDependencies(ServiceNode node)
        {
            // Reset all highlights
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

            // Highlight dependencies (what this node depends on)
            foreach (var edge in node.OutputPort.connections)
            {
                if (edge.input?.node is ServiceNode depNode)
                {
                    depNode.AddToClassList("dependency");
                    edge.AddToClassList("highlighted");
                }
            }

            // Highlight dependents (what depends on this node)
            foreach (var edge in node.InputPort.connections)
            {
                if (edge.output?.node is ServiceNode depNode)
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
        /// Searches for nodes matching the query and highlights them.
        /// </summary>
        public List<ServiceNode> SearchNodes(string query)
        {
            var results = new List<ServiceNode>();
            if (string.IsNullOrWhiteSpace(query)) return results;

            var lowerQuery = query.ToLowerInvariant();
            foreach (var node in _allNodes)
            {
                var typeName = node.ServiceType.Name.ToLowerInvariant();
                var fullName = node.ServiceType.FullName?.ToLowerInvariant() ?? "";
                
                if (typeName.Contains(lowerQuery) || fullName.Contains(lowerQuery))
                {
                    results.Add(node);
                }
            }

            return results;
        }

        /// <summary>
        /// Focuses the view on a specific node.
        /// </summary>
        public void FocusOnNode(ServiceNode node)
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
        }

        private void ShowNotAvailableMessage()
        {
            ClearGraph();
            var label = new Label("Container not available. Enter Play Mode to view dependencies.")
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
            var label = new Label("No registrations found in container.")
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
