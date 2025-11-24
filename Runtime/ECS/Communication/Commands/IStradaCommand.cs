using System;

namespace Strada.Core.ECS.Communication
{
    public interface IStradaCommand
    {
    }

    public interface ICommandBuffer
    {
        void Send<T>(T command) where T : struct, IStradaCommand;
        void SendDelayed<T>(T command, float delaySeconds) where T : struct, IStradaCommand;
        int PendingCount { get; }
        void Clear();
    }

    public interface ICommandReader
    {
        T[] GetCommands<T>() where T : struct, IStradaCommand;
        bool HasCommands<T>() where T : struct, IStradaCommand;
        int GetCommandCount<T>() where T : struct, IStradaCommand;
    }

    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
    public class CommandProcessorAttribute : Attribute
    {
        public Type[] CommandTypes { get; }

        public CommandProcessorAttribute(params Type[] commandTypes)
        {
            CommandTypes = commandTypes ?? Array.Empty<Type>();

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

    public enum CommandResult
    {
        Success,
        Failed,
        Deferred,
        Cancelled
    }

    public struct CommandProcessingInfo
    {
        public Type CommandType;
        public int ProcessedCount;
        public int FailedCount;
        public int DeferredCount;
        public float ProcessingTimeMs;
    }
}
