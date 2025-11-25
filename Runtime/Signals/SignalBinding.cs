using System;

namespace Strada.Core.Signals
{
    public readonly struct SignalBinding<T> : IDisposable where T : struct, IStructSignal
    {
        private readonly SignalBus _bus;
        private readonly Action<T> _handler;

        internal SignalBinding(SignalBus bus, Action<T> handler)
        {
            _bus = bus;
            _handler = handler;
        }

        public void Dispose()
        {
            _bus?.Unsubscribe(_handler);
        }
    }
}
