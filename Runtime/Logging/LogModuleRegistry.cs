using System;
using System.Collections.Generic;
using System.Reflection;

namespace Strada.Core.Logging
{
    /// <summary>
    /// Runtime registry for log modules. Manages module metadata, tier information,
    /// and dynamic registration of game modules.
    /// </summary>
    public static class LogModuleRegistry
    {
        private const int GameModuleStartId = 1000;
        private const int StradaModuleStartId = 100;

        private static readonly Dictionary<LogModule, ModuleInfo> _moduleInfos = new();
        private static readonly Dictionary<string, LogModule> _gameModulesByName = new();
        private static int _nextGameModuleId = GameModuleStartId;
        private static bool _initialized;

        /// <summary>
        /// Information about a registered log module.
        /// </summary>
        public readonly struct ModuleInfo
        {
            public readonly LogModuleTier Tier;
            public readonly bool DefaultVisible;
            public readonly string DisplayName;

            public ModuleInfo(LogModuleTier tier, bool defaultVisible, string displayName)
            {
                Tier = tier;
                DefaultVisible = defaultVisible;
                DisplayName = displayName;
            }
        }

        /// <summary>
        /// Ensures the registry is initialized with enum-defined modules.
        /// </summary>
        public static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            foreach (LogModule module in Enum.GetValues(typeof(LogModule)))
            {
                var field = typeof(LogModule).GetField(module.ToString());
                if (field == null) continue;

                var attr = field.GetCustomAttribute<LogModuleInfoAttribute>();
                var tier = attr?.Tier ?? GetDefaultTier((int)module);
                var defaultVisible = attr?.DefaultVisible ?? true;
                var displayName = attr?.DisplayName ?? module.ToString();

                _moduleInfos[module] = new ModuleInfo(tier, defaultVisible, displayName);
            }
        }

        /// <summary>
        /// Registers a game module by name. Returns an existing module if already registered,
        /// otherwise creates a new one with an auto-assigned ID starting from 1000.
        /// </summary>
        /// <param name="name">The name of the game module.</param>
        /// <returns>The LogModule value for this game module.</returns>
        public static LogModule RegisterGameModule(string name)
        {
            EnsureInitialized();

            if (string.IsNullOrEmpty(name))
            {
                return LogModule.General;
            }

            if (_gameModulesByName.TryGetValue(name, out var existing))
            {
                return existing;
            }

            var module = (LogModule)_nextGameModuleId++;
            _gameModulesByName[name] = module;
            _moduleInfos[module] = new ModuleInfo(LogModuleTier.Game, true, name);

            return module;
        }

        /// <summary>
        /// Gets the tier for a module.
        /// </summary>
        public static LogModuleTier GetTier(LogModule module)
        {
            EnsureInitialized();

            if (_moduleInfos.TryGetValue(module, out var info))
            {
                return info.Tier;
            }

            return GetDefaultTier((int)module);
        }

        /// <summary>
        /// Returns true if visibility is locked for this module (Core and StradaModule tiers).
        /// </summary>
        public static bool IsVisibilityLocked(LogModule module)
        {
            var tier = GetTier(module);
            return tier == LogModuleTier.StradaCore || tier == LogModuleTier.StradaModule;
        }

        /// <summary>
        /// Gets the display name for a module.
        /// </summary>
        public static string GetDisplayName(LogModule module)
        {
            EnsureInitialized();

            if (_moduleInfos.TryGetValue(module, out var info))
            {
                return info.DisplayName;
            }

            if (Enum.IsDefined(typeof(LogModule), module))
            {
                return module.ToString();
            }

            return $"Module_{(int)module}";
        }

        /// <summary>
        /// Gets the default visibility for a module.
        /// </summary>
        public static bool GetDefaultVisible(LogModule module)
        {
            EnsureInitialized();

            if (_moduleInfos.TryGetValue(module, out var info))
            {
                return info.DefaultVisible;
            }

            return true;
        }

        /// <summary>
        /// Gets all registered modules for a specific tier.
        /// </summary>
        public static List<LogModule> GetModulesByTier(LogModuleTier tier)
        {
            EnsureInitialized();

            var result = new List<LogModule>();
            foreach (var kvp in _moduleInfos)
            {
                if (kvp.Value.Tier == tier)
                {
                    result.Add(kvp.Key);
                }
            }

            result.Sort((a, b) => ((int)a).CompareTo((int)b));
            return result;
        }

        /// <summary>
        /// Gets all registered modules.
        /// </summary>
        public static List<LogModule> GetAllModules()
        {
            EnsureInitialized();

            var result = new List<LogModule>(_moduleInfos.Keys);
            result.Sort((a, b) => ((int)a).CompareTo((int)b));
            return result;
        }

        /// <summary>
        /// Checks if a module is registered.
        /// </summary>
        public static bool IsRegistered(LogModule module)
        {
            EnsureInitialized();
            return _moduleInfos.ContainsKey(module);
        }

        /// <summary>
        /// Gets the module info if available.
        /// </summary>
        public static bool TryGetModuleInfo(LogModule module, out ModuleInfo info)
        {
            EnsureInitialized();
            return _moduleInfos.TryGetValue(module, out info);
        }

        /// <summary>
        /// Registers a module with explicit info. Used for dynamically discovered modules
        /// that aren't defined in the enum but exist in settings.
        /// </summary>
        internal static void RegisterModule(LogModule module, LogModuleTier tier, string displayName)
        {
            EnsureInitialized();

            if (!_moduleInfos.ContainsKey(module))
            {
                _moduleInfos[module] = new ModuleInfo(tier, true, displayName);
            }
        }

        private static LogModuleTier GetDefaultTier(int moduleId)
        {
            if (moduleId < StradaModuleStartId)
                return LogModuleTier.StradaCore;
            if (moduleId < GameModuleStartId)
                return LogModuleTier.StradaModule;
            return LogModuleTier.Game;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Resets the registry. Used for testing or editor refresh.
        /// </summary>
        internal static void Reset()
        {
            _moduleInfos.Clear();
            _gameModulesByName.Clear();
            _nextGameModuleId = GameModuleStartId;
            _initialized = false;
        }
#endif
    }
}
