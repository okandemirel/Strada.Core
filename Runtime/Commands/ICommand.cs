using System;
using System.Threading;
using System.Threading.Tasks;

namespace Strada.Core.Commands
{
    /// <summary>
    /// Async command that supports modern async/await with CancellationToken.
    /// Used for standalone async operations (not struct signals).
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

    /// <summary>
    /// Handler for struct-based signals (synchronous).
    /// </summary>
    public interface ISignalHandler<in TSignal> where TSignal : struct
    {
        void Handle(TSignal signal);
    }

    /// <summary>
    /// Handler for struct-based signals (asynchronous) using ValueTask.
    /// </summary>
    public interface IAsyncSignalHandler<in TSignal> where TSignal : struct
    {
        ValueTask HandleAsync(TSignal signal, CancellationToken ct = default);
    }
}
