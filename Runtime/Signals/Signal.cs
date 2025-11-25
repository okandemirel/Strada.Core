using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Strada.Core.Signals
{
    public interface ISignal
    {
        int ListenerCount { get; }
        void RemoveAllListeners();
    }

    public sealed class Signal : ISignal
    {
        private readonly List<Action> _listeners = new(8);
        private readonly List<Action> _onceListeners = new(4);
        private bool _dispatching;
        private readonly List<Action> _pendingAdd = new(4);
        private readonly List<Action> _pendingRemove = new(4);

        public int ListenerCount => _listeners.Count + _onceListeners.Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddListener(Action listener)
        {
            if (_dispatching)
            {
                _pendingAdd.Add(listener);
                return;
            }

            _listeners.Add(listener);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOnce(Action listener)
        {
            _onceListeners.Add(listener);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveListener(Action listener)
        {
            if (_dispatching)
            {
                _pendingRemove.Add(listener);
                return;
            }

            _listeners.Remove(listener);
        }

        public void RemoveAllListeners()
        {
            _listeners.Clear();
            _onceListeners.Clear();
            _pendingAdd.Clear();
            _pendingRemove.Clear();
        }

        public void Dispatch()
        {
            _dispatching = true;

            for (int i = 0; i < _listeners.Count; i++)
                _listeners[i]();

            for (int i = 0; i < _onceListeners.Count; i++)
                _onceListeners[i]();

            _onceListeners.Clear();
            _dispatching = false;

            ProcessPending();
        }

        private void ProcessPending()
        {
            for (int i = 0; i < _pendingAdd.Count; i++)
                _listeners.Add(_pendingAdd[i]);

            for (int i = 0; i < _pendingRemove.Count; i++)
                _listeners.Remove(_pendingRemove[i]);

            _pendingAdd.Clear();
            _pendingRemove.Clear();
        }
    }

    public sealed class Signal<T> : ISignal
    {
        private readonly List<Action<T>> _listeners = new(8);
        private readonly List<Action<T>> _onceListeners = new(4);
        private bool _dispatching;
        private readonly List<Action<T>> _pendingAdd = new(4);
        private readonly List<Action<T>> _pendingRemove = new(4);

        public int ListenerCount => _listeners.Count + _onceListeners.Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddListener(Action<T> listener)
        {
            if (_dispatching)
            {
                _pendingAdd.Add(listener);
                return;
            }

            _listeners.Add(listener);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOnce(Action<T> listener)
        {
            _onceListeners.Add(listener);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveListener(Action<T> listener)
        {
            if (_dispatching)
            {
                _pendingRemove.Add(listener);
                return;
            }

            _listeners.Remove(listener);
        }

        public void RemoveAllListeners()
        {
            _listeners.Clear();
            _onceListeners.Clear();
            _pendingAdd.Clear();
            _pendingRemove.Clear();
        }

        public void Dispatch(T arg)
        {
            _dispatching = true;

            for (int i = 0; i < _listeners.Count; i++)
                _listeners[i](arg);

            for (int i = 0; i < _onceListeners.Count; i++)
                _onceListeners[i](arg);

            _onceListeners.Clear();
            _dispatching = false;

            ProcessPending();
        }

        private void ProcessPending()
        {
            for (int i = 0; i < _pendingAdd.Count; i++)
                _listeners.Add(_pendingAdd[i]);

            for (int i = 0; i < _pendingRemove.Count; i++)
                _listeners.Remove(_pendingRemove[i]);

            _pendingAdd.Clear();
            _pendingRemove.Clear();
        }
    }

    public sealed class Signal<T1, T2> : ISignal
    {
        private readonly List<Action<T1, T2>> _listeners = new(8);
        private readonly List<Action<T1, T2>> _onceListeners = new(4);
        private bool _dispatching;
        private readonly List<Action<T1, T2>> _pendingAdd = new(4);
        private readonly List<Action<T1, T2>> _pendingRemove = new(4);

        public int ListenerCount => _listeners.Count + _onceListeners.Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddListener(Action<T1, T2> listener)
        {
            if (_dispatching)
            {
                _pendingAdd.Add(listener);
                return;
            }

            _listeners.Add(listener);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOnce(Action<T1, T2> listener)
        {
            _onceListeners.Add(listener);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveListener(Action<T1, T2> listener)
        {
            if (_dispatching)
            {
                _pendingRemove.Add(listener);
                return;
            }

            _listeners.Remove(listener);
        }

        public void RemoveAllListeners()
        {
            _listeners.Clear();
            _onceListeners.Clear();
            _pendingAdd.Clear();
            _pendingRemove.Clear();
        }

        public void Dispatch(T1 arg1, T2 arg2)
        {
            _dispatching = true;

            for (int i = 0; i < _listeners.Count; i++)
                _listeners[i](arg1, arg2);

            for (int i = 0; i < _onceListeners.Count; i++)
                _onceListeners[i](arg1, arg2);

            _onceListeners.Clear();
            _dispatching = false;

            ProcessPending();
        }

        private void ProcessPending()
        {
            for (int i = 0; i < _pendingAdd.Count; i++)
                _listeners.Add(_pendingAdd[i]);

            for (int i = 0; i < _pendingRemove.Count; i++)
                _listeners.Remove(_pendingRemove[i]);

            _pendingAdd.Clear();
            _pendingRemove.Clear();
        }
    }

    public sealed class Signal<T1, T2, T3> : ISignal
    {
        private readonly List<Action<T1, T2, T3>> _listeners = new(8);
        private readonly List<Action<T1, T2, T3>> _onceListeners = new(4);
        private bool _dispatching;
        private readonly List<Action<T1, T2, T3>> _pendingAdd = new(4);
        private readonly List<Action<T1, T2, T3>> _pendingRemove = new(4);

        public int ListenerCount => _listeners.Count + _onceListeners.Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddListener(Action<T1, T2, T3> listener)
        {
            if (_dispatching)
            {
                _pendingAdd.Add(listener);
                return;
            }

            _listeners.Add(listener);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOnce(Action<T1, T2, T3> listener)
        {
            _onceListeners.Add(listener);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveListener(Action<T1, T2, T3> listener)
        {
            if (_dispatching)
            {
                _pendingRemove.Add(listener);
                return;
            }

            _listeners.Remove(listener);
        }

        public void RemoveAllListeners()
        {
            _listeners.Clear();
            _onceListeners.Clear();
            _pendingAdd.Clear();
            _pendingRemove.Clear();
        }

        public void Dispatch(T1 arg1, T2 arg2, T3 arg3)
        {
            _dispatching = true;

            for (int i = 0; i < _listeners.Count; i++)
                _listeners[i](arg1, arg2, arg3);

            for (int i = 0; i < _onceListeners.Count; i++)
                _onceListeners[i](arg1, arg2, arg3);

            _onceListeners.Clear();
            _dispatching = false;

            ProcessPending();
        }

        private void ProcessPending()
        {
            for (int i = 0; i < _pendingAdd.Count; i++)
                _listeners.Add(_pendingAdd[i]);

            for (int i = 0; i < _pendingRemove.Count; i++)
                _listeners.Remove(_pendingRemove[i]);

            _pendingAdd.Clear();
            _pendingRemove.Clear();
        }
    }
}
