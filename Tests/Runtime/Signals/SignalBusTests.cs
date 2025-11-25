using System;
using NUnit.Framework;
using Strada.Core.Signals;

namespace Strada.Core.Tests.Signals
{
    public struct TestSignal : IStructSignal
    {
        public int Value;
    }

    public struct AnotherSignal : IStructSignal
    {
        public string Message;
    }

    [TestFixture]
    public class SignalBusTests
    {
        private SignalBus _bus;

        [SetUp]
        public void SetUp()
        {
            _bus = new SignalBus();
            _bus.Clear<TestSignal>();
            _bus.Clear<AnotherSignal>();
        }

        [TearDown]
        public void TearDown()
        {
            _bus.Dispose();
        }

        [Test]
        public void Subscribe_And_Fire_Invokes_Handler()
        {
            int received = 0;
            _bus.Subscribe<TestSignal>(s => received = s.Value);

            _bus.Fire(new TestSignal { Value = 42 });

            Assert.AreEqual(42, received);
        }

        [Test]
        public void Fire_Without_Subscribers_Does_Not_Throw()
        {
            Assert.DoesNotThrow(() => _bus.Fire(new TestSignal { Value = 1 }));
        }

        [Test]
        public void Multiple_Subscribers_All_Receive_Signal()
        {
            int count = 0;
            _bus.Subscribe<TestSignal>(_ => count++);
            _bus.Subscribe<TestSignal>(_ => count++);
            _bus.Subscribe<TestSignal>(_ => count++);

            _bus.Fire(new TestSignal { Value = 1 });

            Assert.AreEqual(3, count);
        }

        [Test]
        public void Unsubscribe_Removes_Handler()
        {
            int count = 0;
            Action<TestSignal> handler = _ => count++;

            var binding = _bus.Subscribe<TestSignal>(handler);
            _bus.Fire(new TestSignal { Value = 1 });
            Assert.AreEqual(1, count);

            binding.Dispose();
            _bus.Fire(new TestSignal { Value = 2 });
            Assert.AreEqual(1, count);
        }

        [Test]
        public void Different_Signal_Types_Are_Independent()
        {
            int testCount = 0;
            int anotherCount = 0;

            _bus.Subscribe<TestSignal>(_ => testCount++);
            _bus.Subscribe<AnotherSignal>(_ => anotherCount++);

            _bus.Fire(new TestSignal { Value = 1 });

            Assert.AreEqual(1, testCount);
            Assert.AreEqual(0, anotherCount);
        }

        [Test]
        public void Clear_Removes_All_Handlers()
        {
            int count = 0;
            _bus.Subscribe<TestSignal>(_ => count++);
            _bus.Subscribe<TestSignal>(_ => count++);

            _bus.Clear<TestSignal>();
            _bus.Fire(new TestSignal { Value = 1 });

            Assert.AreEqual(0, count);
        }

        [Test]
        public void SignalBinding_Dispose_Unsubscribes()
        {
            int count = 0;

            using (_bus.Subscribe<TestSignal>(_ => count++))
            {
                _bus.Fire(new TestSignal { Value = 1 });
                Assert.AreEqual(1, count);
            }

            _bus.Fire(new TestSignal { Value = 2 });
            Assert.AreEqual(1, count);
        }

        [Test]
        public void Subscribe_Many_Handlers_Grows_Array()
        {
            int count = 0;

            for (int i = 0; i < 100; i++)
                _bus.Subscribe<TestSignal>(_ => count++);

            _bus.Fire(new TestSignal { Value = 1 });

            Assert.AreEqual(100, count);
        }

        [Test]
        public void Signal_Data_Is_Passed_Correctly()
        {
            TestSignal received = default;
            _bus.Subscribe<TestSignal>(s => received = s);

            _bus.Fire(new TestSignal { Value = 12345 });

            Assert.AreEqual(12345, received.Value);
        }
    }
}
