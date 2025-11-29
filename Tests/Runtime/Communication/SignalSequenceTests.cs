using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Strada.Core.Communication;

namespace Strada.Core.Tests.Tests.Runtime.Communication
{
    [TestFixture]
    public class SignalSequenceTests
    {
        private EventBus _bus;

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
        }

        [TearDown]
        public void TearDown()
        {
            _bus?.Dispose();
        }

        [Test]
        public void Then_SingleSignal_ExecutesHandler()
        {
            int received = 0;
            _bus.RegisterSignalHandler<TestSignal>(s => received = s.Value);

            var sequence = new SignalSequence(_bus)
                .Then(new TestSignal { Value = 42 });

            sequence.Execute();

            Assert.AreEqual(42, received);
        }

        [Test]
        public void Then_MultipleSignals_ExecutesInOrder()
        {
            var order = new List<int>();
            _bus.RegisterSignalHandler<TestSignal>(s => order.Add(s.Value));

            var sequence = new SignalSequence(_bus)
                .Then(new TestSignal { Value = 1 })
                .Then(new TestSignal { Value = 2 })
                .Then(new TestSignal { Value = 3 });

            sequence.Execute();

            Assert.AreEqual(3, order.Count);
            Assert.AreEqual(1, order[0]);
            Assert.AreEqual(2, order[1]);
            Assert.AreEqual(3, order[2]);
        }

        [Test]
        public void Then_WithAction_ExecutesAction()
        {
            bool actionCalled = false;

            var sequence = new SignalSequence()
                .Then(() => actionCalled = true);

            sequence.Execute();

            Assert.IsTrue(actionCalled);
        }

        [Test]
        public void Then_MixedSignalsAndActions_ExecutesInOrder()
        {
            var order = new List<string>();
            _bus.RegisterSignalHandler<TestSignal>(s => order.Add($"signal_{s.Value}"));

            var sequence = new SignalSequence(_bus)
                .Then(() => order.Add("action_1"))
                .Then(new TestSignal { Value = 10 })
                .Then(() => order.Add("action_2"))
                .Then(new TestSignal { Value = 20 });

            sequence.Execute();

            Assert.AreEqual(4, order.Count);
            Assert.AreEqual("action_1", order[0]);
            Assert.AreEqual("signal_10", order[1]);
            Assert.AreEqual("action_2", order[2]);
            Assert.AreEqual("signal_20", order[3]);
        }

        [Test]
        public void ThenIf_ConditionTrue_ExecutesSignal()
        {
            int received = 0;
            _bus.RegisterSignalHandler<TestSignal>(s => received = s.Value);

            var sequence = new SignalSequence(_bus)
                .ThenIf(true, new TestSignal { Value = 99 });

            sequence.Execute();

            Assert.AreEqual(99, received);
        }

        [Test]
        public void ThenIf_ConditionFalse_SkipsSignal()
        {
            int received = 0;
            _bus.RegisterSignalHandler<TestSignal>(s => received = s.Value);

            var sequence = new SignalSequence(_bus)
                .ThenIf(false, new TestSignal { Value = 99 });

            sequence.Execute();

            Assert.AreEqual(0, received);
        }

        [Test]
        public void ThenIf_PredicateEvaluatedAtExecutionTime()
        {
            int received = 0;
            bool shouldExecute = false;
            _bus.RegisterSignalHandler<TestSignal>(s => received = s.Value);

            var sequence = new SignalSequence(_bus)
                .ThenIf(() => shouldExecute, new TestSignal { Value = 55 });

            sequence.Execute();
            Assert.AreEqual(0, received);

            shouldExecute = true;
            sequence.Execute();
            Assert.AreEqual(55, received);
        }

        [Test]
        public void Include_NestedSequence_ExecutesBoth()
        {
            var order = new List<int>();
            _bus.RegisterSignalHandler<TestSignal>(s => order.Add(s.Value));

            var inner = new SignalSequence(_bus)
                .Then(new TestSignal { Value = 2 })
                .Then(new TestSignal { Value = 3 });

            var outer = new SignalSequence(_bus)
                .Then(new TestSignal { Value = 1 })
                .Include(inner)
                .Then(new TestSignal { Value = 4 });

            outer.Execute();

            Assert.AreEqual(4, order.Count);
            Assert.AreEqual(1, order[0]);
            Assert.AreEqual(2, order[1]);
            Assert.AreEqual(3, order[2]);
            Assert.AreEqual(4, order[3]);
        }

        [Test]
        public void Include_NullSequence_DoesNotThrow()
        {
            var sequence = new SignalSequence()
                .Include(null);

            Assert.DoesNotThrow(() => sequence.Execute());
        }

        [Test]
        public void Include_SelfReference_Ignored()
        {
            var sequence = new SignalSequence();
            sequence.Include(sequence);

            Assert.DoesNotThrow(() => sequence.Execute());
            Assert.AreEqual(0, sequence.Count);
        }

        [Test]
        public void WithBus_SetsDefaultBus()
        {
            int received = 0;
            _bus.RegisterSignalHandler<TestSignal>(s => received = s.Value);

            var sequence = new SignalSequence()
                .WithBus(_bus)
                .Then(new TestSignal { Value = 77 });

            sequence.Execute();

            Assert.AreEqual(77, received);
        }

        [Test]
        public void Then_WithTargetBus_UsesSpecificBus()
        {
            int bus1Received = 0;
            int bus2Received = 0;

            var bus2 = new EventBus();
            _bus.RegisterSignalHandler<TestSignal>(s => bus1Received = s.Value);
            bus2.RegisterSignalHandler<TestSignal>(s => bus2Received = s.Value);

            var sequence = new SignalSequence(_bus)
                .Then(new TestSignal { Value = 1 })
                .Then(new TestSignal { Value = 2 }, bus2);

            sequence.Execute();

            Assert.AreEqual(1, bus1Received);
            Assert.AreEqual(2, bus2Received);

            bus2.Dispose();
        }

        [Test]
        public void Count_ReturnsCorrectEntryCount()
        {
            var sequence = new SignalSequence()
                .Then(new TestSignal { Value = 1 })
                .Then(new TestSignal { Value = 2 })
                .Then(() => { });

            Assert.AreEqual(3, sequence.Count);
        }

        [Test]
        public void Clear_RemovesAllEntries()
        {
            var sequence = new SignalSequence()
                .Then(new TestSignal { Value = 1 })
                .Then(new TestSignal { Value = 2 });

            Assert.AreEqual(2, sequence.Count);

            sequence.Clear();

            Assert.AreEqual(0, sequence.Count);
        }

        [Test]
        public void Dispose_PreventsExecution()
        {
            int received = 0;
            _bus.RegisterSignalHandler<TestSignal>(s => received = s.Value);

            var sequence = new SignalSequence(_bus)
                .Then(new TestSignal { Value = 42 });

            sequence.Dispose();
            sequence.Execute();

            Assert.AreEqual(0, received);
        }

        [Test]
        public void Dispose_CalledMultipleTimes_DoesNotThrow()
        {
            var sequence = new SignalSequence();

            Assert.DoesNotThrow(() =>
            {
                sequence.Dispose();
                sequence.Dispose();
                sequence.Dispose();
            });
        }

        [Test]
        public async Task ExecuteAsync_ExecutesAllSignals()
        {
            var order = new List<int>();
            _bus.RegisterAsyncSignalHandler<TestSignal>(async (s, ct) =>
            {
                await Task.Yield();
                order.Add(s.Value);
            });

            var sequence = new SignalSequence(_bus)
                .Then(new TestSignal { Value = 1 })
                .Then(new TestSignal { Value = 2 });

            await sequence.ExecuteAsync();

            Assert.AreEqual(2, order.Count);
            Assert.AreEqual(1, order[0]);
            Assert.AreEqual(2, order[1]);
        }

        [Test]
        public async Task ThenAsync_ExecutesAsyncAction()
        {
            bool completed = false;

            var sequence = new SignalSequence()
                .ThenAsync(async ct =>
                {
                    await Task.Yield();
                    completed = true;
                });

            await sequence.ExecuteAsync();

            Assert.IsTrue(completed);
        }

        [Test]
        public async Task ExecuteAsync_RespectsCancellation()
        {
            var executed = new List<int>();
            var cts = new CancellationTokenSource();

            _bus.RegisterAsyncSignalHandler<TestSignal>(async (s, ct) =>
            {
                if (s.Value == 2)
                    cts.Cancel();

                ct.ThrowIfCancellationRequested();
                executed.Add(s.Value);
            });

            var sequence = new SignalSequence(_bus)
                .Then(new TestSignal { Value = 1 })
                .Then(new TestSignal { Value = 2 })
                .Then(new TestSignal { Value = 3 });

            try
            {
                await sequence.ExecuteAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            Assert.AreEqual(1, executed.Count);
            Assert.AreEqual(1, executed[0]);
        }

        #region SignalSequenceRegistry Tests

        [Test]
        public void Registry_Create_RegistersSequence()
        {
            var registry = new SignalSequenceRegistry(_bus);

            registry.Create("test", seq => seq.Then(new TestSignal { Value = 1 }));

            Assert.IsTrue(registry.Contains("test"));
            Assert.IsNotNull(registry.Get("test"));

            registry.Dispose();
        }

        [Test]
        public void Registry_Execute_RunsNamedSequence()
        {
            int received = 0;
            _bus.RegisterSignalHandler<TestSignal>(s => received = s.Value);

            var registry = new SignalSequenceRegistry(_bus);
            registry.Create("spawn", seq => seq.Then(new TestSignal { Value = 100 }));

            registry.Execute("spawn");

            Assert.AreEqual(100, received);

            registry.Dispose();
        }

        [Test]
        public void Registry_Remove_DisposesSequence()
        {
            var registry = new SignalSequenceRegistry(_bus);
            registry.Create("test", seq => seq.Then(new TestSignal { Value = 1 }));

            Assert.IsTrue(registry.Remove("test"));
            Assert.IsFalse(registry.Contains("test"));

            registry.Dispose();
        }

        [Test]
        public void Registry_Clear_RemovesAllSequences()
        {
            var registry = new SignalSequenceRegistry(_bus);
            registry.Create("seq1", seq => seq.Then(new TestSignal { Value = 1 }));
            registry.Create("seq2", seq => seq.Then(new TestSignal { Value = 2 }));

            registry.Clear();

            Assert.IsFalse(registry.Contains("seq1"));
            Assert.IsFalse(registry.Contains("seq2"));

            registry.Dispose();
        }

        [Test]
        public async Task Registry_ExecuteAsync_RunsNamedSequenceAsync()
        {
            int received = 0;
            _bus.RegisterAsyncSignalHandler<TestSignal>(async (s, ct) =>
            {
                await Task.Yield();
                received = s.Value;
            });

            var registry = new SignalSequenceRegistry(_bus);
            registry.Create("async_test", seq => seq.Then(new TestSignal { Value = 200 }));

            await registry.ExecuteAsync("async_test");

            Assert.AreEqual(200, received);

            registry.Dispose();
        }

        #endregion

        private struct TestSignal
        {
            public int Value;
        }
    }
}
