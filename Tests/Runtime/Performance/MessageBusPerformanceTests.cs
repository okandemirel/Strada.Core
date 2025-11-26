using System.Diagnostics;
using NUnit.Framework;
using Strada.Core.Communication;

namespace Strada.Core.Tests.Performance
{
    [TestFixture]
    [Category("Performance")]
    public class StradaBusPerformanceTests
    {
        private StradaBus _bus;

        [SetUp]
        public void SetUp()
        {
            _bus = new StradaBus();
        }

        [TearDown]
        public void TearDown()
        {
            _bus?.Dispose();
        }

        [Test]
        public void Benchmark_100k_Commands()
        {
            var sum = 0;
            _bus.RegisterCommandHandler<BenchmarkCommand>(cmd => sum += cmd.Value);

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 100_000; i++)
            {
                var cmd = new BenchmarkCommand { Value = 1 };
                _bus.Send(ref cmd);
            }
            sw.Stop();

            UnityEngine.Debug.Log($"[StradaBus] 100k commands: {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks * 1000.0 / 100_000 / Stopwatch.Frequency * 1_000_000:F0}ns/dispatch)");

            Assert.AreEqual(100_000, sum);
            Assert.Less(sw.ElapsedMilliseconds, 50, "100k commands should complete in <50ms");
        }

        [Test]
        public void Benchmark_100k_Queries()
        {
            _bus.RegisterQueryHandler<BenchmarkQuery, int>(q => q.Input * 2);

            var total = 0;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 100_000; i++)
            {
                var q = new BenchmarkQuery { Input = 1 };
                total += _bus.Query<BenchmarkQuery, int>(ref q);
            }
            sw.Stop();

            UnityEngine.Debug.Log($"[StradaBus] 100k queries: {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks * 1000.0 / 100_000 / Stopwatch.Frequency * 1_000_000:F0}ns/dispatch)");

            Assert.AreEqual(200_000, total);
            Assert.Less(sw.ElapsedMilliseconds, 50, "100k queries should complete in <50ms");
        }

        [Test]
        public void Benchmark_100k_Events_SingleSubscriber()
        {
            var count = 0;
            _bus.Subscribe<BenchmarkEvent>(evt => count++);

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 100_000; i++)
            {
                var evt = new BenchmarkEvent();
                _bus.Publish(ref evt);
            }
            sw.Stop();

            UnityEngine.Debug.Log($"[StradaBus] 100k events (1 sub): {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks * 1000.0 / 100_000 / Stopwatch.Frequency * 1_000_000:F0}ns/dispatch)");

            Assert.AreEqual(100_000, count);
            Assert.Less(sw.ElapsedMilliseconds, 50, "100k events should complete in <50ms");
        }

        [Test]
        public void Benchmark_100k_Events_TenSubscribers()
        {
            var count = 0;
            for (int i = 0; i < 10; i++)
                _bus.Subscribe<BenchmarkEvent>(evt => count++);

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 100_000; i++)
            {
                var evt = new BenchmarkEvent();
                _bus.Publish(ref evt);
            }
            sw.Stop();

            UnityEngine.Debug.Log($"[StradaBus] 100k events (10 subs): {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks * 1000.0 / 100_000 / Stopwatch.Frequency * 1_000_000:F0}ns/dispatch)");

            Assert.AreEqual(1_000_000, count);
            Assert.Less(sw.ElapsedMilliseconds, 200, "100k events with 10 subs should complete in <200ms");
        }

        [Test]
        public void Benchmark_RegistrationSpeed()
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
            {
                var localBus = new StradaBus();
                localBus.RegisterCommandHandler<BenchmarkCommand>(cmd => { });
                localBus.RegisterQueryHandler<BenchmarkQuery, int>(q => q.Input);
                localBus.Subscribe<BenchmarkEvent>(evt => { });
                localBus.Dispose();
            }
            sw.Stop();

            UnityEngine.Debug.Log($"[StradaBus] 1000 full registrations: {sw.ElapsedMilliseconds}ms");
            Assert.Less(sw.ElapsedMilliseconds, 100, "1000 registrations should complete in <100ms");
        }

        private struct BenchmarkCommand
        {
            public int Value;
        }

        private struct BenchmarkQuery : IQuery<int>
        {
            public int Input;
        }

        private struct BenchmarkEvent { }
    }
}
