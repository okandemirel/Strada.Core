using System;
using System.Collections.Generic;
using UnityEngine;

namespace Strada.Core.Data
{
    public interface IAssetDatabase
    {
        T Get<T>(string guid) where T : AssetContainer;
        bool TryGet<T>(string guid, out T asset) where T : AssetContainer;
        IEnumerable<T> GetAll<T>() where T : AssetContainer;
        void Register(AssetContainer asset);
        void Unregister(string guid);
        void Clear();
    }

    public sealed class RuntimeAssetDatabase : IAssetDatabase
    {
        private readonly Dictionary<string, AssetContainer> _assets = new();
        private readonly Dictionary<Type, List<AssetContainer>> _assetsByType = new();

        public T Get<T>(string guid) where T : AssetContainer
        {
            if (_assets.TryGetValue(guid, out var asset))
                return asset as T;

            throw new KeyNotFoundException($"Asset with GUID '{guid}' not found");
        }

        public bool TryGet<T>(string guid, out T asset) where T : AssetContainer
        {
            if (_assets.TryGetValue(guid, out var container) && container is T typed)
            {
                asset = typed;
                return true;
            }

            asset = null;
            return false;
        }

        public IEnumerable<T> GetAll<T>() where T : AssetContainer
        {
            var type = typeof(T);

            if (!_assetsByType.TryGetValue(type, out var list))
                yield break;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is T typed)
                    yield return typed;
            }
        }

        public void Register(AssetContainer asset)
        {
            if (asset == null)
                return;

            var guid = asset.AssetGuid;
            _assets[guid] = asset;

            var type = asset.GetType();
            if (!_assetsByType.TryGetValue(type, out var list))
            {
                list = new List<AssetContainer>();
                _assetsByType[type] = list;
            }

            if (!list.Contains(asset))
                list.Add(asset);
        }

        public void Unregister(string guid)
        {
            if (!_assets.TryGetValue(guid, out var asset))
                return;

            _assets.Remove(guid);

            var type = asset.GetType();
            if (_assetsByType.TryGetValue(type, out var list))
                list.Remove(asset);
        }

        public void Clear()
        {
            _assets.Clear();
            _assetsByType.Clear();
        }
    }

    [CreateAssetMenu(fileName = "AssetRegistry", menuName = "Strada/Core/Asset Registry")]
    public sealed class AssetRegistry : ScriptableObject
    {
        [SerializeField] private List<AssetContainer> _assets = new();

        public IReadOnlyList<AssetContainer> Assets => _assets;

        public void PopulateDatabase(IAssetDatabase database)
        {
            for (int i = 0; i < _assets.Count; i++)
            {
                if (_assets[i] != null)
                    database.Register(_assets[i]);
            }
        }

        public void Add(AssetContainer asset)
        {
            if (asset != null && !_assets.Contains(asset))
                _assets.Add(asset);
        }

        public void Remove(AssetContainer asset)
        {
            _assets.Remove(asset);
        }
    }
}
