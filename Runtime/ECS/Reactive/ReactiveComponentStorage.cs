using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Strada.Core.ECS.Storage;
using Unity.Collections;
using UnityEngine;

namespace Strada.Core.ECS.Reactive
{
    internal interface IReactiveStorage
    {
        bool Remove(int entityIndex);
    }

    public sealed class ReactiveComponentStorage<T> : IDisposable, IReactiveStorage where T : unmanaged, IComponent
    {
        private const int MaxNotifyDepth = 8;

        private readonly ComponentStorage<T> _storage;
        private readonly List<Action<int, T>> _onAddCallbacks = new(4);
        private readonly List<Action<int, T>> _onRemoveCallbacks = new(4);
        private readonly List<Action<int, T, T>> _onChangeCallbacks = new(4);
        private int _notifyDepth;

        public ComponentStorage<T> Storage => _storage;
        public int Count => _storage.Count;

        public ReactiveComponentStorage(int sparseCapacity = 1024, int denseCapacity = 256)
        {
            _storage = new ComponentStorage<T>(sparseCapacity, denseCapacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SubscribeOnAdd(Action<int, T> callback) => _onAddCallbacks.Add(callback);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SubscribeOnRemove(Action<int, T> callback) => _onRemoveCallbacks.Add(callback);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SubscribeOnChange(Action<int, T, T> callback) => _onChangeCallbacks.Add(callback);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnsubscribeOnAdd(Action<int, T> callback) => _onAddCallbacks.Remove(callback);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnsubscribeOnRemove(Action<int, T> callback) => _onRemoveCallbacks.Remove(callback);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnsubscribeOnChange(Action<int, T, T> callback) => _onChangeCallbacks.Remove(callback);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(int entityIndex, T component)
        {
            bool isNew = !_storage.Contains(entityIndex);
            _storage.Add(entityIndex, component);

            if (isNew)
                NotifyAdd(entityIndex, component);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(int entityIndex)
        {
            if (!_storage.Contains(entityIndex))
                return false;

            var component = _storage.Get(entityIndex);
            NotifyRemove(entityIndex, component);
            return _storage.Remove(entityIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int entityIndex, T component)
        {
            if (!_storage.Contains(entityIndex))
            {
                Add(entityIndex, component);
                return;
            }

            var oldValue = _storage.Get(entityIndex);
            _storage.Set(entityIndex, component);
            NotifyChange(entityIndex, oldValue, component);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get(int entityIndex) => _storage.Get(entityIndex);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet(int entityIndex, out T component) => _storage.TryGet(entityIndex, out component);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(int entityIndex) => _storage.Contains(entityIndex);

        private void NotifyAdd(int entityIndex, T component)
        {
            if (_notifyDepth >= MaxNotifyDepth)
            {
                Debug.LogError($"[ReactiveComponentStorage<{typeof(T).Name}>] Max notify depth ({MaxNotifyDepth}) exceeded in OnAdd. Aborting to prevent stack overflow.");
                return;
            }

            _notifyDepth++;
            try
            {
                var snapshot = _onAddCallbacks.ToArray();
                foreach (var callback in snapshot)
                {
                    try { callback(entityIndex, component); }
                    catch (Exception ex) { Debug.LogError($"[ReactiveComponentStorage<{typeof(T).Name}>] Exception in OnAdd callback: {ex}"); }
                }
            }
            finally { _notifyDepth--; }
        }

        private void NotifyRemove(int entityIndex, T component)
        {
            if (_notifyDepth >= MaxNotifyDepth)
            {
                Debug.LogError($"[ReactiveComponentStorage<{typeof(T).Name}>] Max notify depth ({MaxNotifyDepth}) exceeded in OnRemove. Aborting to prevent stack overflow.");
                return;
            }

            _notifyDepth++;
            try
            {
                var snapshot = _onRemoveCallbacks.ToArray();
                foreach (var callback in snapshot)
                {
                    try { callback(entityIndex, component); }
                    catch (Exception ex) { Debug.LogError($"[ReactiveComponentStorage<{typeof(T).Name}>] Exception in OnRemove callback: {ex}"); }
                }
            }
            finally { _notifyDepth--; }
        }

        private void NotifyChange(int entityIndex, T oldValue, T newValue)
        {
            if (_notifyDepth >= MaxNotifyDepth)
            {
                Debug.LogError($"[ReactiveComponentStorage<{typeof(T).Name}>] Max notify depth ({MaxNotifyDepth}) exceeded in OnChange. Aborting to prevent stack overflow.");
                return;
            }

            _notifyDepth++;
            try
            {
                var snapshot = _onChangeCallbacks.ToArray();
                foreach (var callback in snapshot)
                {
                    try { callback(entityIndex, oldValue, newValue); }
                    catch (Exception ex) { Debug.LogError($"[ReactiveComponentStorage<{typeof(T).Name}>] Exception in OnChange callback: {ex}"); }
                }
            }
            finally { _notifyDepth--; }
        }

        public void Clear()
        {
            _storage.Clear();
        }

        public void Dispose()
        {
            _storage.Dispose();
            _onAddCallbacks.Clear();
            _onRemoveCallbacks.Clear();
            _onChangeCallbacks.Clear();
        }
    }
}
