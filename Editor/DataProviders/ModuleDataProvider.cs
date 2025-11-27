using System;
using System.Collections.Generic;
using System.Linq;
using Strada.Core.Bootstrap;
using Strada.Core.Editor.DataProviders.Models;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Strada.Core.Editor.DataProviders
{
    /// <summary>
    /// Provides access to module registry data for editor tools.
    /// Connects to ModuleRegistry via GameBootstrapper.
    /// </summary>
    public class ModuleDataProvider : EditorDataProviderBase<ModuleSnapshot>, IModuleDataProvider
    {
        private static ModuleDataProvider _instance;

        /// <summary>
        /// Gets the singleton instance of the ModuleDataProvider.
        /// </summary>
        public static ModuleDataProvider Instance => _instance ??= new ModuleDataProvider();

        private ModuleDataProvider() { }

        /// <summary>
        /// Gets whether the module registry is available.
        /// </summary>
        public override bool IsAvailable
        {
            get
            {
                if (!Application.isPlaying) return false;
                var bootstrapper = Object.FindObjectOfType<GameBootstrapper>();
                return bootstrapper != null && bootstrapper.IsInitialized;
            }
        }

        /// <summary>
        /// Gets all registered modules.
        /// </summary>
        public IReadOnlyList<ModuleInfoData> GetModules()
        {
            var snapshot = GetData();
            return snapshot?.Modules ?? new List<ModuleInfoData>();
        }

        /// <summary>
        /// Builds a dependency graph from the current modules.
        /// </summary>
        public DependencyGraph BuildModuleGraph()
        {
            var modules = GetModules();
            return BuildModuleGraphFromModules(modules);
        }

        /// <summary>
        /// Validates all modules and returns the result.
        /// </summary>
        public ValidationResult ValidateModules()
        {
            var result = new ValidationResult();

            var bootstrapper = Object.FindObjectOfType<GameBootstrapper>();
            if (bootstrapper == null)
            {
                result.IsValid = false;
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Message = "No GameBootstrapper found in scene"
                });
                return result;
            }

            var registry = bootstrapper.GetRegistry();
            if (registry == null)
            {
                result.IsValid = false;
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Message = "Module registry not initialized"
                });
                return result;
            }

            if (!registry.Validate(out var errorMessage))
            {
                result.IsValid = false;
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Message = errorMessage
                });
            }
            else
            {
                result.IsValid = true;
            }

            return result;
        }

        protected override ModuleSnapshot FetchData()
        {
            var bootstrapper = Object.FindObjectOfType<GameBootstrapper>();
            if (bootstrapper == null) return null;

            var registry = bootstrapper.GetRegistry();
            if (registry == null) return null;

            var snapshot = new ModuleSnapshot
            {
                Timestamp = DateTime.Now,
                ModuleCount = registry.Modules.Count,
                Modules = new List<ModuleInfoData>()
            };

            foreach (var module in registry.Modules)
            {
                snapshot.Modules.Add(new ModuleInfoData
                {
                    ModuleType = module.Type,
                    Name = module.Name,
                    Priority = module.Priority,
                    Dependencies = module.Dependencies?.ToList() ?? new List<Type>(),
                    IsInitialized = true
                });
            }

            if (!registry.Validate(out var errorMessage))
            {
                snapshot.HasCircularDependency = errorMessage?.Contains("Circular") ?? false;
                if (snapshot.HasCircularDependency)
                {
                    snapshot.CircularDependencyPath = ParseCyclePath(errorMessage);
                }
            }

            return snapshot;
        }

        private List<string> ParseCyclePath(string errorMessage)
        {
            if (string.IsNullOrEmpty(errorMessage)) return null;

            var colonIndex = errorMessage.IndexOf(':');
            if (colonIndex < 0) return null;

            var pathPart = errorMessage.Substring(colonIndex + 1).Trim();
            return pathPart.Split(new[] { " -> " }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        private DependencyGraph BuildModuleGraphFromModules(IReadOnlyList<ModuleInfoData> modules)
        {
            var graph = new DependencyGraph();
            var typeToNode = new Dictionary<Type, DependencyNode>();

            foreach (var module in modules)
            {
                var node = new DependencyNode
                {
                    ServiceType = module.ModuleType,
                    ImplementationType = module.ModuleType,
                    Lifetime = DI.Lifetime.Singleton
                };
                graph.Nodes.Add(node);
                typeToNode[module.ModuleType] = node;
            }

            foreach (var module in modules)
            {
                foreach (var depType in module.Dependencies)
                {
                    if (typeToNode.ContainsKey(depType))
                    {
                        graph.Edges.Add(new DependencyEdge
                        {
                            Source = module.ModuleType,
                            Target = depType,
                            IsCircular = false
                        });
                    }
                }
            }

            DetectCycles(graph);

            return graph;
        }

        private void DetectCycles(DependencyGraph graph)
        {
            var visited = new HashSet<Type>();
            var recursionStack = new HashSet<Type>();
            var path = new List<Type>();

            foreach (var node in graph.Nodes)
            {
                if (!visited.Contains(node.ServiceType))
                {
                    if (DetectCyclesDFS(node.ServiceType, graph, visited, recursionStack, path))
                    {
                        graph.HasCycle = true;
                        graph.CyclePath = new List<Type>(path);

                        for (int i = 0; i < path.Count - 1; i++)
                        {
                            var edge = graph.Edges.FirstOrDefault(e =>
                                e.Source == path[i] && e.Target == path[i + 1]);
                            if (edge != null)
                                edge.IsCircular = true;
                        }
                        return;
                    }
                }
            }
        }

        private bool DetectCyclesDFS(Type current, DependencyGraph graph,
            HashSet<Type> visited, HashSet<Type> recursionStack, List<Type> path)
        {
            visited.Add(current);
            recursionStack.Add(current);
            path.Add(current);

            var outgoingEdges = graph.Edges.Where(e => e.Source == current);
            foreach (var edge in outgoingEdges)
            {
                if (!visited.Contains(edge.Target))
                {
                    if (DetectCyclesDFS(edge.Target, graph, visited, recursionStack, path))
                        return true;
                }
                else if (recursionStack.Contains(edge.Target))
                {
                    path.Add(edge.Target);
                    return true;
                }
            }

            path.Remove(current);
            recursionStack.Remove(current);
            return false;
        }
    }

    /// <summary>
    /// Extended interface for module data provider.
    /// </summary>
    public interface IModuleDataProvider : IEditorDataProvider<ModuleSnapshot>
    {
        IReadOnlyList<ModuleInfoData> GetModules();
        DependencyGraph BuildModuleGraph();
        ValidationResult ValidateModules();
    }

    /// <summary>
    /// Result of module validation.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<ValidationIssue> Issues { get; set; } = new List<ValidationIssue>();
    }

    /// <summary>
    /// A validation issue found during module validation.
    /// </summary>
    public class ValidationIssue
    {
        public ValidationSeverity Severity { get; set; }
        public string Message { get; set; }
        public string FilePath { get; set; }
        public int LineNumber { get; set; }
        public string SuggestedFix { get; set; }
    }

    /// <summary>
    /// Severity level for validation issues.
    /// </summary>
    public enum ValidationSeverity
    {
        Info,
        Warning,
        Error
    }
}
