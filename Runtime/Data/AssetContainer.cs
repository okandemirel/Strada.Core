using System;
using UnityEngine;

namespace Strada.Core.Data
{
    public interface IAssetRef
    {
        string Guid { get; }
        bool IsValid { get; }
    }

    public interface IAssetRef<out T> : IAssetRef where T : ScriptableObject
    {
        T Asset { get; }
    }

    [Serializable]
    public struct AssetRef<T> : IAssetRef<T> where T : ScriptableObject
    {
        [SerializeField] private string _guid;
        [SerializeField] private T _asset;

        public string Guid => _guid;
        public T Asset => _asset;
        public bool IsValid => _asset != null;

        public AssetRef(T asset)
        {
            _asset = asset;
            _guid = asset != null ? System.Guid.NewGuid().ToString("N") : string.Empty;
        }

        public static implicit operator T(AssetRef<T> assetRef) => assetRef._asset;
        public static implicit operator AssetRef<T>(T asset) => new(asset);
    }

    public abstract class AssetContainer : ScriptableObject
    {
        [SerializeField, HideInInspector] private string _assetGuid;

        public string AssetGuid
        {
            get
            {
                if (string.IsNullOrEmpty(_assetGuid))
                    _assetGuid = System.Guid.NewGuid().ToString("N");
                return _assetGuid;
            }
        }

        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(_assetGuid))
                _assetGuid = System.Guid.NewGuid().ToString("N");
        }
    }

    public abstract class AssetContainer<TData> : AssetContainer where TData : class, new()
    {
        [SerializeField] private TData _data;

        public TData Data
        {
            get
            {
                _data ??= new TData();
                return _data;
            }
        }

        public void SetData(TData data) => _data = data;

        protected override void OnValidate()
        {
            base.OnValidate();
            _data ??= new TData();
        }
    }
}
