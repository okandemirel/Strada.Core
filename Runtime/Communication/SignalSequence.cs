using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Strada.Core.Communication
{
    /// <summary>
    /// Composable, async-capable signal sequence builder.
    /// Allows chaining multiple signals, including nested sequences,
    /// with optional multi-bus targeting.
    /// </summary>
    public sealed class SignalSequence : IDisposable
    {
        private readonly List<ISequenceEntry> _entries;
        private IEventBus _defaultBus;
        private bool _disposed;

        public SignalSequence()
        {
            _entries = new List<ISequenceEntry>(8);
        }

        public SignalSequence(IEventBus defaultBus) : this()
        {
            _defaultBus = defaultBus;
        }

        /// <summary>
        /// Sets the default EventBus for signals in this sequence.
        /// </summary>
        public SignalSequence WithBus(IEventBus bus)
        {
            _defaultBus = bus;
            return this;
        }

        /// <summary>
        /// Adds a signal to the sequence using the default bus.
        /// </summary>
        public SignalSequence Then<TSignal>(TSignal signal) where TSignal : struct
        {
            _entries.Add(new SignalEntry<TSignal>(signal, null));
            return this;
        }

        /// <summary>
        /// Adds a signal to the sequence targeting a specific bus.
        /// </summary>
        public SignalSequence Then<TSignal>(TSignal signal, IEventBus targetBus) where TSignal : struct
        {
            _entries.Add(new SignalEntry<TSignal>(signal, targetBus));
            return this;
        }

        /// <summary>
        /// Includes another sequence in this sequence.
        /// The included sequence will be executed at this point in the chain.
        /// </summary>
        public SignalSequence Include(SignalSequence other)
        {
            if (other != null && other != this)
            {
                _entries.Add(new SequenceEntry(other));
            }
            return this;
        }

        /// <summary>
        /// Adds a synchronous action to the sequence.
        /// </summary>
        public SignalSequence Then(Action action)
        {
            if (action != null)
            {
                _entries.Add(new ActionEntry(action));
            }
            return this;
        }

        /// <summary>
        /// Adds an async action to the sequence.
        /// </summary>
        public SignalSequence ThenAsync(Func<CancellationToken, ValueTask> asyncAction)
        {
            if (asyncAction != null)
            {
                _entries.Add(new AsyncActionEntry(asyncAction));
            }
            return this;
        }

        /// <summary>
        /// Adds a conditional signal that only executes if the predicate is true.
        /// </summary>
        public SignalSequence ThenIf<TSignal>(bool condition, TSignal signal) where TSignal : struct
        {
            if (condition)
            {
                _entries.Add(new SignalEntry<TSignal>(signal, null));
            }
            return this;
        }

        /// <summary>
        /// Adds a conditional signal using a predicate evaluated at execution time.
        /// </summary>
        public SignalSequence ThenIf<TSignal>(Func<bool> predicate, TSignal signal) where TSignal : struct
        {
            _entries.Add(new ConditionalSignalEntry<TSignal>(signal, predicate, null));
            return this;
        }

        /// <summary>
        /// Executes all signals in the sequence synchronously.
        /// </summary>
        public void Execute()
        {
            Execute(_defaultBus);
        }

        /// <summary>
        /// Executes all signals in the sequence synchronously with a specific default bus.
        /// </summary>
        public void Execute(IEventBus defaultBus)
        {
            if (_disposed) return;

            foreach (var entry in _entries)
            {
                entry.Execute(defaultBus ?? _defaultBus);
            }
        }

        /// <summary>
        /// Executes all signals in the sequence asynchronously.
        /// </summary>
        public ValueTask ExecuteAsync(CancellationToken ct = default)
        {
            return ExecuteAsync(_defaultBus, ct);
        }

        /// <summary>
        /// Executes all signals in the sequence asynchronously with a specific default bus.
        /// </summary>
        public async ValueTask ExecuteAsync(IEventBus defaultBus, CancellationToken ct = default)
        {
            if (_disposed) return;

            foreach (var entry in _entries)
            {
                ct.ThrowIfCancellationRequested();
                await entry.ExecuteAsync(defaultBus ?? _defaultBus, ct);
            }
        }

        /// <summary>
        /// Clears all entries from the sequence for reuse.
        /// </summary>
        public void Clear()
        {
            _entries.Clear();
        }

        /// <summary>
        /// Gets the number of entries in the sequence.
        /// </summary>
        public int Count => _entries.Count;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _entries.Clear();
        }

        #region Entry Interfaces and Implementations

        private interface ISequenceEntry
        {
            void Execute(IEventBus defaultBus);
            ValueTask ExecuteAsync(IEventBus defaultBus, CancellationToken ct);
        }

        private readonly struct SignalEntry<TSignal> : ISequenceEntry where TSignal : struct
        {
            private readonly TSignal _signal;
            private readonly IEventBus _targetBus;

            public SignalEntry(TSignal signal, IEventBus targetBus)
            {
                _signal = signal;
                _targetBus = targetBus;
            }

            public void Execute(IEventBus defaultBus)
            {
                var bus = _targetBus ?? defaultBus;
                if (bus != null)
                {
                    var signal = _signal;
                    bus.Send(ref signal);
                }
            }

            public ValueTask ExecuteAsync(IEventBus defaultBus, CancellationToken ct)
            {
                var bus = _targetBus ?? defaultBus;
                if (bus != null)
                {
                    return bus.SendAsync(_signal, ct);
                }
                return default;
            }
        }

        private readonly struct ConditionalSignalEntry<TSignal> : ISequenceEntry where TSignal : struct
        {
            private readonly TSignal _signal;
            private readonly Func<bool> _predicate;
            private readonly IEventBus _targetBus;

            public ConditionalSignalEntry(TSignal signal, Func<bool> predicate, IEventBus targetBus)
            {
                _signal = signal;
                _predicate = predicate;
                _targetBus = targetBus;
            }

            public void Execute(IEventBus defaultBus)
            {
                if (_predicate == null || !_predicate()) return;

                var bus = _targetBus ?? defaultBus;
                if (bus != null)
                {
                    var signal = _signal;
                    bus.Send(ref signal);
                }
            }

            public ValueTask ExecuteAsync(IEventBus defaultBus, CancellationToken ct)
            {
                if (_predicate == null || !_predicate()) return default;

                var bus = _targetBus ?? defaultBus;
                if (bus != null)
                {
                    return bus.SendAsync(_signal, ct);
                }
                return default;
            }
        }

        private sealed class SequenceEntry : ISequenceEntry
        {
            private readonly SignalSequence _sequence;

            public SequenceEntry(SignalSequence sequence)
            {
                _sequence = sequence;
            }

            public void Execute(IEventBus defaultBus)
            {
                _sequence?.Execute(defaultBus);
            }

            public ValueTask ExecuteAsync(IEventBus defaultBus, CancellationToken ct)
            {
                return _sequence?.ExecuteAsync(defaultBus, ct) ?? default;
            }
        }

        private sealed class ActionEntry : ISequenceEntry
        {
            private readonly Action _action;

            public ActionEntry(Action action)
            {
                _action = action;
            }

            public void Execute(IEventBus defaultBus)
            {
                _action?.Invoke();
            }

            public ValueTask ExecuteAsync(IEventBus defaultBus, CancellationToken ct)
            {
                _action?.Invoke();
                return default;
            }
        }

        private sealed class AsyncActionEntry : ISequenceEntry
        {
            private readonly Func<CancellationToken, ValueTask> _asyncAction;

            public AsyncActionEntry(Func<CancellationToken, ValueTask> asyncAction)
            {
                _asyncAction = asyncAction;
            }

            public void Execute(IEventBus defaultBus)
            {
                if (_asyncAction == null) return;
                var task = _asyncAction.Invoke(CancellationToken.None);
                if (!task.IsCompleted)
                {
                    task.AsTask().ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                            UnityEngine.Debug.LogError($"[SignalSequence] Async action failed: {t.Exception?.InnerException?.Message}");
                    }, TaskContinuationOptions.OnlyOnFaulted);
                }
                else if (task.IsFaulted)
                {
                    UnityEngine.Debug.LogError($"[SignalSequence] Async action failed: {task.AsTask().Exception?.InnerException?.Message}");
                }
            }

            public ValueTask ExecuteAsync(IEventBus defaultBus, CancellationToken ct)
            {
                return _asyncAction?.Invoke(ct) ?? default;
            }
        }

        #endregion
    }

    /// <summary>
    /// Registry for named signal sequences that can reference each other.
    /// </summary>
    public sealed class SignalSequenceRegistry : IDisposable
    {
        private readonly Dictionary<string, SignalSequence> _sequences;
        private readonly IEventBus _defaultBus;
        private bool _disposed;

        public SignalSequenceRegistry(IEventBus defaultBus = null)
        {
            _sequences = new Dictionary<string, SignalSequence>(16);
            _defaultBus = defaultBus;
        }

        /// <summary>
        /// Registers a named sequence.
        /// </summary>
        public void Register(string name, SignalSequence sequence)
        {
            if (string.IsNullOrEmpty(name)) return;
            _sequences[name] = sequence;
        }

        /// <summary>
        /// Creates and registers a named sequence with a builder action.
        /// </summary>
        public SignalSequence Create(string name, Action<SignalSequence> builder)
        {
            var sequence = new SignalSequence(_defaultBus);
            builder?.Invoke(sequence);
            Register(name, sequence);
            return sequence;
        }

        /// <summary>
        /// Gets a named sequence.
        /// </summary>
        public SignalSequence Get(string name)
        {
            return _sequences.TryGetValue(name, out var sequence) ? sequence : null;
        }

        /// <summary>
        /// Checks if a named sequence exists.
        /// </summary>
        public bool Contains(string name)
        {
            return _sequences.ContainsKey(name);
        }

        /// <summary>
        /// Executes a named sequence synchronously.
        /// </summary>
        public void Execute(string name)
        {
            if (_sequences.TryGetValue(name, out var sequence))
            {
                sequence.Execute(_defaultBus);
            }
        }

        /// <summary>
        /// Executes a named sequence asynchronously.
        /// </summary>
        public ValueTask ExecuteAsync(string name, CancellationToken ct = default)
        {
            if (_sequences.TryGetValue(name, out var sequence))
            {
                return sequence.ExecuteAsync(_defaultBus, ct);
            }
            return default;
        }

        /// <summary>
        /// Removes a named sequence.
        /// </summary>
        public bool Remove(string name)
        {
            if (_sequences.TryGetValue(name, out var sequence))
            {
                sequence.Dispose();
                return _sequences.Remove(name);
            }
            return false;
        }

        /// <summary>
        /// Clears all registered sequences.
        /// </summary>
        public void Clear()
        {
            foreach (var sequence in _sequences.Values)
            {
                sequence.Dispose();
            }
            _sequences.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Clear();
        }
    }
}
