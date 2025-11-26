using System.Threading;
using System.Threading.Tasks;

namespace Strada.Core.DI
{
    public interface IAsyncFactory<T> where T : class
    {
        ValueTask<T> CreateAsync(IContainer container, CancellationToken cancellation = default);
    }

    public interface IAsyncInitializable
    {
        ValueTask InitializeAsync(CancellationToken cancellation = default);
    }
}
