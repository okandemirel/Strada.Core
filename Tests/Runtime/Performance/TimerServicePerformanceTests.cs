using NUnit.Framework;
using Strada.Core.Services;
using Unity.PerformanceTesting;

namespace Strada.Core.Tests.Runtime.Performance
{
    [TestFixture]
    [Category("Performance")]
    public sealed class TimerServicePerformanceTests
    {
        [Test, Performance]
        public void Benchmark_1k_ActiveTimers_Update()
        {
            var timerService = new TimerService();

            for (var i = 0; i < 1000; i++)
            {
                timerService.After(100f + i * 0.1f, () => { });
            }

            Measure.Method(() =>
            {
                timerService.Update(0.016f);
            })
            .WarmupCount(10)
            .MeasurementCount(50)
            .Run();

            timerService.Dispose();
        }

        [Test, Performance]
        public void Benchmark_TimerCreation_10k()
        {
            var timerService = new TimerService();

            Measure.Method(() =>
            {
                for (var i = 0; i < 10000; i++)
                {
                    timerService.After(100f, () => { });
                }
            })
            .WarmupCount(3)
            .MeasurementCount(10)
            .Run();

            timerService.Dispose();
        }

        [Test, Performance]
        public void Benchmark_TimerExpiration_1k()
        {
            var timerService = new TimerService();
            var count = 0;

            for (var i = 0; i < 1000; i++)
            {
                timerService.After(0.001f * i, () => count++);
            }

            Measure.Method(() =>
            {
                timerService.Update(2f);
            })
            .WarmupCount(1)
            .MeasurementCount(5)
            .SetUp(() =>
            {
                timerService.Dispose();
                timerService = new TimerService();
                count = 0;
                for (var i = 0; i < 1000; i++)
                {
                    timerService.After(0.001f * i, () => count++);
                }
            })
            .Run();

            timerService.Dispose();
        }
    }
}
