using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Strada.Core.Data
{
    public sealed class ConfigDatabase : IDisposable
    {
        private readonly Dictionary<Type, ConfigData> _configsByType = new(32);
        private readonly Dictionary<string, ConfigData> _configsByGuid = new(32);
        private bool _disposed;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Register<T>(T config) where T : ConfigData
        {
            var type = typeof(T);
            _configsByType[type] = config;
            _configsByGuid[config.Guid] = config;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Register(ConfigData config)
        {
            var type = config.GetType();
            _configsByType[type] = config;
            _configsByGuid[config.Guid] = config;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get<T>() where T : ConfigData
        {
            return _configsByType.TryGetValue(typeof(T), out var config) ? (T)config : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet<T>(out T config) where T : ConfigData
        {
            if (_configsByType.TryGetValue(typeof(T), out var c))
            {
                config = (T)c;
                return true;
            }
            config = null;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ConfigData GetByGuid(string guid)
        {
            return _configsByGuid.TryGetValue(guid, out var config) ? config : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetByGuid<T>(string guid) where T : ConfigData
        {
            return _configsByGuid.TryGetValue(guid, out var config) ? config as T : null;
        }

        public TData GetData<TConfig, TData>()
            where TConfig : ConfigData<TData>
            where TData : class, new()
        {
            var config = Get<TConfig>();
            return config?.Data;
        }

        public void Clear()
        {
            _configsByType.Clear();
            _configsByGuid.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Clear();
        }
    }

    [CreateAssetMenu(fileName = "ConfigDatabaseAsset", menuName = "Strada/Config Database")]
    public sealed class ConfigDatabaseAsset : ScriptableObject
    {
        [SerializeField] private List<ConfigData> _configs = new();

        public IReadOnlyList<ConfigData> Configs => _configs;

        public void Add(ConfigData config)
        {
            if (!_configs.Contains(config))
                _configs.Add(config);
        }

        public void Remove(ConfigData config)
        {
            _configs.Remove(config);
        }

        public ConfigDatabase CreateDatabase()
        {
            var database = new ConfigDatabase();
            foreach (var config in _configs)
            {
                if (config != null)
                    database.Register(config);
            }
            return database;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _configs.RemoveAll(c => c == null);
        }
#endif
    }
}
