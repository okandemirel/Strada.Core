using NUnit.Framework;
using Strada.Core.ECS;
using Strada.Core.ECS.Core;
using Strada.Core.ECS.Jobs;
using Strada.Core.ECS.Query;
using Unity.Collections;

namespace Strada.Core.Tests.Tests.Runtime.ECS
{
    [TestFixture]
    public class ECSIterationSafetyTests
    {
        private EntityManager _manager;

        private struct TestComponent : IComponent { public int Value; }

        [SetUp]
        public void Setup() => _manager = new EntityManager();

        [TearDown]
        public void TearDown() => _manager?.Dispose();

        [Test]
        public void ForEach_DestroyEntity_SkipsElements()
        {
            var e1 = _manager.CreateEntity(); _manager.AddComponent(e1, new TestComponent { Value = 1 });
            var e2 = _manager.CreateEntity(); _manager.AddComponent(e2, new TestComponent { Value = 2 });
            var e3 = _manager.CreateEntity(); _manager.AddComponent(e3, new TestComponent { Value = 3 });

            int processedCount = 0;
            
            _manager.ForEach((int entityIndex, ref TestComponent c) =>
            {
                var entity = _manager.GetEntity(entityIndex);
                if (c.Value == 1)
                {
                    _manager.DestroyEntity(entity);
                }
                processedCount++;
            });

            Assert.AreEqual(3, processedCount, "Legacy ForEach iterates original count, processing moved entities at old indices (stale but counted).");
            Assert.IsFalse(_manager.Exists(e1));
            Assert.IsTrue(_manager.Exists(e2));
            Assert.IsTrue(_manager.Exists(e3));
        }

        [Test]
        public void ForEach_WithECB_IsSafe()
        {
            var e1 = _manager.CreateEntity(); _manager.AddComponent(e1, new TestComponent { Value = 1 });
            var e2 = _manager.CreateEntity(); _manager.AddComponent(e2, new TestComponent { Value = 2 });
            var e3 = _manager.CreateEntity(); _manager.AddComponent(e3, new TestComponent { Value = 3 });

            int processedCount = 0;
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            _manager.ForEach((int entityIndex, ref TestComponent c) =>
            {
                processedCount++;
                if (c.Value == 1)
                {
                    var entity = _manager.GetEntity(entityIndex);
                    ecb.DestroyEntity(entity);
                }
            });

            ecb.Playback(_manager);
            ecb.Dispose();

            Assert.AreEqual(3, processedCount);
            Assert.IsFalse(_manager.Exists(e1));
            Assert.IsTrue(_manager.Exists(e2));
            Assert.IsTrue(_manager.Exists(e3));
        }
    }
}
