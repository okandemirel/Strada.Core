using NUnit.Framework;
using Strada.Core.Services;

namespace Strada.Core.Tests.Services
{
    [TestFixture]
    public sealed class TimerServiceTests
    {
        private TimerService _timerService;

        [SetUp]
        public void SetUp()
        {
            _timerService = new TimerService();
        }

        [TearDown]
        public void TearDown()
        {
            _timerService.Dispose();
        }

        [Test]
        public void After_ExecutesCallback_WhenDelayElapsed()
        {
            var executed = false;
            _timerService.After(0.1f, () => executed = true);

            _timerService.Update(0.05f);
            Assert.IsFalse(executed);

            _timerService.Update(0.06f);
            Assert.IsTrue(executed);
        }

        [Test]
        public void Every_ExecutesRepeatedly()
        {
            var count = 0;
            _timerService.Every(0.1f, () => count++);

            _timerService.Update(0.1f);
            Assert.AreEqual(1, count);

            _timerService.Update(0.1f);
            Assert.AreEqual(2, count);

            _timerService.Update(0.1f);
            Assert.AreEqual(3, count);
        }

        [Test]
        public void Cancel_StopsTimer()
        {
            var count = 0;
            var handle = _timerService.Every(0.1f, () => count++);

            _timerService.Update(0.1f);
            Assert.AreEqual(1, count);

            handle.Cancel();
            _timerService.Update(0.1f);
            Assert.AreEqual(1, count);
        }

        [Test]
        public void Schedule_ExecutesWithDelayAndInterval()
        {
            var executed = false;
            _timerService.Schedule(0.5f, 0f, 1, () => executed = true);

            _timerService.Update(0.1f);
            Assert.IsFalse(executed);

            _timerService.Update(0.3f);
            Assert.IsFalse(executed);

            _timerService.Update(0.2f);
            Assert.IsTrue(executed);
        }

        [Test]
        public void Pause_StopsTimerProgress()
        {
            var executed = false;
            var handle = _timerService.After(0.2f, () => executed = true);

            _timerService.Update(0.1f);
            handle.Pause();
            _timerService.Update(0.2f);

            Assert.IsFalse(executed);
        }

        [Test]
        public void Resume_ContinuesTimer()
        {
            var executed = false;
            var handle = _timerService.After(0.2f, () => executed = true);

            _timerService.Update(0.1f);
            handle.Pause();
            _timerService.Update(0.5f);
            handle.Resume();
            _timerService.Update(0.15f);

            Assert.IsTrue(executed);
        }

        [Test]
        public void CancelAll_StopsAllTimers()
        {
            var count = 0;
            _timerService.After(0.1f, () => count++);
            _timerService.After(0.1f, () => count++);
            _timerService.After(0.1f, () => count++);

            _timerService.CancelAll();
            _timerService.Update(0.2f);

            Assert.AreEqual(0, count);
        }

        [Test]
        public void Every_WithRepeatCount_StopsAfterCount()
        {
            var count = 0;
            _timerService.Every(0.1f, () => count++, 3);

            _timerService.Update(0.1f);
            _timerService.Update(0.1f);
            _timerService.Update(0.1f);
            _timerService.Update(0.1f);
            _timerService.Update(0.1f);

            Assert.AreEqual(3, count);
        }

        [Test]
        public void IsActive_ReturnsCorrectState()
        {
            var handle = _timerService.After(1f, () => { });

            Assert.IsTrue(handle.IsActive);

            handle.Cancel();

            Assert.IsFalse(handle.IsActive);
        }
    }
}
