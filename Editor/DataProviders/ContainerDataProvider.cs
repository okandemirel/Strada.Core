using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Strada.Core.Bootstrap;
using Strada.Core.DI;
using Strada.Core.Editor.DataProviders.Models;
using UnityEngine;

namespace Strada.Core.Editor.DataProviders
{
    /// <summary>
    /// Provides access to DI container registration data for editor tools.
    /// Connects to FastContainer at runtime via GameBootstrapper.Container.
    /// </summary>
    public class ContainerDataProvider : EditorDataProviderBase<ContainerSnapshot>, IContainerDataProvider
    {
        private static ContainerDataProvider _instance;
        
        /// <summary>
        /// Gets the singleton instance of the ContainerDataProvider.
        /// </summary>
        public static ContainerDataProvider Instance => _instance ??= new ContainerDataProvider();

        private ContainerDataProvider() { }

        /// <summary>
        /// Gets whether the container is available (Play Mode with initialized container).
        /// </summary>
        public override bool IsAvailable => 
            Application.isPlaying && GameBootstrapper.Container != null;

        /// <summary>
        /// Gets all service registrations from the container.
        /// </summary>
        public IReadOnlyList<ServiceRegistrationInfo> GetRegistrations()
        {
            var snapshot = GetData();
            return snapshot?.Registrations ?? new List<ServiceRegistrationInfo>();
        }

        /// <summary>
        /// Builds a dependency graph from the current registrations.
        /// </summary>
        public DependencyGraph BuildDependencyGraph()
        {
            var registrations = GetRegistrations();
            return BuildDependencyGraphFromRegistrations(registrations);
        }

        /// <summary>
        /// Checks if there is a circular dependency in the container.
        /// </summary>
        public bool HasCircularDependency(out List<Type> cycle)
        {
            cycle = null;
            var graph = BuildDependencyGraph();
            if (graph.HasCycle)
            {
                cycle = graph.CyclePath;
                return true;
            }
            return false;
        }

        protected override ContainerSnapshot FetchData()
        {
            var container = GameBootstrapper.Container;
            if (container == null)
                return null;

            var snapshot = new ContainerSnapshot
            {
                Timestamp = DateTime.Now,
                Registrations = ExtractRegistrations(container)
            };

            snapshot.RegistrationCount = snapshot.Registrations.Count;
            snapshot.SingletonCount = snapshot.Registrations.Count(r => r.Lifetime == Lifetime.Singleton);
            snapshot.TransientCount = snapshot.Registrations.Count(r => r.Lifetime == Lifetime.Transient);
            snapshot.ScopedCount = snapshot.Registrations.Count(r => r.Lifetime == Lifetime.Scoped);

            return snapshot;
        }

        private List<ServiceRegistrationInfo> ExtractRegistrations(IContainer container)
        {
            var registrations = new List<ServiceRegistrationInfo>();

            if (container is not FastContainer fastContainer)
                return registrations;

            try
            {
                var containerType = typeof(FastContainer);
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;

                var registeredTypesField = containerType.GetField("_registeredTypes", flags);
                var lifetimesField = containerType.GetField("_lifetimes", flags);
                var singletonsField = containerType.GetField("_singletons", flags);
                var registeredCountField = containerType.GetField("_registeredCount", flags);

                if (registeredTypesField == null || lifetimesField == null)
                {
                    Debug.LogWarning("[ContainerDataProvider] Could not access container internals via reflection");
                    return registrations;
                }

                var registeredTypes = (Type[])registeredTypesField.GetValue(fastContainer);
                var lifetimes = (Lifetime[])lifetimesField.GetValue(fastContainer);
                var singletons = (object[])singletonsField?.GetValue(fastContainer);
                var registeredCount = (int)registeredCountField.GetValue(fastContainer);

                for (int i = 0; i < registeredCount; i++)
                {
                    var serviceType = registeredTypes[i];
                    if (serviceType == null) continue;

                    var registration = new ServiceRegistrationInfo
                    {
                        ServiceType = serviceType,
                        ImplementationType = serviceType,
                        Lifetime = lifetimes[i],
                        HasInstance = singletons != null && singletons[i] != null,
                        Dependencies = GetConstructorDependencies(serviceType)
                    };

                    if (registration.HasInstance && singletons[i] != null)
                    {
                        registration.ImplementationType = singletons[i].GetType();
                    }

                    registrations.Add(registration);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ContainerDataProvider] Error extracting registrations: {ex.Message}");
            }

            return registrations;
        }

        private Type[] GetConstructorDependencies(Type type)
        {
            try
            {
                if (type.IsInterface || type.IsAbstract)
                    return Array.Empty<Type>();

                var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
                if (constructors.Length == 0)
                    return Array.Empty<Type>();

                var bestCtor = constructors.OrderByDescending(c => c.GetParameters().Length).First();
                return bestCtor.GetParameters().Select(p => p.ParameterType).ToArray();
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }

        private DependencyGraph BuildDependencyGraphFromRegistrations(IReadOnlyList<ServiceRegistrationInfo> registrations)
        {
            var graph = new DependencyGraph();
            var typeToNode = new Dictionary<Type, DependencyNode>();

            foreach (var reg in registrations)
            {
                var node = new DependencyNode
                {
                    ServiceType = reg.ServiceType,
                    ImplementationType = reg.ImplementationType,
                    Lifetime = reg.Lifetime
                };
                graph.Nodes.Add(node);
                typeToNode[reg.ServiceType] = node;
            }

            foreach (var reg in registrations)
            {
                foreach (var depType in reg.Dependencies)
                {
                    if (typeToNode.ContainsKey(depType))
                    {
                        graph.Edges.Add(new DependencyEdge
                        {
                            Source = reg.ServiceType,
                            Target = depType,
                            IsCircular = false
                        });
                    }
                }
            }

            DetectCycles(graph, typeToNode);

            return graph;
        }

        private void DetectCycles(DependencyGraph graph, Dictionary<Type, DependencyNode> typeToNode)
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
    /// Extended interface for container data provider with graph building capabilities.
    /// </summary>
    public interface IContainerDataProvider : IEditorDataProvider<ContainerSnapshot>
    {
        IReadOnlyList<ServiceRegistrationInfo> GetRegistrations();
        DependencyGraph BuildDependencyGraph();
        bool HasCircularDependency(out List<Type> cycle);
    }
}
