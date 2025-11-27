using NUnit.Framework;
using Strada.Core.Pooling;
using Unity.PerformanceTesting;

namespace Strada.Core.Tests.Runtime.Performance
{
    [TestFixture]
    [Category("Performance")]
    public sealed class PoolingPerformanceTests
    {
        private sealed class HeavyPoolable : IPoolable
        {
            public int[] Data = new int[1000];

            public void OnSpawn()
            {
                for (var i = 0; i < Data.Length; i++)
                    Data[i] = 0;
            }

            public void OnDespawn() { }
        }

        [Test, Performance]
        public void Benchmark_10k_PoolSpawnDespawn()
        {
            var pool = new ObjectPool<HeavyPoolable>(() => new HeavyPoolable(), 100);
            pool.Prewarm(100);

            Measure.Method(() =>
            {
                for (var i = 0; i < 10000; i++)
                {
                    var obj = pool.Spawn();
                    pool.Despawn(obj);
                }
            })
            .WarmupCount(3)
            .MeasurementCount(10)
            .Run();
        }

        [Test, Performance]
        public void Benchmark_10k_DirectAllocation()
        {
            Measure.Method(() =>
            {
                for (var i = 0; i < 10000; i++)
                {
                    var obj = new HeavyPoolable();
                    obj.OnSpawn();
                }
            })
            .WarmupCount(3)
            .MeasurementCount(10)
            .Run();
        }

        [Test, Performance]
        public void Benchmark_PoolRegistry_SpawnByType()
        {
            var registry = new PoolRegistry();
            registry.GetOrCreate(() => new HeavyPoolable(), 100);
            registry.Get<HeavyPoolable>().Prewarm(50);

            Measure.Method(() =>
            {
                for (var i = 0; i < 10000; i++)
                {
                    var obj = registry.Spawn<HeavyPoolable>();
                    registry.Despawn(obj);
                }
            })
            .WarmupCount(3)
            .MeasurementCount(10)
            .Run();

            registry.Dispose();
        }
    }
}
