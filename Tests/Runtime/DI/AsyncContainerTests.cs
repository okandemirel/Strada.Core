using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Strada.Core.DI;

namespace Strada.Core.Tests.Tests.Runtime.DI
{
    [TestFixture]
    public class AsyncContainerTests
    {
        private IContainer _container;

        private interface IService { }
        private class SimpleService : IService { }

        private class AsyncInitService : IService, IAsyncInitializable
        {
            public bool Initialized { get; private set; }

            public async ValueTask InitializeAsync(CancellationToken cancellation = default)
            {
                await Task.Delay(10, cancellation);
                Initialized = true;
            }
        }

        private class ExpensiveService : IService, IAsyncInitializable
        {
            public int Value { get; private set; }

            public async ValueTask InitializeAsync(CancellationToken cancellation = default)
            {
                await Task.Delay(50, cancellation);
                Value = 42;
            }
        }

        [SetUp]
        public void SetUp()
        {
            var builder = new ContainerBuilder();
            builder.Register<IService, SimpleService>(Lifetime.Scoped);
            builder.Register<AsyncInitService>(Lifetime.Scoped);
            builder.Register<ExpensiveService>(Lifetime.Scoped);
            _container = builder.Build();
        }

        [TearDown]
        public void TearDown()
        {
            _container?.Dispose();
        }

        [Test]
        public async Task CreateScopeAsync_CreatesValidScope()
        {
            await using var scope = await _container.CreateScopeAsync();

            Assert.IsNotNull(scope);
            Assert.IsTrue(scope.IsRegistered<IService>());
        }

        [Test]
        public async Task ResolveAsync_WithSyncService_ReturnsInstance()
        {
            await using var scope = await _container.CreateScopeAsync();

            var service = await scope.ResolveAsync<IService>();

            Assert.IsNotNull(service);
            Assert.IsInstanceOf<SimpleService>(service);
        }

        [Test]
        public async Task ResolveAsync_WithAsyncInit_InitializesService()
        {
            await using var scope = await _container.CreateScopeAsync();

            var service = await scope.ResolveAsync<AsyncInitService>();

            Assert.IsNotNull(service);
            Assert.IsTrue(service.Initialized);
        }

        [Test]
        public async Task AsyncScopeBuilder_PreWarm_InitializesBeforeReturn()
        {
            await using var scope = await _container
                .CreateAsyncScopeBuilder()
                .PreWarm<ExpensiveService>()
                .BuildAsync();

            var service = scope.Resolve<ExpensiveService>();

            Assert.AreEqual(42, service.Value);
        }

        [Test]
        public async Task AsyncScopeBuilder_RegisterAsync_UsesAsyncFactory()
        {
            int factoryCalls = 0;

            await using var scope = await _container
                .CreateAsyncScopeBuilder()
                .RegisterAsync<IService>(async (container, ct) =>
                {
                    factoryCalls++;
                    await Task.Delay(10, ct);
                    return new SimpleService();
                })
                .BuildAsync();

            var service = await scope.ResolveAsync<IService>();

            Assert.IsNotNull(service);
            Assert.AreEqual(1, factoryCalls);
        }

        [Test]
        public async Task CreateScopeWithPreWarmAsync_SingleType_Works()
        {
            await using var scope = await _container.CreateScopeWithPreWarmAsync<ExpensiveService>();

            var service = scope.Resolve<ExpensiveService>();
            Assert.AreEqual(42, service.Value);
        }

        [Test]
        public async Task CreateScopeWithPreWarmAsync_MultipleTypes_Works()
        {
            await using var scope = await _container.CreateScopeWithPreWarmAsync<ExpensiveService, AsyncInitService>();

            var expensive = scope.Resolve<ExpensiveService>();
            var asyncInit = scope.Resolve<AsyncInitService>();

            Assert.AreEqual(42, expensive.Value);
            Assert.IsTrue(asyncInit.Initialized);
        }

        [Test]
        public async Task AsyncScope_Cancellation_ThrowsOperationCanceled()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<TaskCanceledException>(async () =>
            {
                await _container
                    .CreateAsyncScopeBuilder()
                    .PreWarm<ExpensiveService>()
                    .BuildAsync(cts.Token);
            });
        }

        [Test]
        public async Task AsyncScope_DisposeAsync_DisposesInnerScope()
        {
            var scope = await _container.CreateScopeAsync();
            var service = scope.Resolve<IService>();

            await scope.DisposeAsync();

            Assert.Throws<System.ObjectDisposedException>(() => scope.Resolve<IService>());
        }

        [Test]
        public async Task AsyncScope_SyncResolve_StillWorks()
        {
            await using var scope = await _container.CreateScopeAsync();

            var service = scope.Resolve<IService>();

            Assert.IsNotNull(service);
            Assert.IsInstanceOf<SimpleService>(service);
        }
    }

    [TestFixture]
    [Category("Performance")]
    public class AsyncContainerPerformanceTests
    {
        [Test]
        public async Task Benchmark_AsyncScopeCreation_1k()
        {
            var builder = new ContainerBuilder();
            builder.Register<TestService>(Lifetime.Scoped);
            var container = builder.Build();

            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
            {
                await using var scope = await container.CreateScopeAsync();
                var _ = scope.Resolve<TestService>();
            }
            sw.Stop();

            UnityEngine.Debug.Log($"[Async DI] 1k scope creation+resolve: {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks * 1_000_000.0 / 1000 / System.Diagnostics.Stopwatch.Frequency:F0}μs/scope)");

            Assert.Less(sw.ElapsedMilliseconds, 500);
            container.Dispose();
        }

        [Test]
        public async Task Benchmark_AsyncResolve_10k()
        {
            var builder = new ContainerBuilder();
            builder.Register<TestService>(Lifetime.Transient);
            var container = builder.Build();
            await using var scope = await container.CreateScopeAsync();

            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 10_000; i++)
            {
                var _ = await scope.ResolveAsync<TestService>();
            }
            sw.Stop();

            UnityEngine.Debug.Log($"[Async DI] 10k ResolveAsync: {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks * 1_000_000_000.0 / 10_000 / System.Diagnostics.Stopwatch.Frequency:F0}ns/resolve)");

            Assert.Less(sw.ElapsedMilliseconds, 100);
            container.Dispose();
        }

        private class TestService { }
    }
}
