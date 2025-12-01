using System;
using System.Diagnostics;
using NUnit.Framework;
using Strada.Core.ECS;
using Strada.Core.ECS.Core;
using Strada.Core.ECS.Query;
using Unity.Collections;

namespace Strada.Core.Tests.Tests.Runtime.Performance
{
    [TestFixture]
    [Category("Performance")]
    public class RealisticSimulationTests
    {
        private EntityManager _manager;
        private const int EntityCount = 100_000;

        private struct Position : IComponent { public float X, Y, Z; }
        private struct Velocity : IComponent { public float X, Y, Z; }
        private struct Health : IComponent { public int Value; }

        [SetUp]
        public void Setup()
        {
            _manager = new EntityManager(EntityCount);
            // Create entities with mixed components to simulate fragmentation
            for (int i = 0; i < EntityCount; i++)
            {
                var e = _manager.CreateEntity();
                _manager.AddComponent(e, new Position { X = i });
                if (i % 2 == 0) _manager.AddComponent(e, new Velocity { X = 1 });
                if (i % 3 == 0) _manager.AddComponent(e, new Health { Value = 100 });
            }
        }

        [TearDown]
        public void TearDown() => _manager?.Dispose();

        [Test]
        public void Simulation_MixedReadWrite()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            _manager.ForEach((int e, ref Position p, ref Velocity v) =>
            {
                v.Y -= 9.81f * 0.016f; // dt
            });

            _manager.ForEach((int e, ref Position p, ref Velocity v) =>
            {
                p.X += v.X * 0.016f;
                p.Y += v.Y * 0.016f;
            });

            stopwatch.Stop();
            UnityEngine.Debug.Log($"Mixed Read/Write ({EntityCount} entities): {stopwatch.Elapsed.TotalMilliseconds} ms");
            
            Assert.Less(stopwatch.Elapsed.TotalMilliseconds, 50.0);
        }

        [Test]
        public void Simulation_CacheThrashing()
        {
            var randomIndices = new NativeArray<int>(EntityCount, Allocator.Temp);
            var rand = new Random(12345);
            for (int i = 0; i < EntityCount; i++)
                randomIndices[i] = rand.Next(1, EntityCount); 

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            for (int i = 0; i < EntityCount; i++)
            {
                int index = randomIndices[i];
                var entity = _manager.GetEntity(index); 
                if (_manager.Exists(entity))
                {
                    if (_manager.HasComponent<Position>(entity))
                    {
                        ref var p = ref _manager.GetComponentRef<Position>(entity);
                        p.X += 1;
                    }
                }
            }

            stopwatch.Stop();
            randomIndices.Dispose();
            
            UnityEngine.Debug.Log($"Random Access ({EntityCount} ops): {stopwatch.Elapsed.TotalMilliseconds} ms");
            
            Assert.Less(stopwatch.Elapsed.TotalMilliseconds, 500.0);
        }
    }
}
