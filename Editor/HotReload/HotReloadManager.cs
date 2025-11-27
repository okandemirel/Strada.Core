using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Strada.Core.Data;
using Strada.Core.Bootstrap;

namespace Strada.Core.Editor.HotReload
{
    /// <summary>
    /// Manages hot reload functionality for CD_ config assets during Play Mode.
    /// Monitors config changes, tracks dependent services, and coordinates reload operations.
    /// </summary>
    [InitializeOnLoad]
    public static class HotReloadManager
    {
        private const string EnabledPrefKey = "Strada.HotReload.Enabled";
        private const string NotificationsEnabledPrefKey = "Strada.HotReload.NotificationsEnabled";
        
        private static readonly Queue<ConfigChangeInfo> _pendingChanges = new Queue<ConfigChangeInfo>();
        private static readonly Dictionary<string, ConfigData> _configCache = new Dictionary<string, ConfigData>();
        private static readonly Dictionary<Type, List<IConfigDependentService>> _dependentServices = new Dictionary<Type, List<IConfigDependentService>>();
        private static readonly Dictionary<string, object> _previousConfigStates = new Dictionary<string, object>();
        
        private static bool _isProcessing;
        private static HotReloadState _lastReloadState;
        
        /// <summary>
        /// Event raised when a hot reload operation completes.
        /// </summary>
        public static event Action<HotReloadResult> OnHotReloadComplete;
        
        /// <summary>
        /// Event raised when a config change is detected.
        /// </summary>
        public static event Action<ConfigChangeInfo> OnConfigChangeDetected;
        
        /// <summary>
        /// Gets or sets whether hot reload is enabled.
        /// </summary>
        public static bool IsEnabled
        {
            get => EditorPrefs.GetBool(EnabledPrefKey, true);
            set => EditorPrefs.SetBool(EnabledPrefKey, value);
        }
        
        /// <summary>
        /// Gets or sets whether notifications are shown on reload.
        /// </summary>
        public static bool NotificationsEnabled
        {
            get => EditorPrefs.GetBool(NotificationsEnabledPrefKey, true);
            set => EditorPrefs.SetBool(NotificationsEnabledPrefKey, value);
        }
        
        /// <summary>
        /// Gets the last reload state for dashboard display.
        /// </summary>
        public static HotReloadState LastReloadState => _lastReloadState;
        
        /// <summary>
        /// Gets whether a reload is currently in progress.
        /// </summary>
        public static bool IsProcessing => _isProcessing;
        
        /// <summary>
        /// Gets the number of pending config changes.
        /// </summary>
        public static int PendingChangeCount => _pendingChanges.Count;
        
        static HotReloadManager()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += ProcessPendingChanges;
        }
        
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    CacheCurrentConfigs();
                    break;
                    
                case PlayModeStateChange.ExitingPlayMode:
                    ClearCaches();
                    break;
            }
        }
        
        /// <summary>
        /// Queues a config change for processing.
        /// Called by ConfigAssetPostprocessor when a CD_ asset is modified.
        /// </summary>
        public static void QueueConfigChange(string assetPath, ConfigData config)
        {
            if (!IsEnabled || !Application.isPlaying)
                return;
                
            var changeInfo = new ConfigChangeInfo
            {
                AssetPath = assetPath,
                Config = config,
                ConfigType = config?.GetType(),
                Timestamp = DateTime.Now
            };
            
            _pendingChanges.Enqueue(changeInfo);
            OnConfigChangeDetected?.Invoke(changeInfo);
            
            if (NotificationsEnabled)
            {
                Debug.Log($"[HotReload] Config change detected: {assetPath}");
            }
        }
        
        /// <summary>
        /// Registers a service as dependent on a specific config type.
        /// </summary>
        public static void RegisterDependentService<TConfig>(IConfigDependentService service) where TConfig : ConfigData
        {
            var configType = typeof(TConfig);
            if (!_dependentServices.TryGetValue(configType, out var services))
            {
                services = new List<IConfigDependentService>();
                _dependentServices[configType] = services;
            }
            
            if (!services.Contains(service))
            {
                services.Add(service);
            }
        }
        
        /// <summary>
        /// Unregisters a service from config dependency tracking.
        /// </summary>
        public static void UnregisterDependentService<TConfig>(IConfigDependentService service) where TConfig : ConfigData
        {
            var configType = typeof(TConfig);
            if (_dependentServices.TryGetValue(configType, out var services))
            {
                services.Remove(service);
            }
        }
        
        /// <summary>
        /// Gets all services dependent on a specific config type.
        /// </summary>
        public static IReadOnlyList<IConfigDependentService> GetDependentServices(Type configType)
        {
            if (_dependentServices.TryGetValue(configType, out var services))
            {
                return services;
            }
            return Array.Empty<IConfigDependentService>();
        }
        
        /// <summary>
        /// Gets all tracked config types with their dependent service counts.
        /// </summary>
        public static Dictionary<Type, int> GetDependencyMap()
        {
            var map = new Dictionary<Type, int>();
            foreach (var kvp in _dependentServices)
            {
                map[kvp.Key] = kvp.Value.Count;
            }
            return map;
        }
        
        private static void ProcessPendingChanges()
        {
            if (!Application.isPlaying || !IsEnabled || _isProcessing || _pendingChanges.Count == 0)
                return;
                
            _isProcessing = true;
            
            try
            {
                while (_pendingChanges.Count > 0)
                {
                    var change = _pendingChanges.Dequeue();
                    ProcessConfigChange(change);
                }
            }
            finally
            {
                _isProcessing = false;
            }
        }
        
        private static void ProcessConfigChange(ConfigChangeInfo change)
        {
            var result = new HotReloadResult
            {
                ConfigPath = change.AssetPath,
                ConfigType = change.ConfigType,
                Timestamp = DateTime.Now
            };
            
            try
            {
                // Capture entity state before reload
                var entityState = EntityStatePreserver.CaptureState();
                
                // Store previous config state for potential rollback
                var configGuid = change.Config?.Guid ?? change.AssetPath;
                _previousConfigStates[configGuid] = CaptureConfigState(change.Config);
                
                // Notify dependent services
                var dependentServices = GetDependentServices(change.ConfigType);
                result.NotifiedServiceCount = dependentServices.Count;
                
                foreach (var service in dependentServices)
                {
                    try
                    {
                        service.OnConfigReloaded(change.Config);
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Service {service.GetType().Name}: {ex.Message}");
                    }
                }
                
                // Restore entity state after reload
                EntityStatePreserver.RestoreState(entityState);
                
                result.Success = result.Errors.Count == 0;
                
                _lastReloadState = new HotReloadState
                {
                    LastReloadTime = result.Timestamp,
                    LastConfigPath = result.ConfigPath,
                    WasSuccessful = result.Success,
                    ErrorMessage = result.Success ? null : string.Join("; ", result.Errors)
                };
                
                if (NotificationsEnabled)
                {
                    if (result.Success)
                    {
                        Debug.Log($"[HotReload] Successfully reloaded: {change.AssetPath} ({result.NotifiedServiceCount} services notified)");
                    }
                    else
                    {
                        Debug.LogWarning($"[HotReload] Reload completed with errors: {change.AssetPath}\n{string.Join("\n", result.Errors)}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add(ex.Message);
                
                // Attempt rollback
                TryRollback(change);
                
                _lastReloadState = new HotReloadState
                {
                    LastReloadTime = DateTime.Now,
                    LastConfigPath = change.AssetPath,
                    WasSuccessful = false,
                    ErrorMessage = ex.Message
                };
                
                Debug.LogError($"[HotReload] Failed to reload {change.AssetPath}: {ex.Message}");
            }
            
            OnHotReloadComplete?.Invoke(result);
        }
        
        private static void TryRollback(ConfigChangeInfo change)
        {
            var configGuid = change.Config?.Guid ?? change.AssetPath;
            
            if (_previousConfigStates.TryGetValue(configGuid, out var previousState))
            {
                try
                {
                    RestoreConfigState(change.Config, previousState);
                    
                    if (NotificationsEnabled)
                    {
                        Debug.Log($"[HotReload] Rolled back config: {change.AssetPath}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[HotReload] Rollback failed for {change.AssetPath}: {ex.Message}");
                }
            }
        }
        
        private static object CaptureConfigState(ConfigData config)
        {
            if (config == null) return null;
            
            // Use JSON serialization to capture state
            return JsonUtility.ToJson(config);
        }
        
        private static void RestoreConfigState(ConfigData config, object state)
        {
            if (config == null || state == null) return;
            
            var json = state as string;
            if (!string.IsNullOrEmpty(json))
            {
                JsonUtility.FromJsonOverwrite(json, config);
            }
        }
        
        private static void CacheCurrentConfigs()
        {
            _configCache.Clear();
            
            var guids = AssetDatabase.FindAssets("t:ScriptableObject");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                
                if (asset is ConfigData config && asset.name.StartsWith("CD_"))
                {
                    _configCache[path] = config;
                }
            }
        }
        
        private static void ClearCaches()
        {
            _configCache.Clear();
            _previousConfigStates.Clear();
            _pendingChanges.Clear();
            _lastReloadState = default;
        }
        
        /// <summary>
        /// Manually triggers a reload for a specific config.
        /// </summary>
        public static void ForceReload(ConfigData config)
        {
            if (config == null) return;
            
            var path = AssetDatabase.GetAssetPath(config);
            QueueConfigChange(path, config);
        }
        
        /// <summary>
        /// Clears all pending changes without processing them.
        /// </summary>
        public static void ClearPendingChanges()
        {
            _pendingChanges.Clear();
        }
    }
}