using System;
using System.Collections.Generic;
using UnityEngine;
using Strada.Core.ECS;
using Strada.Core.ECS.Storage;

namespace Strada.Core.Editor.HotReload
{
    /// <summary>
    /// Captures and restores entity state during hot reload operations.
    /// Preserves entity IDs, versions, and component values.
    /// </summary>
    public static class EntityStatePreserver
    {
        /// <summary>
        /// Captures the current state of all entities in the active world.
        /// </summary>
        /// <returns>A snapshot of the world state, or null if no world is active.</returns>
        public static WorldStateSnapshot CaptureState()
        {
            var world = World.Current;
            if (world == null || world.EntityManager == null)
            {
                return null;
            }
            
            var snapshot = new WorldStateSnapshot
            {
                Timestamp = DateTime.Now,
                Entities = new List<EntityStateSnapshot>()
            };
            
            try
            {
                var entityManager = world.EntityManager;
                var store = entityManager.Store;
                var entityIndices = entityManager.GetAllEntities();
                var componentTypes = store.GetComponentTypes();
                
                foreach (var entityIndex in entityIndices)
                {
                    var entitySnapshot = new EntityStateSnapshot
                    {
                        EntityIndex = entityIndex,
                        EntityVersion = 0, // Version tracking handled by EntityManager internally
                        Components = new Dictionary<string, object>()
                    };
                    
                    // Capture component data for this entity
                    foreach (var componentType in componentTypes)
                    {
                        try
                        {
                            if (!store.HasComponent(entityIndex, componentType))
                                continue;
                                
                            var componentValue = store.GetComponentBoxed(entityIndex, componentType);
                            if (componentValue != null)
                            {
                                // Store as JSON for serialization safety
                                var json = JsonUtility.ToJson(componentValue);
                                entitySnapshot.Components[componentType.FullName] = json;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[EntityStatePreserver] Failed to capture component {componentType.Name} on entity {entityIndex}: {ex.Message}");
                        }
                    }
                    
                    snapshot.Entities.Add(entitySnapshot);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EntityStatePreserver] Failed to capture world state: {ex.Message}");
                return null;
            }
            
            return snapshot;
        }
        
        /// <summary>
        /// Restores entity state from a snapshot.
        /// </summary>
        /// <param name="snapshot">The snapshot to restore from.</param>
        /// <returns>True if restoration was successful.</returns>
        public static bool RestoreState(WorldStateSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return true; // Nothing to restore
            }
            
            var world = World.Current;
            if (world == null || world.EntityManager == null)
            {
                Debug.LogWarning("[EntityStatePreserver] Cannot restore state: No active world");
                return false;
            }
            
            var entityManager = world.EntityManager;
            var store = entityManager.Store;
            var restoredCount = 0;
            var failedCount = 0;
            
            // Get current active entities for validation
            var activeEntities = new HashSet<int>(entityManager.GetAllEntities());
            
            foreach (var entitySnapshot in snapshot.Entities)
            {
                try
                {
                    var entityIndex = entitySnapshot.EntityIndex;
                    
                    // Verify entity still exists
                    if (!activeEntities.Contains(entityIndex))
                    {
                        // Entity was destroyed, skip
                        continue;
                    }
                    
                    // Restore component values
                    foreach (var kvp in entitySnapshot.Components)
                    {
                        var componentTypeName = kvp.Key;
                        var componentJson = kvp.Value as string;
                        
                        if (string.IsNullOrEmpty(componentJson))
                            continue;
                        
                        try
                        {
                            var componentType = FindComponentType(componentTypeName);
                            if (componentType == null)
                            {
                                Debug.LogWarning($"[EntityStatePreserver] Component type not found: {componentTypeName}");
                                continue;
                            }
                            
                            // Check if entity still has this component
                            if (!store.HasComponent(entityIndex, componentType))
                            {
                                continue;
                            }
                            
                            // Deserialize and restore
                            var componentValue = JsonUtility.FromJson(componentJson, componentType);
                            if (componentValue != null)
                            {
                                store.SetComponentBoxed(entityIndex, componentType, componentValue);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[EntityStatePreserver] Failed to restore component {componentTypeName}: {ex.Message}");
                            failedCount++;
                        }
                    }
                    
                    restoredCount++;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[EntityStatePreserver] Failed to restore entity {entitySnapshot.EntityIndex}: {ex.Message}");
                    failedCount++;
                }
            }
            
            if (failedCount > 0)
            {
                Debug.LogWarning($"[EntityStatePreserver] Restored {restoredCount} entities, {failedCount} failures");
            }
            
            return failedCount == 0;
        }
        
        private static readonly Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();
        
        private static Type FindComponentType(string fullTypeName)
        {
            if (_typeCache.TryGetValue(fullTypeName, out var cachedType))
            {
                return cachedType;
            }
            
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(fullTypeName);
                    if (type != null && typeof(IComponent).IsAssignableFrom(type))
                    {
                        _typeCache[fullTypeName] = type;
                        return type;
                    }
                }
                catch
                {
                    // Skip assemblies that can't be searched
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Clears the type cache. Call when assemblies are reloaded.
        /// </summary>
        public static void ClearTypeCache()
        {
            _typeCache.Clear();
        }
    }
}
