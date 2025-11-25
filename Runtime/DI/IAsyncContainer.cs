using System;
using System.Threading;
using System.Threading.Tasks;

namespace Strada.Core.DI
{
    public interface IAsyncContainer : IContainer
    {
        ValueTask<T> ResolveAsync<T>(CancellationToken cancellation = default) where T : class;
        ValueTask<object> ResolveAsync(Type type, CancellationToken cancellation = default);
        ValueTask<IContainerScope> CreateScopeAsync(CancellationToken cancellation = default);
    }

    public interface IAsyncFactory<T> where T : class
    {
        ValueTask<T> CreateAsync(IContainer container, CancellationToken cancellation = default);
    }

    public interface IAsyncInitializable
    {
        ValueTask InitializeAsync(CancellationToken cancellation = default);
    }
}
