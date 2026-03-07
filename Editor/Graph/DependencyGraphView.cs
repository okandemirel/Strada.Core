using System;
using System.Collections.Generic;
using System.Linq;
using Strada.Core.DI;
using Strada.Core.Editor.DataProviders;
using Strada.Core.Editor.DataProviders.Models;
using UnityEngine;

namespace Strada.Core.Editor.Graph
{
    /// <summary>
    /// GraphView-based interactive visualization of DI container dependencies.
    /// Provides zoom, pan, minimap, and node selection capabilities.
    /// Requirements: 2.1, 2.5
    /// </summary>
    public class DependencyGraphView : BaseGraphView<ServiceNode>
    {
        private const float NODE_WIDTH = 220f;
        private const float NODE_HEIGHT = 80f;
        private const float HORIZONTAL_SPACING = 280f;
        private const float VERTICAL_SPACING = 120f;

        private Lifetime? _lifetimeFilter;

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

        /// <summary>
        /// Populates the graph with data from the container data provider.
        /// </summary>
        public void PopulateFromProvider()
        {
            var provider = ContainerDataProvider.Instance;
            if (!provider.IsAvailable)
            {
                ClearGraph();
                ShowMessage("Container not available. Enter Play Mode to view dependencies.");
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
                ShowMessage("No registrations found in container.");
                return;
            }

            SetCycleInfo(graph.HasCycle, graph.CyclePath);

            foreach (var nodeData in graph.Nodes)
            {
                var node = new ServiceNode(nodeData.ServiceType, nodeData.ImplementationType, nodeData.Lifetime);
                RegisterNodeCallbacks(node);
                AddNodeToGraph(nodeData.ServiceType, node);
            }

            foreach (var edgeData in graph.Edges)
            {
                if (NodeMap.TryGetValue(edgeData.Source, out var sourceNode) &&
                    NodeMap.TryGetValue(edgeData.Target, out var targetNode))
                {
                    CreateEdge(sourceNode.OutputPort, targetNode.InputPort, edgeData.IsCircular);
                }
            }

            LayoutNodes();
            ApplyLifetimeFilter();
            InvokeGraphRefreshed();
        }

        public void HighlightNodeDependencies(ServiceNode node)
        {
            HighlightNodeDependencies(node, node?.InputPort, node?.OutputPort);
        }

        public void ClearHighlights()
        {
            HighlightNodeDependencies(null, null, null);
        }

        /// <summary>
        /// Searches for nodes matching the query and highlights them.
        /// </summary>
        public List<ServiceNode> SearchNodes(string query)
        {
            var results = new List<ServiceNode>();
            if (string.IsNullOrWhiteSpace(query)) return results;

            var lowerQuery = query.ToLowerInvariant();
            foreach (var node in AllNodes)
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

        private void LayoutNodes()
        {
            if (AllNodes.Count == 0) return;

            var levels = ComputeNodeLevels();
            var levelCounts = new Dictionary<int, int>();

            foreach (var node in AllNodes)
            {
                var level = levels.TryGetValue(node.ServiceType, out var l) ? l : 0;
                var countInLevel = levelCounts.TryGetValue(level, out var c) ? c : 0;
                levelCounts[level] = countInLevel + 1;

                var x = level * HORIZONTAL_SPACING + 50;
                var y = countInLevel * VERTICAL_SPACING + 50;
                node.SetPosition(new Rect(x, y, NODE_WIDTH, NODE_HEIGHT));
            }

            FrameAll();
        }

        private Dictionary<Type, int> ComputeNodeLevels()
        {
            var levels = new Dictionary<Type, int>();
            var visited = new HashSet<Type>();

            var hasIncoming = new HashSet<Type>();
            foreach (var edge in AllEdges)
            {
                if (edge.input?.node is ServiceNode targetNode)
                {
                    hasIncoming.Add(targetNode.ServiceType);
                }
            }

            var roots = AllNodes.Where(n => !hasIncoming.Contains(n.ServiceType)).ToList();
            if (roots.Count == 0 && AllNodes.Count > 0)
            {
                roots.Add(AllNodes[0]);
            }

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

                if (NodeMap.TryGetValue(type, out var node))
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
            foreach (var node in AllNodes)
            {
                var visible = !_lifetimeFilter.HasValue || node.Lifetime == _lifetimeFilter.Value;
                node.visible = visible;
            }

            foreach (var edge in AllEdges)
            {
                var sourceVisible = edge.output?.node?.visible ?? false;
                var targetVisible = edge.input?.node?.visible ?? false;
                edge.visible = sourceVisible && targetVisible;
            }
        }
    }
}
