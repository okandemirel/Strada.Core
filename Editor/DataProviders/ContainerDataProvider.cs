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
    /// Connects to Container at runtime via GameBootstrapper.Container.
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
            return snapshot?.Registrations ?? (IReadOnlyList<ServiceRegistrationInfo>)Array.Empty<ServiceRegistrationInfo>();
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

            if (container is not Container fastContainer)
                return registrations;

            try
            {
                var containerType = typeof(Container);
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

                    if (registration.HasInstance)
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

            graph.DetectCycles();

            return graph;
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
