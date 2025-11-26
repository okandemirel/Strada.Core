using System;
using System.Diagnostics;
using NUnit.Framework;
using Strada.Core.Commands;
using Strada.Core.Communication;

namespace Strada.Core.Tests.Runtime.Performance
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
        public void Benchmark_100k_CommandDispatches()
        {
            const int iterations = 100000;
            const int warmup = 1000;
            int counter = 0;

            _bus.RegisterCommandHandler<BenchmarkCommand>(cmd => counter += cmd.Value);

            // Warmup
            for (int i = 0; i < warmup; i++)
            {
                _bus.Send(new BenchmarkCommand { Value = 1 });
            }
            counter = 0;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                _bus.Send(new BenchmarkCommand { Value = 1 });
            }
            sw.Stop();

            double avgNanoseconds = sw.Elapsed.TotalMilliseconds * 1000000 / iterations;

            UnityEngine.Debug.Log($"[StradaBus] Command Dispatch ({iterations} dispatches):");
            UnityEngine.Debug.Log($"  Total Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {avgNanoseconds:F0}ns per dispatch");
            UnityEngine.Debug.Log($"  Throughput: {iterations / sw.Elapsed.TotalSeconds:F0} dispatches/sec");

            Assert.AreEqual(iterations, counter);
            Assert.Less(sw.ElapsedMilliseconds, 20, "Command dispatch too slow (Target: <20ms for 100k)");
        }

        [Test]
        public void Benchmark_100k_QueryDispatches()
        {
            const int iterations = 100000;
            const int warmup = 1000;

            _bus.RegisterQueryHandler<BenchmarkQuery, int>(q => q.Input * 2);

            // Warmup
            for (int i = 0; i < warmup; i++)
            {
                _bus.Query<BenchmarkQuery, int>(new BenchmarkQuery { Input = i });
            }

            var sw = Stopwatch.StartNew();
            int sum = 0;
            for (int i = 0; i < iterations; i++)
            {
                sum += _bus.Query<BenchmarkQuery, int>(new BenchmarkQuery { Input = 1 });
            }
            sw.Stop();

            double avgNanoseconds = sw.Elapsed.TotalMilliseconds * 1000000 / iterations;

            UnityEngine.Debug.Log($"[StradaBus] Query Dispatch ({iterations} queries):");
            UnityEngine.Debug.Log($"  Total Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {avgNanoseconds:F0}ns per query");
            UnityEngine.Debug.Log($"  Throughput: {iterations / sw.Elapsed.TotalSeconds:F0} queries/sec");

            Assert.AreEqual(iterations * 2, sum);
            Assert.Less(sw.ElapsedMilliseconds, 20, "Query dispatch too slow (Target: <20ms for 100k)");
        }

        [Test]
        public void Benchmark_100k_EventPublishes_SingleSubscriber()
        {
            const int iterations = 100000;
            const int warmup = 1000;
            int counter = 0;

            _bus.Subscribe<BenchmarkEvent>(evt => counter += evt.Value);

            // Warmup
            for (int i = 0; i < warmup; i++)
            {
                _bus.Publish(new BenchmarkEvent { Value = 1 });
            }
            counter = 0;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                _bus.Publish(new BenchmarkEvent { Value = 1 });
            }
            sw.Stop();

            double avgNanoseconds = sw.Elapsed.TotalMilliseconds * 1000000 / iterations;

            UnityEngine.Debug.Log($"[StradaBus] Event Publish - Single Subscriber ({iterations} publishes):");
            UnityEngine.Debug.Log($"  Total Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {avgNanoseconds:F0}ns per publish");
            UnityEngine.Debug.Log($"  Throughput: {iterations / sw.Elapsed.TotalSeconds:F0} publishes/sec");

            Assert.AreEqual(iterations, counter);
            Assert.Less(sw.ElapsedMilliseconds, 20, "Event publish too slow (Target: <20ms for 100k)");
        }

        [Test]
        public void Benchmark_100k_EventPublishes_10Subscribers()
        {
            const int iterations = 100000;
            const int subscribers = 10;
            const int warmup = 1000;
            int counter = 0;

            for (int s = 0; s < subscribers; s++)
            {
                _bus.Subscribe<BenchmarkEvent>(evt => counter += evt.Value);
            }

            // Warmup
            for (int i = 0; i < warmup; i++)
            {
                _bus.Publish(new BenchmarkEvent { Value = 1 });
            }
            counter = 0;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                _bus.Publish(new BenchmarkEvent { Value = 1 });
            }
            sw.Stop();

            double avgNanoseconds = sw.Elapsed.TotalMilliseconds * 1000000 / iterations;

            UnityEngine.Debug.Log($"[StradaBus] Event Publish - {subscribers} Subscribers ({iterations} publishes):");
            UnityEngine.Debug.Log($"  Total Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {avgNanoseconds:F0}ns per publish (dispatching to {subscribers})");
            UnityEngine.Debug.Log($"  Throughput: {iterations / sw.Elapsed.TotalSeconds:F0} publishes/sec");

            Assert.AreEqual(iterations * subscribers, counter);
            Assert.Less(sw.ElapsedMilliseconds, 50, "Event publish with 10 subscribers too slow (Target: <50ms for 100k)");
        }

        [Test]
        public void Benchmark_100k_PooledCommands()
        {
            const int iterations = 100000;
            const int warmup = 1000;
            int counter = 0;

            // Prewarm pool
            CommandPool<BenchmarkPooledCommand>.Instance.Prewarm(100);

            // Warmup
            for (int i = 0; i < warmup; i++)
            {
                var cmd = BenchmarkPooledCommand.Rent();
                cmd.Value = 1;
                cmd.OnExecuteAction = () => counter++;
                _bus.Execute(cmd);
            }
            counter = 0;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var cmd = BenchmarkPooledCommand.Rent();
                cmd.Value = 1;
                cmd.OnExecuteAction = () => counter++;
                _bus.Execute(cmd);
            }
            sw.Stop();

            double avgNanoseconds = sw.Elapsed.TotalMilliseconds * 1000000 / iterations;

            UnityEngine.Debug.Log($"[StradaBus] Pooled Command Execute ({iterations} executions):");
            UnityEngine.Debug.Log($"  Total Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {avgNanoseconds:F0}ns per execute (rent+execute+return)");
            UnityEngine.Debug.Log($"  Throughput: {iterations / sw.Elapsed.TotalSeconds:F0} commands/sec");

            Assert.AreEqual(iterations, counter);
            Assert.Less(sw.ElapsedMilliseconds, 50, "Pooled command execution too slow (Target: <50ms for 100k)");

            // Cleanup
            CommandPool<BenchmarkPooledCommand>.Instance.Clear();
        }

        [Test]
        public void Benchmark_Subscribe_Unsubscribe_Cycles()
        {
            const int iterations = 10000;
            const int warmup = 100;

            Action<BenchmarkEvent> handler = _ => { };

            // Warmup
            for (int i = 0; i < warmup; i++)
            {
                _bus.Subscribe(handler);
                _bus.Unsubscribe(handler);
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                _bus.Subscribe(handler);
                _bus.Unsubscribe(handler);
            }
            sw.Stop();

            double avgMicroseconds = sw.Elapsed.TotalMilliseconds * 1000 / iterations;

            UnityEngine.Debug.Log($"[StradaBus] Subscribe/Unsubscribe Cycles ({iterations} cycles):");
            UnityEngine.Debug.Log($"  Total Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {avgMicroseconds:F2}us per cycle");

            Assert.Less(sw.ElapsedMilliseconds, 100, "Subscribe/Unsubscribe too slow (Target: <100ms for 10k)");
        }

        [Test]
        public void Benchmark_MixedOperations()
        {
            const int iterations = 10000;
            int cmdCount = 0;
            int querySum = 0;
            int evtCount = 0;

            _bus.RegisterCommandHandler<BenchmarkCommand>(c => cmdCount++);
            _bus.RegisterQueryHandler<BenchmarkQuery, int>(q => q.Input * 2);
            _bus.Subscribe<BenchmarkEvent>(e => evtCount++);

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                _bus.Send(new BenchmarkCommand { Value = i });
                querySum += _bus.Query<BenchmarkQuery, int>(new BenchmarkQuery { Input = 1 });
                _bus.Publish(new BenchmarkEvent { Value = i });
            }
            sw.Stop();

            double avgMicroseconds = sw.Elapsed.TotalMilliseconds * 1000 / iterations;

            UnityEngine.Debug.Log($"[StradaBus] Mixed Operations ({iterations} iterations, 3 ops each):");
            UnityEngine.Debug.Log($"  Total Time: {sw.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"  Avg: {avgMicroseconds:F2}us per iteration (cmd+query+event)");
            UnityEngine.Debug.Log($"  Commands: {cmdCount}, Queries: {querySum / 2}, Events: {evtCount}");

            Assert.AreEqual(iterations, cmdCount);
            Assert.AreEqual(iterations * 2, querySum);
            Assert.AreEqual(iterations, evtCount);
            Assert.Less(sw.ElapsedMilliseconds, 50, "Mixed operations too slow (Target: <50ms for 10k)");
        }

        #region Benchmark Types

        private struct BenchmarkCommand
        {
            public int Value;
        }

        private struct BenchmarkQuery : IQuery<int>
        {
            public int Input;
        }

        private struct BenchmarkEvent
        {
            public int Value;
        }

        private class BenchmarkPooledCommand : PooledCommandBase<BenchmarkPooledCommand>
        {
            public int Value;
            public Action OnExecuteAction;

            public override void Reset()
            {
                Value = 0;
                OnExecuteAction = null;
            }

            protected override void OnExecute()
            {
                OnExecuteAction?.Invoke();
            }
        }

        #endregion
    }
}
