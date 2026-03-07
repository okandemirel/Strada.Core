using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Strada.Core.Bootstrap;
using Strada.Core.Editor.DataProviders.Models;
using Strada.Core.Modules;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Strada.Core.Editor.DataProviders
{
    /// <summary>
    /// Provides access to module data for editor tools.
    /// Connects to GameBootstrapperConfig for module information.
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
        /// Gets whether the module data is available.
        /// </summary>
        public override bool IsAvailable
        {
            get
            {
                if (!Application.isPlaying) return false;
                var bootstrapper = Object.FindFirstObjectByType<GameBootstrapper>();
                return bootstrapper != null && bootstrapper.IsInitialized;
            }
        }

        /// <summary>
        /// Gets all registered modules.
        /// </summary>
        public IReadOnlyList<ModuleInfoData> GetModules()
        {
            var snapshot = GetData();
            return snapshot?.Modules ?? (IReadOnlyList<ModuleInfoData>)Array.Empty<ModuleInfoData>();
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

            var config = FindGameBootstrapperConfig();
            if (config == null)
            {
                result.IsValid = false;
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Message = "No GameBootstrapperConfig found"
                });
                return result;
            }

            if (!config.Validate(out var errors))
            {
                result.IsValid = false;
                foreach (var error in errors)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        Message = error
                    });
                }
            }
            else
            {
                result.IsValid = true;
            }

            return result;
        }

        protected override ModuleSnapshot FetchData()
        {
            var config = FindGameBootstrapperConfig();
            if (config == null) return null;

            var enabledModules = config.GetEnabledModules().ToList();

            var snapshot = new ModuleSnapshot
            {
                Timestamp = DateTime.Now,
                ModuleCount = enabledModules.Count,
                Modules = new List<ModuleInfoData>()
            };

            foreach (var module in enabledModules)
            {
                var dependencies = module.Dependencies?
                    .Where(d => d != null)
                    .Select(d => d.GetType())
                    .ToList() ?? new List<Type>();

                snapshot.Modules.Add(new ModuleInfoData
                {
                    ModuleType = module.GetType(),
                    Name = module.ModuleName,
                    Priority = 0,
                    Dependencies = dependencies,
                    IsInitialized = true
                });
            }

            if (!config.Validate(out var errors))
            {
                var errorMessage = string.Join("\n", errors);
                snapshot.HasCircularDependency = errorMessage.Contains("Circular");
                if (snapshot.HasCircularDependency)
                {
                    snapshot.CircularDependencyPath = ParseCyclePath(errorMessage);
                }
            }

            return snapshot;
        }

        private GameBootstrapperConfig FindGameBootstrapperConfig()
        {
            var bootstrapper = Object.FindFirstObjectByType<GameBootstrapper>();
            if (bootstrapper != null)
            {
                var configField = typeof(GameBootstrapper).GetField("_gameConfig",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (configField != null)
                {
                    return configField.GetValue(bootstrapper) as GameBootstrapperConfig;
                }
            }

            var guids = UnityEditor.AssetDatabase.FindAssets("t:GameBootstrapperConfig");
            if (guids.Length > 0)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                return UnityEditor.AssetDatabase.LoadAssetAtPath<GameBootstrapperConfig>(path);
            }

            return null;
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

            graph.DetectCycles();

            return graph;
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
