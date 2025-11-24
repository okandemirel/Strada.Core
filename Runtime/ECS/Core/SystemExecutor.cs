using System;
using System.Collections.Generic;
using System.Linq;

namespace Strada.Core.ECS
{
    internal static class SystemExecutor
    {
        public static List<SystemRegistration> TopologicalSort(List<SystemRegistration> systems)
        {
            var graph = BuildDependencyGraph(systems);
            var inDegree = CalculateInDegrees(systems, graph);
            var sorted = PerformKahnSort(systems, graph, inDegree);

            ValidateSortResult(systems, sorted);

            return sorted;
        }

        private static Dictionary<Type, List<Type>> BuildDependencyGraph(List<SystemRegistration> systems)
        {
            var graph = new Dictionary<Type, List<Type>>();

            foreach (var system in systems)
            {
                graph[system.SystemType] = new List<Type>();
            }

            foreach (var system in systems)
            {
                AddUpdateAfterDependencies(system, graph);
                AddUpdateBeforeDependencies(system, graph);
            }

            return graph;
        }

        private static void AddUpdateAfterDependencies(SystemRegistration system, Dictionary<Type, List<Type>> graph)
        {
            if (system.Attribute.UpdateAfter == null)
                return;

            foreach (var dependency in system.Attribute.UpdateAfter)
            {
                if (graph.ContainsKey(dependency))
                {
                    graph[dependency].Add(system.SystemType);
                }
            }
        }

        private static void AddUpdateBeforeDependencies(SystemRegistration system, Dictionary<Type, List<Type>> graph)
        {
            if (system.Attribute.UpdateBefore == null)
                return;

            foreach (var dependency in system.Attribute.UpdateBefore)
            {
                if (graph.ContainsKey(dependency))
                {
                    graph[system.SystemType].Add(dependency);
                }
            }
        }

        private static Dictionary<Type, int> CalculateInDegrees(
            List<SystemRegistration> systems,
            Dictionary<Type, List<Type>> graph)
        {
            var inDegree = new Dictionary<Type, int>();

            foreach (var system in systems)
            {
                inDegree[system.SystemType] = 0;
            }

            foreach (var system in systems)
            {
                if (system.Attribute.UpdateAfter != null)
                {
                    foreach (var dependency in system.Attribute.UpdateAfter)
                    {
                        if (graph.ContainsKey(dependency))
                        {
                            inDegree[system.SystemType]++;
                        }
                    }
                }

                if (system.Attribute.UpdateBefore != null)
                {
                    foreach (var dependency in system.Attribute.UpdateBefore)
                    {
                        if (graph.ContainsKey(dependency))
                        {
                            inDegree[dependency]++;
                        }
                    }
                }
            }

            return inDegree;
        }

        private static List<SystemRegistration> PerformKahnSort(
            List<SystemRegistration> systems,
            Dictionary<Type, List<Type>> graph,
            Dictionary<Type, int> inDegree)
        {
            var queue = new Queue<Type>();
            foreach (var kvp in inDegree)
            {
                if (kvp.Value == 0)
                    queue.Enqueue(kvp.Key);
            }

            var sorted = new List<SystemRegistration>();
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var registration = systems.First(s => s.SystemType == current);
                sorted.Add(registration);

                foreach (var neighbor in graph[current])
                {
                    inDegree[neighbor]--;
                    if (inDegree[neighbor] == 0)
                        queue.Enqueue(neighbor);
                }
            }

            return sorted;
        }

        private static void ValidateSortResult(List<SystemRegistration> systems, List<SystemRegistration> sorted)
        {
            if (sorted.Count != systems.Count)
            {
                var remaining = systems.Where(s => !sorted.Contains(s)).Select(s => s.SystemType.Name);
                throw new InvalidOperationException(
                    $"Circular dependency detected in systems: {string.Join(", ", remaining)}");
            }
        }
    }
}
