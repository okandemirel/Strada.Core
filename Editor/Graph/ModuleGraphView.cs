using System;
using System.Collections.Generic;
using System.Linq;
using Strada.Core.Editor.DataProviders;
using Strada.Core.Editor.DataProviders.Models;
using UnityEngine;

namespace Strada.Core.Editor.Graph
{
    /// <summary>
    /// GraphView-based interactive visualization of module dependencies.
    /// Displays modules as nodes with priority badges and dependency edges.
    /// Requirements: 6.1, 6.2
    /// </summary>
    public class ModuleGraphView : BaseGraphView<ModuleNode>
    {
        private const float NODE_WIDTH = 220f;
        private const float NODE_HEIGHT = 100f;
        private const float HORIZONTAL_SPACING = 280f;
        private const float VERTICAL_SPACING = 140f;

        private List<Type> _initializationOrder;

        /// <summary>
        /// Gets the initialization order of modules.
        /// </summary>
        public IReadOnlyList<Type> InitializationOrder => _initializationOrder;

        protected override void OnClearGraph()
        {
            _initializationOrder = null;
        }

        /// <summary>
        /// Populates the graph with data from the module data provider.
        /// </summary>
        public void PopulateFromProvider()
        {
            var provider = ModuleDataProvider.Instance;
            if (!provider.IsAvailable)
            {
                ClearGraph();
                ShowMessage("Module registry not available. Enter Play Mode to view modules.");
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
                ShowMessage("No modules found in registry.");
                return;
            }

            SetCycleInfo(graph?.HasCycle ?? false, graph?.CyclePath);

            foreach (var moduleInfo in modules)
            {
                var node = new ModuleNode(moduleInfo);
                RegisterNodeCallbacks(node);
                AddNodeToGraph(moduleInfo.ModuleType, node);
            }

            if (graph != null)
            {
                foreach (var edgeData in graph.Edges)
                {
                    if (NodeMap.TryGetValue(edgeData.Source, out var sourceNode) &&
                        NodeMap.TryGetValue(edgeData.Target, out var targetNode))
                    {
                        CreateEdge(sourceNode.OutputPort, targetNode.InputPort, edgeData.IsCircular);
                    }
                }
            }

            ComputeInitializationOrder(modules);
            LayoutNodesByInitOrder();
            InvokeGraphRefreshed();
        }

        public void HighlightNodeDependencies(ModuleNode node)
        {
            HighlightNodeDependencies(node, node?.InputPort, node?.OutputPort);
        }

        public void ClearHighlights()
        {
            HighlightNodeDependencies(null, null, null);
        }

        /// <summary>
        /// Searches for nodes matching the query.
        /// </summary>
        public List<ModuleNode> SearchNodes(string query)
        {
            var results = new List<ModuleNode>();
            if (string.IsNullOrWhiteSpace(query)) return results;

            var lowerQuery = query.ToLowerInvariant();
            foreach (var node in AllNodes)
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

        private void ComputeInitializationOrder(IReadOnlyList<ModuleInfoData> modules)
        {
            _initializationOrder = new List<Type>();
            var visited = new HashSet<Type>();
            var remaining = new List<ModuleInfoData>(modules);

            while (remaining.Count > 0)
            {
                var ready = remaining
                    .Where(m => m.Dependencies.All(d => visited.Contains(d) || !NodeMap.ContainsKey(d)))
                    .OrderBy(m => m.Priority)
                    .ToList();

                if (ready.Count == 0)
                {
                    ready = remaining.OrderBy(m => m.Priority).ToList();
                }

                foreach (var module in ready)
                {
                    visited.Add(module.ModuleType);
                    remaining.Remove(module);
                    _initializationOrder.Add(module.ModuleType);
                }
            }
        }

        private void LayoutNodesByInitOrder()
        {
            if (_initializationOrder == null || _initializationOrder.Count == 0) return;

            var nodesPerColumn = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(AllNodes.Count)));

            for (int i = 0; i < _initializationOrder.Count; i++)
            {
                var type = _initializationOrder[i];
                if (NodeMap.TryGetValue(type, out var node))
                {
                    var column = i / nodesPerColumn;
                    var row = i % nodesPerColumn;

                    var x = column * HORIZONTAL_SPACING + 50;
                    var y = row * VERTICAL_SPACING + 50;
                    node.SetPosition(new Rect(x, y, NODE_WIDTH, NODE_HEIGHT));

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
    }
}
