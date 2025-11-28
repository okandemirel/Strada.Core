using System;
using System.Threading;
using System.Threading.Tasks;

namespace Strada.Core.Commands
{
    public interface ICommand
    {
        void Execute();
    }

    public interface ICommand<in T>
    {
        void Execute(T parameter);
    }

    /// <summary>
    /// Callback-based async command. Consider using IAsyncAwaitCommand instead for modern async/await support.
    /// </summary>
    [Obsolete("Use IAsyncAwaitCommand with ValueTask for better performance and cancellation support")]
    public interface IAsyncCommand
    {
        void Execute(Action onComplete);
        void Cancel();
    }

    /// <summary>
    /// Callback-based async command with parameter. Consider using IAsyncAwaitCommand&lt;T&gt; instead.
    /// </summary>
    [Obsolete("Use IAsyncAwaitCommand<T> with ValueTask for better performance and cancellation support")]
    public interface IAsyncCommand<in T>
    {
        void Execute(T parameter, Action onComplete);
        void Cancel();
    }

    /// <summary>
    /// Async command that supports modern async/await with CancellationToken.
    /// </summary>
    public interface IAsyncAwaitCommand
    {
        ValueTask ExecuteAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// Async command with parameter that supports modern async/await.
    /// </summary>
    public interface IAsyncAwaitCommand<in T>
    {
        ValueTask ExecuteAsync(T parameter, CancellationToken ct = default);
    }

    public interface ICommandHandler<in TCommand> where TCommand : struct
    {
        void Handle(TCommand command);
    }

    /// <summary>
    /// Callback-based async handler. Consider using IAsyncAwaitCommandHandler instead.
    /// </summary>
    [Obsolete("Use IAsyncAwaitCommandHandler<T> with ValueTask for better performance and cancellation support")]
    public interface IAsyncCommandHandler<in TCommand> where TCommand : struct
    {
        void Handle(TCommand command, Action onComplete);
        void Cancel();
    }

    /// <summary>
    /// Modern async command handler using ValueTask.
    /// </summary>
    public interface IAsyncAwaitCommandHandler<in TCommand> where TCommand : struct
    {
        ValueTask HandleAsync(TCommand command, CancellationToken ct = default);
    }
}
