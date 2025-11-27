using NUnit.Framework;
using Strada.Core.Pooling;

namespace Strada.Core.Tests.Tests.Runtime.Pooling
{
    [TestFixture]
    public sealed class ObjectPoolTests
    {
        private sealed class TestPoolable : IPoolable
        {
            public int SpawnCount;
            public int DespawnCount;
            public bool IsActive;

            public void OnSpawn()
            {
                SpawnCount++;
                IsActive = true;
            }

            public void OnDespawn()
            {
                DespawnCount++;
                IsActive = false;
            }
        }

        [Test]
        public void Spawn_ReturnsNewInstance_WhenPoolEmpty()
        {
            var pool = new ObjectPool<TestPoolable>(() => new TestPoolable(), 4);

            var instance = pool.Spawn();

            Assert.IsNotNull(instance);
            Assert.AreEqual(1, instance.SpawnCount);
        }

        [Test]
        public void Spawn_ReusesInstance_AfterDespawn()
        {
            var pool = new ObjectPool<TestPoolable>(() => new TestPoolable(), 4);

            var first = pool.Spawn();
            pool.Despawn(first);
            var second = pool.Spawn();

            Assert.AreSame(first, second);
            Assert.AreEqual(2, first.SpawnCount);
        }

        [Test]
        public void Despawn_CallsOnDespawn()
        {
            var pool = new ObjectPool<TestPoolable>(() => new TestPoolable(), 4);

            var instance = pool.Spawn();
            pool.Despawn(instance);

            Assert.AreEqual(1, instance.DespawnCount);
            Assert.IsFalse(instance.IsActive);
        }

        [Test]
        public void Prewarm_CreatesInstances()
        {
            var createCount = 0;
            var pool = new ObjectPool<TestPoolable>(() =>
            {
                createCount++;
                return new TestPoolable();
            }, 0);

            pool.Prewarm(5);

            Assert.AreEqual(5, createCount);
            Assert.AreEqual(5, pool.AvailableCount);
        }

        [Test]
        public void Clear_DisposesAllPooledInstances()
        {
            var pool = new ObjectPool<TestPoolable>(() => new TestPoolable(), 4);

            var a = pool.Spawn();
            var b = pool.Spawn();
            pool.Despawn(a);
            pool.Despawn(b);
            pool.Clear();

            Assert.AreEqual(0, pool.AvailableCount);
        }

        [Test]
        public void ActiveCount_TracksCorrectly()
        {
            var pool = new ObjectPool<TestPoolable>(() => new TestPoolable(), 0);

            var a = pool.Spawn();
            var b = pool.Spawn();
            Assert.AreEqual(2, pool.ActiveCount);

            pool.Despawn(a);
            Assert.AreEqual(1, pool.ActiveCount);

            pool.Despawn(b);
            Assert.AreEqual(0, pool.ActiveCount);
        }
    }
}
