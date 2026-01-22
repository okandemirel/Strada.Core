using System.Threading;
using System.Threading.Tasks;

namespace Strada.Core.Commands
{
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
