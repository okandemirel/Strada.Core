using Strada.Core.Editor.Graph;
using Strada.Core.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.Windows
{
    /// <summary>
    /// Editor window for visualizing Strada module dependencies as a graph.
    /// Accessible via Window > Strada > Module Graph menu.
    /// </summary>
    public class ModuleGraphWindow : GraphWindow
    {
        private Dictionary<Type, ModuleNode> _moduleNodes = new Dictionary<Type, ModuleNode>();
        private bool _autoLayout = true;
        private bool _showOnlyLoaded = false;

        [MenuItem("Window/Strada/Module Graph")]
        public static void ShowWindow()
        {
            var window = GetWindow<ModuleGraphWindow>("Module Graph");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshGraph();
        }

        protected override void DrawToolbarContent()
        {
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RefreshGraph();
            }

            GUILayout.Space(10);

            var newAutoLayout = GUILayout.Toggle(_autoLayout, "Auto Layout", EditorStyles.toolbarButton, GUILayout.Width(80));
            if (newAutoLayout != _autoLayout)
            {
                _autoLayout = newAutoLayout;
                if (_autoLayout)
                {
                    ApplyAutoLayout();
                }
            }

            GUILayout.Space(10);

            var newShowOnlyLoaded = GUILayout.Toggle(_showOnlyLoaded, "Loaded Only", EditorStyles.toolbarButton, GUILayout.Width(90));
            if (newShowOnlyLoaded != _showOnlyLoaded)
            {
                _showOnlyLoaded = newShowOnlyLoaded;
                RefreshGraph();
            }
        }

        protected override void RefreshGraph()
        {
            Nodes.Clear();
            Connections.Clear();
            _moduleNodes.Clear();

            DiscoverModules();
            CreateConnections();

            if (_autoLayout)
            {
                ApplyAutoLayout();
            }
        }

        /// <summary>
        /// Discovers all module installers in the project.
        /// </summary>
        private void DiscoverModules()
        {
            var moduleInstallerType = typeof(IModuleInstaller);
            var allTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => moduleInstallerType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            int index = 0;
            foreach (var moduleType in allTypes)
            {
                var moduleName = moduleType.Name.Replace("ModuleInstaller", "").Replace("Installer", "");

                var node = new ModuleNode(moduleType, moduleName)
                {
                    RegistrationCount = EstimateRegistrationCount(moduleType),
                    HasECS = typeof(IECSModuleInstaller).IsAssignableFrom(moduleType),
                    IsLoaded = true,
                    Dependencies = GetModuleDependencies(moduleType),
                    Position = new Rect(100 + (index % 3) * 250, 100 + (index / 3) * 150, 200, 80)
                };

                Nodes.Add(node);
                _moduleNodes[moduleType] = node;
                index++;
            }
        }

        /// <summary>
        /// Creates connections between modules based on dependencies.
        /// </summary>
        private void CreateConnections()
        {
            foreach (var kvp in _moduleNodes)
            {
                var fromNode = kvp.Value;

                foreach (var depName in fromNode.Dependencies)
                {
                    var toNode = _moduleNodes.Values.FirstOrDefault(n => n.Title == depName);

                    if (toNode != null)
                    {
                        var connection = new GraphConnection(fromNode, toNode)
                        {
                            Color = StradaEditorStyles.PrimaryColor,
                            Thickness = 2f
                        };

                        Connections.Add(connection);
                        fromNode.OutgoingConnections.Add(connection);
                        toNode.IncomingConnections.Add(connection);
                    }
                }
            }
        }

        /// <summary>
        /// Estimates the number of registrations in a module (placeholder logic).
        /// </summary>
        private int EstimateRegistrationCount(Type moduleType)
        {
            return UnityEngine.Random.Range(3, 10);
        }

        /// <summary>
        /// Gets module dependencies from attributes or analysis (placeholder).
        /// </summary>
        private string[] GetModuleDependencies(Type moduleType)
        {
            return Array.Empty<string>();
        }

        /// <summary>
        /// Applies automatic force-directed layout to nodes.
        /// </summary>
        private void ApplyAutoLayout()
        {
            if (Nodes.Count == 0) return;

            const int iterations = 50;
            const float repulsionStrength = 5000f;
            const float attractionStrength = 0.1f;
            const float damping = 0.5f;

            var velocities = new Dictionary<GraphNode, Vector2>();
            foreach (var node in Nodes)
            {
                velocities[node] = Vector2.zero;
            }

            for (int iter = 0; iter < iterations; iter++)
            {
                foreach (var node in Nodes)
                {
                    var force = Vector2.zero;

                    foreach (var other in Nodes)
                    {
                        if (node == other) continue;

                        var delta = node.Position.center - other.Position.center;
                        var distance = delta.magnitude;

                        if (distance < 1f) distance = 1f;

                        var repulsion = (delta.normalized * repulsionStrength) / (distance * distance);
                        force += repulsion;
                    }

                    foreach (var connection in node.OutgoingConnections)
                    {
                        var other = connection.ToNode;
                        var delta = other.Position.center - node.Position.center;
                        var distance = delta.magnitude;

                        var attraction = delta.normalized * distance * attractionStrength;
                        force += attraction;
                    }

                    foreach (var connection in node.IncomingConnections)
                    {
                        var other = connection.FromNode;
                        var delta = other.Position.center - node.Position.center;
                        var distance = delta.magnitude;

                        var attraction = delta.normalized * distance * attractionStrength;
                        force += attraction;
                    }

                    velocities[node] = velocities[node] * damping + force * 0.01f;
                }

                foreach (var node in Nodes)
                {
                    var newPos = node.Position.center + velocities[node];
                    node.Position = new Rect(
                        newPos.x - node.Position.width / 2,
                        newPos.y - node.Position.height / 2,
                        node.Position.width,
                        node.Position.height);
                }
            }

            CenterGraph();
        }

        /// <summary>
        /// Centers the graph in the viewport.
        /// </summary>
        private void CenterGraph()
        {
            if (Nodes.Count == 0) return;

            var minX = Nodes.Min(n => n.Position.xMin);
            var minY = Nodes.Min(n => n.Position.yMin);

            var offset = new Vector2(-minX + 100, -minY + 100);

            foreach (var node in Nodes)
            {
                node.Position = new Rect(
                    node.Position.x + offset.x,
                    node.Position.y + offset.y,
                    node.Position.width,
                    node.Position.height);
            }
        }

        protected override void OnNodeSelected(GraphNode node)
        {
            if (node is ModuleNode moduleNode)
            {
                Debug.Log($"[Module Graph] Selected: {moduleNode.Title}");
                Debug.Log($"  Type: {moduleNode.ModuleType?.FullName}");
                Debug.Log($"  Registrations: {moduleNode.RegistrationCount}");
                Debug.Log($"  Has ECS: {moduleNode.HasECS}");
                Debug.Log($"  Dependencies: {string.Join(", ", moduleNode.Dependencies)}");
            }
        }
    }
}
