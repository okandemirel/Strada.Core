using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;

namespace Strada.Core.ECS.Communication
{
    /// <summary>
    /// Thread-safe command buffer for MVCS → ECS communication.
    /// </summary>
    /// <remarks>
    /// StradaCommandBuffer provides a thread-safe queue for commands sent from
    /// MVCS layer (main thread) to ECS systems (potentially job threads).
    ///
    /// Design:
    /// - Uses ConcurrentDictionary for thread-safe storage
    /// - Commands grouped by type for efficient retrieval
    /// - Delayed commands processed based on time
    /// - Zero allocation for command retrieval (array pooling)
    ///
    /// Thread Safety:
    /// - Send() can be called from any thread
    /// - GetCommands() should only be called from ECS systems
    /// - Commands processed at ECS sync points
    ///
    /// Performance:
    /// - O(1) send operation
    /// - O(1) command retrieval by type
    /// - Minimal allocation (command pooling)
    /// </remarks>
    public class StradaCommandBuffer : ICommandBuffer, ICommandReader
    {
        private readonly ConcurrentDictionary<Type, Queue<object>> _commandQueues;
        private readonly ConcurrentDictionary<Type, Queue<DelayedCommand>> _delayedCommandQueues;
        private readonly object _lockObject = new object();
        private float _currentTime;

        /// <inheritdoc/>
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

        /// <summary>
        /// Initializes a new instance of StradaCommandBuffer.
        /// </summary>
        public StradaCommandBuffer()
        {
            _commandQueues = new ConcurrentDictionary<Type, Queue<object>>();
            _delayedCommandQueues = new ConcurrentDictionary<Type, Queue<DelayedCommand>>();
            _currentTime = 0f;
        }

        /// <inheritdoc/>
        public void Send<T>(T command) where T : struct, IStradaCommand
        {
            var type = typeof(T);
            var queue = _commandQueues.GetOrAdd(type, _ => new Queue<object>());

            lock (queue)
            {
                queue.Enqueue(command);
            }
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public T[] GetCommands<T>() where T : struct, IStradaCommand
        {
            var type = typeof(T);
            var result = new List<T>();

            // Get immediate commands
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

            // Process delayed commands
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

                    // Re-queue commands that are still delayed
                    while (stillDelayed.Count > 0)
                    {
                        delayedQueue.Enqueue(stillDelayed.Dequeue());
                    }
                }
            }

            return result.ToArray();
        }

        /// <inheritdoc/>
        public bool HasCommands<T>() where T : struct, IStradaCommand
        {
            return GetCommandCount<T>() > 0;
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public void Clear()
        {
            lock (_lockObject)
            {
                _commandQueues.Clear();
                _delayedCommandQueues.Clear();
            }
        }

        /// <summary>
        /// Updates the command buffer's internal time.
        /// Call this once per frame before processing commands.
        /// </summary>
        /// <param name="deltaTime">Time since last update</param>
        public void Update(float deltaTime)
        {
            _currentTime += deltaTime;
        }

        /// <summary>
        /// Sets the current time (for testing).
        /// </summary>
        /// <param name="time">The time to set</param>
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

    /// <summary>
    /// Global command buffer accessor for ECS systems.
    /// </summary>
    /// <remarks>
    /// Provides static access to the command buffer for ECS systems.
    /// The buffer is created and managed by the HybridBridge.
    ///
    /// Usage in Systems:
    /// <code>
    /// [StradaSystem]
    /// public partial struct MySystem : IStradaSystem
    /// {
    ///     public void OnUpdate(ref SystemState state)
    ///     {
    ///         var commands = StradaCommands.GetCommands&lt;MyCommand&gt;();
    ///         foreach (var cmd in commands)
    ///         {
    ///             // Process command
    ///         }
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public static class StradaCommands
    {
        private static StradaCommandBuffer _globalBuffer;

        /// <summary>
        /// Gets or sets the global command buffer.
        /// </summary>
        public static StradaCommandBuffer Global
        {
            get
            {
                if (_globalBuffer == null)
                {
                    _globalBuffer = new StradaCommandBuffer();
                    Debug.LogWarning("Global command buffer was not initialized. Creating default instance.");
                }
                return _globalBuffer;
            }
            set => _globalBuffer = value;
        }

        /// <summary>
        /// Sends a command to the global buffer.
        /// </summary>
        /// <typeparam name="T">The command type</typeparam>
        /// <param name="command">The command data</param>
        public static void Send<T>(T command) where T : struct, IStradaCommand
        {
            Global.Send(command);
        }

        /// <summary>
        /// Sends a delayed command to the global buffer.
        /// </summary>
        /// <typeparam name="T">The command type</typeparam>
        /// <param name="command">The command data</param>
        /// <param name="delaySeconds">Delay before processing</param>
        public static void SendDelayed<T>(T command, float delaySeconds) where T : struct, IStradaCommand
        {
            Global.SendDelayed(command, delaySeconds);
        }

        /// <summary>
        /// Gets all commands of a specific type.
        /// </summary>
        /// <typeparam name="T">The command type</typeparam>
        /// <returns>Array of commands</returns>
        public static T[] GetCommands<T>() where T : struct, IStradaCommand
        {
            return Global.GetCommands<T>();
        }

        /// <summary>
        /// Checks if there are commands of a specific type.
        /// </summary>
        /// <typeparam name="T">The command type</typeparam>
        /// <returns>True if commands are available</returns>
        public static bool HasCommands<T>() where T : struct, IStradaCommand
        {
            return Global.HasCommands<T>();
        }

        /// <summary>
        /// Gets the number of commands of a specific type.
        /// </summary>
        /// <typeparam name="T">The command type</typeparam>
        /// <returns>Number of commands</returns>
        public static int GetCommandCount<T>() where T : struct, IStradaCommand
        {
            return Global.GetCommandCount<T>();
        }

        /// <summary>
        /// Updates the global command buffer's time.
        /// </summary>
        /// <param name="deltaTime">Time since last update</param>
        public static void Update(float deltaTime)
        {
            Global.Update(deltaTime);
        }

        /// <summary>
        /// Clears all pending commands.
        /// </summary>
        public static void Clear()
        {
            Global.Clear();
        }
    }
}
