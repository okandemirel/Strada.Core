using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Strada.Core.ECS.Baking
{
    /// <summary>
    /// Utility methods for the baking system.
    /// </summary>
    public static class BakingUtility
    {
        /// <summary>
        /// Discovers all bakers in the current app domain.
        /// </summary>
        /// <param name="assemblyFilter">Optional filter for assemblies to scan</param>
        /// <returns>List of discovered baker types</returns>
        public static List<Type> DiscoverBakers(Func<Assembly, bool> assemblyFilter = null)
        {
            var bakerTypes = new List<Type>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                // Skip Unity and System assemblies by default
                if (assemblyFilter != null && !assemblyFilter(assembly))
                    continue;

                if (ShouldSkipAssembly(assembly))
                    continue;

                try
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        if (type.IsAbstract || type.IsInterface)
                            continue;

                        // Check if type implements IBaker
                        if (!typeof(IBaker).IsAssignableFrom(type))
                            continue;

                        // Check for StradaBaker attribute (optional)
                        var attribute = type.GetCustomAttribute<StradaBakerAttribute>();
                        if (attribute != null && !attribute.EnabledByDefault)
                            continue;

                        bakerTypes.Add(type);
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    Debug.LogWarning($"Failed to load types from assembly {assembly.FullName}: {ex.Message}");
                }
            }

            return bakerTypes;
        }

        /// <summary>
        /// Creates an instance of a baker.
        /// </summary>
        /// <param name="bakerType">The baker type</param>
        /// <returns>The baker instance</returns>
        public static IBaker CreateBaker(Type bakerType)
        {
            if (bakerType == null)
                throw new ArgumentNullException(nameof(bakerType));

            if (!typeof(IBaker).IsAssignableFrom(bakerType))
                throw new ArgumentException($"Type {bakerType.Name} does not implement IBaker", nameof(bakerType));

            try
            {
                return (IBaker)Activator.CreateInstance(bakerType);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create baker of type {bakerType.Name}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets the authoring type for a baker.
        /// </summary>
        /// <param name="bakerType">The baker type</param>
        /// <returns>The authoring type</returns>
        public static Type GetAuthoringType(Type bakerType)
        {
            if (bakerType == null)
                throw new ArgumentNullException(nameof(bakerType));

            // Look for IBaker<TAuthoring> interface
            var bakerInterface = bakerType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IBaker<>));

            if (bakerInterface == null)
                throw new InvalidOperationException($"Type {bakerType.Name} does not implement IBaker<T>");

            return bakerInterface.GetGenericArguments()[0];
        }

        /// <summary>
        /// Validates that a baker is correctly configured.
        /// </summary>
        /// <param name="bakerType">The baker type</param>
        /// <param name="errorMessage">Error message if validation fails</param>
        /// <returns>True if validation succeeds</returns>
        public static bool ValidateBaker(Type bakerType, out string errorMessage)
        {
            if (bakerType == null)
            {
                errorMessage = "Baker type is null";
                return false;
            }

            if (bakerType.IsAbstract)
            {
                errorMessage = $"Baker type {bakerType.Name} is abstract and cannot be instantiated";
                return false;
            }

            if (!typeof(IBaker).IsAssignableFrom(bakerType))
            {
                errorMessage = $"Baker type {bakerType.Name} does not implement IBaker";
                return false;
            }

            // Check for parameterless constructor
            var constructor = bakerType.GetConstructor(Type.EmptyTypes);
            if (constructor == null)
            {
                errorMessage = $"Baker type {bakerType.Name} must have a parameterless constructor";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// Checks if a type is a valid authoring type.
        /// </summary>
        /// <param name="type">The type to check</param>
        /// <returns>True if the type is a valid authoring type</returns>
        public static bool IsValidAuthoringType(Type type)
        {
            if (type == null)
                return false;

            // Must be a reference type
            if (type.IsValueType)
                return false;

            // Must be a MonoBehaviour or ScriptableObject
            return typeof(MonoBehaviour).IsAssignableFrom(type) ||
                   typeof(ScriptableObject).IsAssignableFrom(type);
        }

        /// <summary>
        /// Gets the priority of a baker.
        /// </summary>
        /// <param name="bakerType">The baker type</param>
        /// <returns>The priority (lower values execute first)</returns>
        public static int GetBakerPriority(Type bakerType)
        {
            var attribute = bakerType.GetCustomAttribute<StradaBakerAttribute>();
            return attribute?.Priority ?? 0;
        }

        /// <summary>
        /// Sorts bakers by priority.
        /// </summary>
        /// <param name="bakerTypes">The baker types to sort</param>
        /// <returns>Sorted list of baker types</returns>
        public static List<Type> SortBakersByPriority(IEnumerable<Type> bakerTypes)
        {
            return bakerTypes.OrderBy(GetBakerPriority).ToList();
        }

        /// <summary>
        /// Creates a baker context for testing.
        /// </summary>
        /// <param name="entityManager">The entity manager</param>
        /// <returns>A baker context instance</returns>
        public static IBakerContext CreateTestContext(IEntityManager entityManager)
        {
            return new SimpleBakerContext(entityManager)
            {
                IsRuntime = false
            };
        }

        private static bool ShouldSkipAssembly(Assembly assembly)
        {
            var name = assembly.FullName;
            return name.StartsWith("Unity.") ||
                   name.StartsWith("UnityEngine.") ||
                   name.StartsWith("UnityEditor.") ||
                   name.StartsWith("System.") ||
                   name.StartsWith("mscorlib") ||
                   name.StartsWith("netstandard");
        }
    }

    /// <summary>
    /// Registry for managing bakers.
    /// </summary>
    public class BakerRegistry
    {
        private readonly Dictionary<Type, List<IBaker>> _bakersByAuthoringType;
        private readonly List<IBaker> _allBakers;

        /// <summary>
        /// Gets the number of registered bakers.
        /// </summary>
        public int Count => _allBakers.Count;

        /// <summary>
        /// Initializes a new instance of BakerRegistry.
        /// </summary>
        public BakerRegistry()
        {
            _bakersByAuthoringType = new Dictionary<Type, List<IBaker>>();
            _allBakers = new List<IBaker>();
        }

        /// <summary>
        /// Registers a baker.
        /// </summary>
        /// <param name="baker">The baker to register</param>
        public void Register(IBaker baker)
        {
            if (baker == null)
                throw new ArgumentNullException(nameof(baker));

            var authoringType = baker.AuthoringType;
            if (!_bakersByAuthoringType.TryGetValue(authoringType, out var bakers))
            {
                bakers = new List<IBaker>();
                _bakersByAuthoringType[authoringType] = bakers;
            }

            if (!bakers.Contains(baker))
            {
                bakers.Add(baker);
                _allBakers.Add(baker);
            }
        }

        /// <summary>
        /// Gets all bakers for a specific authoring type.
        /// </summary>
        /// <param name="authoringType">The authoring type</param>
        /// <returns>List of bakers for the authoring type</returns>
        public List<IBaker> GetBakersForType(Type authoringType)
        {
            if (_bakersByAuthoringType.TryGetValue(authoringType, out var bakers))
            {
                return new List<IBaker>(bakers);
            }
            return new List<IBaker>();
        }

        /// <summary>
        /// Gets all registered bakers.
        /// </summary>
        /// <returns>List of all bakers</returns>
        public List<IBaker> GetAllBakers()
        {
            return new List<IBaker>(_allBakers);
        }

        /// <summary>
        /// Clears all registered bakers.
        /// </summary>
        public void Clear()
        {
            _bakersByAuthoringType.Clear();
            _allBakers.Clear();
        }

        /// <summary>
        /// Discovers and registers all bakers in the current app domain.
        /// </summary>
        /// <param name="assemblyFilter">Optional filter for assemblies to scan</param>
        public void DiscoverAndRegisterBakers(Func<Assembly, bool> assemblyFilter = null)
        {
            var bakerTypes = BakingUtility.DiscoverBakers(assemblyFilter);

            foreach (var bakerType in bakerTypes)
            {
                try
                {
                    var baker = BakingUtility.CreateBaker(bakerType);
                    Register(baker);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to register baker {bakerType.Name}: {ex.Message}");
                }
            }
        }
    }
}
