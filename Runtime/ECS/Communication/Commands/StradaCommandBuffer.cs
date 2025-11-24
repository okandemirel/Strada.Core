using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace Strada.Core.ECS.Communication
{
    public class StradaCommandBuffer : ICommandBuffer, ICommandReader
    {
        private readonly ConcurrentDictionary<Type, Queue<object>> _commandQueues;
        private readonly ConcurrentDictionary<Type, Queue<DelayedCommand>> _delayedCommandQueues;
        private readonly object _lockObject = new object();
        private float _currentTime;

        public int PendingCount
        {
            get
            {
                int count = 0;
                foreach (var queue in _commandQueues.Values)
                {
                    count += queue.Count;
                }
                foreach (var queue in _delayedCommandQueues.Values)
                {
                    count += queue.Count;
                }
                return count;
            }
        }

        public StradaCommandBuffer()
        {
            _commandQueues = new ConcurrentDictionary<Type, Queue<object>>();
            _delayedCommandQueues = new ConcurrentDictionary<Type, Queue<DelayedCommand>>();
            _currentTime = 0f;
        }

        public void Send<T>(T command) where T : struct, IStradaCommand
        {
            var type = typeof(T);
            var queue = _commandQueues.GetOrAdd(type, _ => new Queue<object>());

            lock (queue)
            {
                queue.Enqueue(command);
            }
        }

        public void SendDelayed<T>(T command, float delaySeconds) where T : struct, IStradaCommand
        {
            if (delaySeconds <= 0)
            {
                Send(command);
                return;
            }

            var type = typeof(T);
            var queue = _delayedCommandQueues.GetOrAdd(type, _ => new Queue<DelayedCommand>());

            lock (queue)
            {
                queue.Enqueue(new DelayedCommand
                {
                    Command = command,
                    ExecuteTime = _currentTime + delaySeconds
                });
            }
        }

        public T[] GetCommands<T>() where T : struct, IStradaCommand
        {
            var type = typeof(T);
            var result = new List<T>();

            if (_commandQueues.TryGetValue(type, out var queue))
            {
                lock (queue)
                {
                    while (queue.Count > 0)
                    {
                        if (queue.Dequeue() is T command)
                        {
                            result.Add(command);
                        }
                    }
                }
            }

            if (_delayedCommandQueues.TryGetValue(type, out var delayedQueue))
            {
                lock (delayedQueue)
                {
                    var stillDelayed = new Queue<DelayedCommand>();

                    while (delayedQueue.Count > 0)
                    {
                        var delayedCmd = delayedQueue.Dequeue();
                        if (delayedCmd.ExecuteTime <= _currentTime)
                        {
                            if (delayedCmd.Command is T command)
                            {
                                result.Add(command);
                            }
                        }
                        else
                        {
                            stillDelayed.Enqueue(delayedCmd);
                        }
                    }

                    while (stillDelayed.Count > 0)
                    {
                        delayedQueue.Enqueue(stillDelayed.Dequeue());
                    }
                }
            }

            return result.ToArray();
        }

        public bool HasCommands<T>() where T : struct, IStradaCommand
        {
            return GetCommandCount<T>() > 0;
        }

        public int GetCommandCount<T>() where T : struct, IStradaCommand
        {
            var type = typeof(T);
            int count = 0;

            if (_commandQueues.TryGetValue(type, out var queue))
            {
                lock (queue)
                {
                    count += queue.Count;
                }
            }

            if (_delayedCommandQueues.TryGetValue(type, out var delayedQueue))
            {
                lock (delayedQueue)
                {
                    foreach (var delayed in delayedQueue)
                    {
                        if (delayed.ExecuteTime <= _currentTime)
                            count++;
                    }
                }
            }

            return count;
        }

        public void Clear()
        {
            lock (_lockObject)
            {
                _commandQueues.Clear();
                _delayedCommandQueues.Clear();
            }
        }

        public void Update(float deltaTime)
        {
            _currentTime += deltaTime;
        }

        public void SetTime(float time)
        {
            _currentTime = time;
        }

        private struct DelayedCommand
        {
            public object Command;
            public float ExecuteTime;
        }
    }
}
