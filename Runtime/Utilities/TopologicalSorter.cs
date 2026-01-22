using System;
using System.Collections.Generic;

namespace Strada.Core.Utilities
{
    /// <summary>
    /// Generic topological sorter using depth-first search.
    /// Sorts items based on their dependencies, ensuring dependencies come before dependents.
    /// </summary>
    /// <typeparam name="T">The type of items to sort.</typeparam>
    public static class TopologicalSorter<T> where T : class
    {
        /// <summary>
        /// Sorts items topologically based on their dependencies.
        /// </summary>
        /// <param name="items">The items to sort.</param>
        /// <param name="getDependencies">Function to get dependencies of an item.</param>
        /// <param name="getIdentifier">Function to get a unique identifier for error messages.</param>
        /// <returns>Sorted list with dependencies before dependents.</returns>
        /// <exception cref="InvalidOperationException">Thrown when a circular dependency is detected.</exception>
        public static List<T> Sort(
            IEnumerable<T> items,
            Func<T, IEnumerable<T>> getDependencies,
            Func<T, string> getIdentifier = null)
        {
            var itemSet = new HashSet<T>(items);
            var sorted = new List<T>();
            var visited = new HashSet<T>();
            var visiting = new HashSet<T>();
            var getId = getIdentifier ?? (item => item.ToString());

            foreach (var item in items)
            {
                if (!visited.Contains(item))
                {
                    Visit(item, itemSet, visited, visiting, sorted, getDependencies, getId);
                }
            }

            return sorted;
        }

        /// <summary>
        /// Validates that the items have no circular dependencies.
        /// </summary>
        /// <param name="items">The items to validate.</param>
        /// <param name="getDependencies">Function to get dependencies of an item.</param>
        /// <param name="getIdentifier">Function to get a unique identifier for error messages.</param>
        /// <param name="cyclePath">Output parameter containing the cycle path if found.</param>
        /// <returns>True if no circular dependencies exist, false otherwise.</returns>
        public static bool ValidateNoCycles(
            IEnumerable<T> items,
            Func<T, IEnumerable<T>> getDependencies,
            Func<T, string> getIdentifier,
            out List<string> cyclePath)
        {
            cyclePath = null;
            var itemSet = new HashSet<T>(items);
            var visited = new HashSet<T>();
            var visiting = new HashSet<T>();
            var path = new List<T>();

            foreach (var item in items)
            {
                if (!visited.Contains(item))
                {
                    if (DetectCycle(item, itemSet, visited, visiting, path, getDependencies))
                    {
                        cyclePath = new List<string>();
                        foreach (var p in path)
                            cyclePath.Add(getIdentifier(p));
                        return false;
                    }
                }
            }

            return true;
        }

        private static void Visit(
            T item,
            HashSet<T> itemSet,
            HashSet<T> visited,
            HashSet<T> visiting,
            List<T> sorted,
            Func<T, IEnumerable<T>> getDependencies,
            Func<T, string> getId)
        {
            if (visiting.Contains(item))
            {
                throw new InvalidOperationException(
                    $"Circular dependency detected involving '{getId(item)}'. Check dependencies for cycles.");
            }

            if (visited.Contains(item))
                return;

            visiting.Add(item);

            foreach (var dependency in getDependencies(item))
            {
                if (dependency != null && itemSet.Contains(dependency))
                {
                    Visit(dependency, itemSet, visited, visiting, sorted, getDependencies, getId);
                }
            }

            visiting.Remove(item);
            visited.Add(item);
            sorted.Add(item);
        }

        private static bool DetectCycle(
            T item,
            HashSet<T> itemSet,
            HashSet<T> visited,
            HashSet<T> visiting,
            List<T> path,
            Func<T, IEnumerable<T>> getDependencies)
        {
            if (visiting.Contains(item))
            {
                path.Add(item);
                return true;
            }

            if (visited.Contains(item))
                return false;

            visited.Add(item);
            visiting.Add(item);
            path.Add(item);

            foreach (var dependency in getDependencies(item))
            {
                if (dependency != null && itemSet.Contains(dependency))
                {
                    if (DetectCycle(dependency, itemSet, visited, visiting, path, getDependencies))
                        return true;
                }
            }

            path.Remove(item);
            visiting.Remove(item);
            return false;
        }
    }
}
