using System;

namespace Strada.Core.ECS.Communication
{
    /// <summary>
    /// Marker interface for Strada commands.
    /// Commands represent requests from MVCS to ECS systems.
    /// </summary>
    /// <remarks>
    /// Commands flow from MVCS layer to ECS layer:
    ///
    /// Flow:
    /// 1. Controller/Service creates command
    /// 2. Command sent to StradaCommandBuffer
    /// 3. Buffer queues command (thread-safe)
    /// 4. ECS system processes command at sync point
    /// 5. System performs structural changes or updates components
    ///
    /// Best Practices:
    /// - Commands should be structs (value types)
    /// - Keep commands small and focused
    /// - Commands are immutable once sent
    /// - Use Burst-compatible types only
    /// - No references to MVCS objects
    ///
    /// Example:
    /// <code>
    /// // Define command
    /// public struct SpawnBallCommand : IStradaCommand
    /// {
    ///     public float3 Position;
    ///     public float Mass;
    ///     public float Radius;
    /// }
    ///
    /// // Send from Controller (MVCS)
    /// public class GameController : IController
    /// {
    ///     private readonly ICommandBuffer _commands;
    ///
    ///     public void SpawnBall(Vector3 position)
    ///     {
    ///         _commands.Send(new SpawnBallCommand
    ///         {
    ///             Position = position,
    ///             Mass = 1.0f,
    ///             Radius = 0.5f
    ///         });
    ///     }
    /// }
    ///
    /// // Process in System (ECS)
    /// [StradaSystem]
    /// public partial struct BallSpawnSystem : IStradaSystem
    /// {
    ///     public void OnUpdate(ref SystemState state)
    ///     {
    ///         var commands = StradaCommandBuffer.GetCommands&lt;SpawnBallCommand&gt;(ref state);
    ///         foreach (var cmd in commands)
    ///         {
    ///             var entity = state.EntityManager.CreateEntity();
    ///             state.EntityManager.AddComponent(entity, new BallComponent
    ///             {
    ///                 Position = cmd.Position,
    ///                 Mass = cmd.Mass,
    ///                 Radius = cmd.Radius
    ///             });
    ///         }
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public interface IStradaCommand
    {
        // Marker interface - commands are defined by their data
    }

    /// <summary>
    /// Interface for sending commands from MVCS to ECS.
    /// </summary>
    /// <remarks>
    /// The command buffer provides a thread-safe way to queue commands
    /// from the MVCS layer (main thread) to ECS systems (potentially job threads).
    ///
    /// Commands are queued and processed at safe synchronization points
    /// in the ECS update cycle to avoid race conditions.
    /// </remarks>
    public interface ICommandBuffer
    {
        /// <summary>
        /// Sends a command to be processed by ECS systems.
        /// </summary>
        /// <typeparam name="T">The command type</typeparam>
        /// <param name="command">The command data</param>
        void Send<T>(T command) where T : struct, IStradaCommand;

        /// <summary>
        /// Sends a command with a delay.
        /// </summary>
        /// <typeparam name="T">The command type</typeparam>
        /// <param name="command">The command data</param>
        /// <param name="delaySeconds">Delay before processing</param>
        void SendDelayed<T>(T command, float delaySeconds) where T : struct, IStradaCommand;

        /// <summary>
        /// Gets the number of pending commands.
        /// </summary>
        int PendingCount { get; }

        /// <summary>
        /// Clears all pending commands.
        /// </summary>
        void Clear();
    }

    /// <summary>
    /// Interface for reading commands in ECS systems.
    /// </summary>
    /// <remarks>
    /// This interface is used by ECS systems to retrieve and process
    /// commands that were sent from the MVCS layer.
    /// </remarks>
    public interface ICommandReader
    {
        /// <summary>
        /// Gets all commands of a specific type.
        /// </summary>
        /// <typeparam name="T">The command type</typeparam>
        /// <returns>Array of commands</returns>
        T[] GetCommands<T>() where T : struct, IStradaCommand;

        /// <summary>
        /// Checks if there are any commands of a specific type.
        /// </summary>
        /// <typeparam name="T">The command type</typeparam>
        /// <returns>True if commands are available</returns>
        bool HasCommands<T>() where T : struct, IStradaCommand;

        /// <summary>
        /// Gets the number of commands of a specific type.
        /// </summary>
        /// <typeparam name="T">The command type</typeparam>
        /// <returns>Number of commands</returns>
        int GetCommandCount<T>() where T : struct, IStradaCommand;
    }

    /// <summary>
    /// Attribute to mark a system as a command processor.
    /// </summary>
    /// <remarks>
    /// Systems marked with this attribute are automatically registered
    /// to process specific command types.
    ///
    /// Example:
    /// <code>
    /// [StradaSystem]
    /// [CommandProcessor(typeof(SpawnBallCommand))]
    /// public partial struct BallSpawnSystem : IStradaSystem
    /// {
    ///     // Processes SpawnBallCommand
    /// }
    /// </code>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
    public class CommandProcessorAttribute : Attribute
    {
        /// <summary>
        /// The command types this system processes.
        /// </summary>
        public Type[] CommandTypes { get; }

        /// <summary>
        /// Initializes a new instance of CommandProcessorAttribute.
        /// </summary>
        /// <param name="commandTypes">The command types to process</param>
        public CommandProcessorAttribute(params Type[] commandTypes)
        {
            CommandTypes = commandTypes ?? Array.Empty<Type>();

            // Validate command types
            foreach (var type in CommandTypes)
            {
                if (!typeof(IStradaCommand).IsAssignableFrom(type))
                {
                    throw new ArgumentException(
                        $"Type {type.Name} must implement IStradaCommand",
                        nameof(commandTypes));
                }
            }
        }
    }

    /// <summary>
    /// Result of command processing.
    /// </summary>
    public enum CommandResult
    {
        /// <summary>
        /// Command processed successfully.
        /// </summary>
        Success,

        /// <summary>
        /// Command failed to process.
        /// </summary>
        Failed,

        /// <summary>
        /// Command was deferred for later processing.
        /// </summary>
        Deferred,

        /// <summary>
        /// Command was cancelled.
        /// </summary>
        Cancelled
    }

    /// <summary>
    /// Information about command processing.
    /// </summary>
    public struct CommandProcessingInfo
    {
        /// <summary>
        /// The command type.
        /// </summary>
        public Type CommandType;

        /// <summary>
        /// Number of commands processed.
        /// </summary>
        public int ProcessedCount;

        /// <summary>
        /// Number of commands failed.
        /// </summary>
        public int FailedCount;

        /// <summary>
        /// Number of commands deferred.
        /// </summary>
        public int DeferredCount;

        /// <summary>
        /// Processing time in milliseconds.
        /// </summary>
        public float ProcessingTimeMs;
    }
}
