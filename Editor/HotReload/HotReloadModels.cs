using System;
using System.Collections.Generic;
using Strada.Core.Data;

namespace Strada.Core.Editor.HotReload
{
    /// <summary>
    /// Information about a detected config change.
    /// </summary>
    public class ConfigChangeInfo
    {
        /// <summary>
        /// The asset path of the changed config.
        /// </summary>
        public string AssetPath { get; set; }
        
        /// <summary>
        /// The config instance that was changed.
        /// </summary>
        public ConfigData Config { get; set; }
        
        /// <summary>
        /// The type of the config.
        /// </summary>
        public Type ConfigType { get; set; }
        
        /// <summary>
        /// When the change was detected.
        /// </summary>
        public DateTime Timestamp { get; set; }
    }
    
    /// <summary>
    /// Result of a hot reload operation.
    /// </summary>
    public class HotReloadResult
    {
        /// <summary>
        /// Whether the reload was successful.
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// The path of the reloaded config.
        /// </summary>
        public string ConfigPath { get; set; }
        
        /// <summary>
        /// The type of the reloaded config.
        /// </summary>
        public Type ConfigType { get; set; }
        
        /// <summary>
        /// When the reload completed.
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// Number of services that were notified.
        /// </summary>
        public int NotifiedServiceCount { get; set; }
        
        /// <summary>
        /// Any errors that occurred during reload.
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();
    }
    
    /// <summary>
    /// Current state of the hot reload system for dashboard display.
    /// </summary>
    public struct HotReloadState
    {
        /// <summary>
        /// When the last reload occurred.
        /// </summary>
        public DateTime LastReloadTime;
        
        /// <summary>
        /// The path of the last reloaded config.
        /// </summary>
        public string LastConfigPath;
        
        /// <summary>
        /// Whether the last reload was successful.
        /// </summary>
        public bool WasSuccessful;
        
        /// <summary>
        /// Error message if the last reload failed.
        /// </summary>
        public string ErrorMessage;
        
        /// <summary>
        /// Gets whether there has been any reload activity.
        /// </summary>
        public bool HasActivity => LastReloadTime != default;
    }
    
    /// <summary>
    /// Interface for services that depend on config data and need to be notified of changes.
    /// </summary>
    public interface IConfigDependentService
    {
        /// <summary>
        /// Called when a config this service depends on is reloaded.
        /// </summary>
        /// <param name="config">The reloaded config instance.</param>
        void OnConfigReloaded(ConfigData config);
    }
    
    /// <summary>
    /// Captured state of an entity for preservation during hot reload.
    /// </summary>
    public class EntityStateSnapshot
    {
        /// <summary>
        /// Entity index.
        /// </summary>
        public int EntityIndex { get; set; }

        /// <summary>
        /// Component data keyed by component type name.
        /// </summary>
        public Dictionary<string, object> Components { get; set; } = new Dictionary<string, object>();
    }
    
    /// <summary>
    /// Complete world state snapshot for hot reload preservation.
    /// </summary>
    public class WorldStateSnapshot
    {
        /// <summary>
        /// When the snapshot was taken.
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// All entity snapshots.
        /// </summary>
        public List<EntityStateSnapshot> Entities { get; set; } = new List<EntityStateSnapshot>();
    }
}
