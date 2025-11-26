using System;
using NUnit.Framework;
using Strada.Core.Commands;
using Strada.Core.Communication;

namespace Strada.Core.Tests.Commands
{
    [TestFixture]
    public class CommandBusTests
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
        public void Send_WithRegisteredHandler_ExecutesHandler()
        {
            var executed = false;
            _bus.RegisterCommandHandler<TestCommand>(cmd => executed = true);

            _bus.Send(new TestCommand());

            Assert.IsTrue(executed);
        }

        [Test]
        public void Send_PassesCorrectData()
        {
            var receivedValue = 0;
            _bus.RegisterCommandHandler<TestCommand>(cmd => receivedValue = cmd.Value);

            _bus.Send(new TestCommand { Value = 42 });

            Assert.AreEqual(42, receivedValue);
        }

        [Test]
        public void Send_WithoutHandler_ThrowsException()
        {
            Assert.Throws<InvalidOperationException>(() => _bus.Send(new TestCommand()));
        }

        [Test]
        public void Execute_PooledCommand_ReturnsToPool()
        {
            var command = TestPooledCommand.Rent();
            command.TestValue = 123;
            var executedValue = 0;
            command.OnExecuted = v => executedValue = v;

            _bus.Execute(command);

            Assert.AreEqual(123, executedValue);
            Assert.AreEqual(0, command.TestValue);
        }

        [Test]
        public void CommandPool_RentAndReturn()
        {
            var cmd1 = CommandPool<TestPooledCommand>.Instance.Rent();
            cmd1.TestValue = 100;

            CommandPool<TestPooledCommand>.Instance.Return(cmd1);

            var cmd2 = CommandPool<TestPooledCommand>.Instance.Rent();
            Assert.AreSame(cmd1, cmd2);
            Assert.AreEqual(0, cmd2.TestValue);
        }

        [Test]
        public void CommandPool_Prewarm()
        {
            var pool = new CommandPool<TestPooledCommand>();
            pool.Prewarm(10);

            var commands = new TestPooledCommand[10];
            for (int i = 0; i < 10; i++)
                commands[i] = pool.Rent();

            Assert.IsNotNull(commands[9]);
        }

        private struct TestCommand
        {
            public int Value;
        }

        private class TestPooledCommand : PooledCommandBase<TestPooledCommand>
        {
            public int TestValue;
            public Action<int> OnExecuted;

            public override void Reset()
            {
                TestValue = 0;
                OnExecuted = null;
            }

            protected override void OnExecute()
            {
                OnExecuted?.Invoke(TestValue);
            }
        }
    }

    [TestFixture]
    [Category("Performance")]
    public class CommandBusPerformanceTests
    {
        [Test]
        public void Benchmark_100k_TypedCommands()
        {
            var bus = new StradaBus();
            var sum = 0;
            bus.RegisterCommandHandler<BenchmarkCommand>(cmd => sum += cmd.Value);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 100_000; i++)
                bus.Send(new BenchmarkCommand { Value = 1 });
            sw.Stop();

            UnityEngine.Debug.Log($"[StradaBus] 100k typed commands: {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks * 1000.0 / 100_000 / System.Diagnostics.Stopwatch.Frequency * 1_000_000:F0}ns/send)");

            Assert.AreEqual(100_000, sum);
            Assert.Less(sw.ElapsedMilliseconds, 50);

            bus.Dispose();
        }

        [Test]
        public void Benchmark_100k_PooledCommands()
        {
            var bus = new StradaBus();
            CommandPool<BenchmarkPooledCommand>.Instance.Prewarm(100);
            var sum = 0;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 100_000; i++)
            {
                var cmd = BenchmarkPooledCommand.Rent();
                cmd.Value = 1;
                cmd.OnExecuted = v => sum += v;
                bus.Execute(cmd);
            }
            sw.Stop();

            UnityEngine.Debug.Log($"[StradaBus] 100k pooled commands: {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks * 1000.0 / 100_000 / System.Diagnostics.Stopwatch.Frequency * 1_000_000:F0}ns/execute)");

            Assert.AreEqual(100_000, sum);
            Assert.Less(sw.ElapsedMilliseconds, 100);

            bus.Dispose();
        }

        private struct BenchmarkCommand
        {
            public int Value;
        }

        private class BenchmarkPooledCommand : PooledCommandBase<BenchmarkPooledCommand>
        {
            public int Value;
            public Action<int> OnExecuted;

            public override void Reset()
            {
                Value = 0;
                OnExecuted = null;
            }

            protected override void OnExecute()
            {
                OnExecuted?.Invoke(Value);
            }
        }
    }
}
