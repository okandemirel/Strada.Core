using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Strada.Core.ECS;
using Strada.Core.ECS.Storage;
using Strada.Core.Editor.DataProviders.Models;
using UnityEngine;

namespace Strada.Core.Editor.DataProviders
{
    /// <summary>
    /// Provides access to ECS World data for editor tools.
    /// Connects to World.Current at runtime.
    /// </summary>
    public class WorldDataProvider : EditorDataProviderBase<WorldSnapshot>, IWorldDataProvider
    {
        private static WorldDataProvider _instance;

        /// <summary>
        /// Gets the singleton instance of the WorldDataProvider.
        /// </summary>
        public static WorldDataProvider Instance => _instance ??= new WorldDataProvider();

        private WorldDataProvider() { }

        /// <summary>
        /// Gets whether the World is available (Play Mode with active World).
        /// </summary>
        public override bool IsAvailable =>
            Application.isPlaying && World.Current != null;

        /// <summary>
        /// Gets all entity IDs in the current world.
        /// </summary>
        public IEnumerable<int> GetEntityIds()
        {
            if (!IsAvailable) return Enumerable.Empty<int>();

            try
            {
                return World.Current.Entities.GetAllEntities();
            }
            catch
            {
                return Enumerable.Empty<int>();
            }
        }

        /// <summary>
        /// Gets all component types registered in the world.
        /// </summary>
        public IEnumerable<Type> GetComponentTypes()
        {
            if (!IsAvailable) return Enumerable.Empty<Type>();

            try
            {
                return World.Current.Entities.Store.GetComponentTypes();
            }
            catch
            {
                return Enumerable.Empty<Type>();
            }
        }

        /// <summary>
        /// Gets a component value as a boxed object.
        /// </summary>
        public object GetComponentBoxed(int entityId, Type componentType)
        {
            if (!IsAvailable) return null;

            try
            {
                return World.Current.Entities.Store.GetComponentBoxed(entityId, componentType);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Sets a component value from a boxed object.
        /// </summary>
        public void SetComponentBoxed(int entityId, Type componentType, object value)
        {
            if (!IsAvailable) return;

            try
            {
                World.Current.Entities.Store.SetComponentBoxed(entityId, componentType, value);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WorldDataProvider] Failed to set component: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if an entity exists.
        /// </summary>
        public bool EntityExists(int entityId)
        {
            if (!IsAvailable) return false;

            try
            {
                var entityIds = World.Current.Entities.GetAllEntities();
                return entityIds.Contains(entityId);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets all components attached to an entity.
        /// </summary>
        public IEnumerable<ComponentInfo> GetEntityComponents(int entityId)
        {
            if (!IsAvailable) yield break;

            var componentTypes = GetComponentTypes();
            var store = World.Current.Entities.Store;

            foreach (var componentType in componentTypes)
            {
                if (store.HasComponent(entityId, componentType))
                {
                    var value = store.GetComponentBoxed(entityId, componentType);
                    yield return new ComponentInfo
                    {
                        ComponentType = componentType,
                        Value = value,
                        Fields = ExtractFieldValues(componentType, value)
                    };
                }
            }
        }

        protected override WorldSnapshot FetchData()
        {
            var world = World.Current;
            if (world == null) return null;

            var snapshot = new WorldSnapshot
            {
                Timestamp = DateTime.Now,
                EntityCount = world.Entities.EntityCount,
                ComponentTypeCount = world.Entities.Store.GetComponentTypes().Count(),
                Entities = new List<EntityInfo>(),
                Systems = new List<Models.SystemInfo>()
            };

            // Enumerate entities
            foreach (var entityId in world.Entities.GetAllEntities())
            {
                var entityInfo = new EntityInfo
                {
                    Id = entityId,
                    Version = GetEntityVersion(entityId),
                    Components = GetEntityComponents(entityId).ToList()
                };
                snapshot.Entities.Add(entityInfo);
            }

            // Get system info (if accessible)
            snapshot.Systems = ExtractSystemInfo(world);
            snapshot.SystemCount = snapshot.Systems.Count;

            return snapshot;
        }

        private int GetEntityVersion(int entityId)
        {
            try
            {
                // Access entity versions via reflection
                var entityManager = World.Current.Entities;
                var versionsField = typeof(EntityManager).GetField("_entityVersions", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (versionsField?.GetValue(entityManager) is Dictionary<int, int> versions)
                {
                    return versions.TryGetValue(entityId, out var version) ? version : 0;
                }
            }
            catch { }
            return 0;
        }

        private List<FieldValue> ExtractFieldValues(Type componentType, object value)
        {
            var fields = new List<FieldValue>();
            if (value == null) return fields;

            try
            {
                foreach (var field in componentType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    fields.Add(new FieldValue
                    {
                        Name = field.Name,
                        FieldType = field.FieldType,
                        Value = field.GetValue(value)
                    });
                }
            }
            catch { }

            return fields;
        }

        private List<Models.SystemInfo> ExtractSystemInfo(World world)
        {
            var systems = new List<Models.SystemInfo>();

            try
            {
                // Access scheduler via reflection
                var schedulerField = typeof(World).GetField("_scheduler", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (schedulerField?.GetValue(world) is { } scheduler)
                {
                    // Try to get systems from scheduler
                    var systemsField = scheduler.GetType().GetField("_systems", 
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    if (systemsField?.GetValue(scheduler) is IEnumerable<object> systemList)
                    {
                        foreach (var system in systemList)
                        {
                            systems.Add(new Models.SystemInfo
                            {
                                SystemType = system.GetType(),
                                Name = system.GetType().Name,
                                Phase = Models.UpdatePhase.Update,
                                IsEnabled = true
                            });
                        }
                    }
                }
            }
            catch { }

            return systems;
        }
    }

    /// <summary>
    /// Extended interface for world data provider.
    /// </summary>
    public interface IWorldDataProvider : IEditorDataProvider<WorldSnapshot>
    {
        IEnumerable<int> GetEntityIds();
        IEnumerable<Type> GetComponentTypes();
        object GetComponentBoxed(int entityId, Type componentType);
        void SetComponentBoxed(int entityId, Type componentType, object value);
    }
}
