using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Strada.Core.DI;
using Strada.Core.ECS;
using Strada.Core.MVCS;
using Strada.Core.Pooling;

namespace Strada.Core.Bridge
{
    public interface IMediatorRegistry : IDisposable
    {
        TMediator Create<TMediator, TView>(Entity entity, TView view)
            where TMediator : ViewMediator<TView>, new()
            where TView : StradaView;
        void Release<TMediator, TView>(TMediator mediator)
            where TMediator : ViewMediator<TView>, new()
            where TView : StradaView;
        void SyncAll();
        void ReleaseAll();
    }

    public sealed class MediatorRegistry : IMediatorRegistry
    {
        private readonly IContainer _container;
        private readonly List<IDisposable> _activeMediators = new(64);
        private bool _disposed;

        public int ActiveCount => _activeMediators.Count;

        public MediatorRegistry(IContainer container)
        {
            _container = container;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TMediator Create<TMediator, TView>(Entity entity, TView view)
            where TMediator : ViewMediator<TView>, new()
            where TView : StradaView
        {
            var mediator = MediatorPool<TMediator, TView>.Instance.Rent();
            mediator.Initialize(_container);
            mediator.Bind(entity, view);
            _activeMediators.Add(mediator);
            return mediator;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Release<TMediator, TView>(TMediator mediator)
            where TMediator : ViewMediator<TView>, new()
            where TView : StradaView
        {
            mediator.Unbind();
            _activeMediators.Remove(mediator);
            MediatorPool<TMediator, TView>.Instance.Return(mediator);
        }

        public void SyncAll()
        {
            for (int i = 0; i < _activeMediators.Count; i++)
            {
                if (_activeMediators[i] is ISyncable syncable)
                    syncable.SyncBindings();
            }
        }

        public void ReleaseAll()
        {
            for (int i = _activeMediators.Count - 1; i >= 0; i--)
            {
                _activeMediators[i].Dispose();
            }
            _activeMediators.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ReleaseAll();
        }

        private interface ISyncable
        {
            void SyncBindings();
        }
    }

    internal static class MediatorPool<TMediator, TView>
        where TMediator : ViewMediator<TView>, new()
        where TView : StradaView
    {
        private static MediatorPoolInstance _instance;
        public static MediatorPoolInstance Instance => _instance ??= new MediatorPoolInstance();

        internal sealed class MediatorPoolInstance
        {
            private readonly Stack<TMediator> _available = new(16);
            private readonly object _lock = new();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public TMediator Rent()
            {
                lock (_lock)
                {
                    return _available.Count > 0 ? _available.Pop() : new TMediator();
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Return(TMediator mediator)
            {
                lock (_lock)
                {
                    _available.Push(mediator);
                }
            }
        }
    }
}
