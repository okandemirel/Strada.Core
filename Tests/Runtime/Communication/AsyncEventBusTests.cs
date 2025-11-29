using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Strada.Core.Commands;
using Strada.Core.Communication;

namespace Strada.Core.Tests.Tests.Runtime.Communication
{
    [TestFixture]
    public class AsyncEventBusTests
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

        #region Async Signal Tests

        [Test]
        public async Task SendAsync_WithRegisteredHandler_ExecutesHandler()
        {
            int received = 0;
            _bus.RegisterAsyncSignalHandler<TestSignal>(async (s, ct) =>
            {
                await Task.Yield();
                received = s.Value;
            });

            await _bus.SendAsync(new TestSignal { Value = 42 });

            Assert.AreEqual(42, received);
        }

        [Test]
        public async Task SendAsync_WithDelegateHandler_ExecutesHandler()
        {
            int received = 0;
            _bus.RegisterAsyncSignalHandler<TestSignal>((s, ct) =>
            {
                received = s.Value;
                return default;
            });

            await _bus.SendAsync(new TestSignal { Value = 99 });

            Assert.AreEqual(99, received);
        }

        [Test]
        public async Task SendAsync_WithInterfaceHandler_ExecutesHandler()
        {
            var handler = new TestAsyncSignalHandler();
            _bus.RegisterAsyncSignalHandler(handler);

            await _bus.SendAsync(new TestSignal { Value = 77 });

            Assert.AreEqual(77, handler.LastValue);
        }

        [Test]
        public void SendAsync_WithoutHandler_ThrowsException()
        {
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _bus.SendAsync(new UnhandledSignal()));
        }

        [Test]
        public async Task SendAsync_RespectsCancellation()
        {
            var cts = new CancellationTokenSource();
            bool handlerStarted = false;
            bool handlerCompleted = false;

            _bus.RegisterAsyncSignalHandler<TestSignal>(async (s, ct) =>
            {
                handlerStarted = true;
                await Task.Delay(100, ct);
                handlerCompleted = true;
            });

            cts.CancelAfter(10);

            try
            {
                await _bus.SendAsync(new TestSignal { Value = 1 }, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            Assert.IsTrue(handlerStarted);
            Assert.IsFalse(handlerCompleted);
        }

        [Test]
        public async Task SendAsync_MultipleSignalTypes_RoutesCorrectly()
        {
            int signal1Value = 0;
            string signal2Value = null;

            _bus.RegisterAsyncSignalHandler<TestSignal>((s, ct) =>
            {
                signal1Value = s.Value;
                return default;
            });

            _bus.RegisterAsyncSignalHandler<StringSignal>((s, ct) =>
            {
                signal2Value = s.Text;
                return default;
            });

            await _bus.SendAsync(new TestSignal { Value = 10 });
            await _bus.SendAsync(new StringSignal { Text = "hello" });

            Assert.AreEqual(10, signal1Value);
            Assert.AreEqual("hello", signal2Value);
        }

        #endregion

        #region Async Query Tests

        [Test]
        public async Task QueryAsync_WithRegisteredHandler_ReturnsResult()
        {
            _bus.RegisterAsyncQueryHandler<GetDataQuery, int>(async (q, ct) =>
            {
                await Task.Yield();
                return q.Input * 10;
            });

            var result = await _bus.QueryAsync<GetDataQuery, int>(new GetDataQuery { Input = 5 });

            Assert.AreEqual(50, result);
        }

        [Test]
        public async Task QueryAsync_WithInterfaceHandler_ReturnsResult()
        {
            var handler = new TestAsyncQueryHandler();
            _bus.RegisterAsyncQueryHandler<GetDataQuery, int>(handler);

            var result = await _bus.QueryAsync<GetDataQuery, int>(new GetDataQuery { Input = 7 });

            Assert.AreEqual(70, result);
        }

        [Test]
        public void QueryAsync_WithoutHandler_ThrowsException()
        {
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _bus.QueryAsync<UnhandledAsyncQuery, int>(new UnhandledAsyncQuery()));
        }

        [Test]
        public async Task QueryAsync_StringResult_ReturnsCorrectType()
        {
            _bus.RegisterAsyncQueryHandler<GetNameAsyncQuery, string>(async (q, ct) =>
            {
                await Task.Yield();
                return $"Result_{q.Id}";
            });

            var result = await _bus.QueryAsync<GetNameAsyncQuery, string>(
                new GetNameAsyncQuery { Id = 42 });

            Assert.AreEqual("Result_42", result);
        }

        [Test]
        public async Task QueryAsync_ComplexResult_ReturnsCorrectObject()
        {
            _bus.RegisterAsyncQueryHandler<GetItemsQuery, List<int>>(async (q, ct) =>
            {
                await Task.Yield();
                var list = new List<int>();
                for (int i = 0; i < q.Count; i++)
                    list.Add(i);
                return list;
            });

            var result = await _bus.QueryAsync<GetItemsQuery, List<int>>(
                new GetItemsQuery { Count = 5 });

            Assert.AreEqual(5, result.Count);
            Assert.AreEqual(0, result[0]);
            Assert.AreEqual(4, result[4]);
        }

        #endregion

        #region Handler Overwrite Tests

        [Test]
        public async Task RegisterAsyncSignalHandler_OverwritesPreviousHandler()
        {
            int handler1Called = 0;
            int handler2Called = 0;

            _bus.RegisterAsyncSignalHandler<TestSignal>((s, ct) =>
            {
                handler1Called++;
                return default;
            });

            _bus.RegisterAsyncSignalHandler<TestSignal>((s, ct) =>
            {
                handler2Called++;
                return default;
            });

            await _bus.SendAsync(new TestSignal());

            Assert.AreEqual(0, handler1Called);
            Assert.AreEqual(1, handler2Called);
        }

        [Test]
        public async Task RegisterAsyncQueryHandler_OverwritesPreviousHandler()
        {
            _bus.RegisterAsyncQueryHandler<GetDataQuery, int>((q, ct) => new ValueTask<int>(1));
            _bus.RegisterAsyncQueryHandler<GetDataQuery, int>((q, ct) => new ValueTask<int>(2));

            var result = await _bus.QueryAsync<GetDataQuery, int>(new GetDataQuery());

            Assert.AreEqual(2, result);
        }

        #endregion

        #region Clear Tests

        [Test]
        public void Clear_RemovesAsyncSignalHandlers()
        {
            _bus.RegisterAsyncSignalHandler<TestSignal>((s, ct) => default);
            _bus.Clear();

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _bus.SendAsync(new TestSignal()));
        }

        [Test]
        public void Clear_RemovesAsyncQueryHandlers()
        {
            _bus.RegisterAsyncQueryHandler<GetDataQuery, int>((q, ct) => new ValueTask<int>(1));
            _bus.Clear();

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _bus.QueryAsync<GetDataQuery, int>(new GetDataQuery()));
        }

        #endregion

        #region Test Types

        private struct TestSignal
        {
            public int Value;
        }

        private struct StringSignal
        {
            public string Text;
        }

        private struct UnhandledSignal { }

        private struct GetDataQuery : IAsyncQuery<int>
        {
            public int Input;
        }

        private struct GetNameAsyncQuery : IAsyncQuery<string>
        {
            public int Id;
        }

        private struct GetItemsQuery : IAsyncQuery<List<int>>
        {
            public int Count;
        }

        private struct UnhandledAsyncQuery : IAsyncQuery<int> { }

        private class TestAsyncSignalHandler : IAsyncSignalHandler<TestSignal>
        {
            public int LastValue;

            public ValueTask HandleAsync(TestSignal signal, CancellationToken ct = default)
            {
                LastValue = signal.Value;
                return default;
            }
        }

        private class TestAsyncQueryHandler : IAsyncQueryHandler<GetDataQuery, int>
        {
            public ValueTask<int> HandleAsync(GetDataQuery query, CancellationToken ct = default)
            {
                return new ValueTask<int>(query.Input * 10);
            }
        }

        #endregion
    }
}
