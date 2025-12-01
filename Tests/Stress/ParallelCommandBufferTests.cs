using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Strada.Core.ECS;
using Strada.Core.ECS.Core;
using Strada.Core.ECS.Jobs;
using Strada.Core.ECS.World;
using Unity.Collections;
using UnityEngine;

namespace Strada.Core.Tests.Stress
{
    public class ParallelCommandBufferTests
    {
        private World _world;

        [SetUp]
        public void Setup()
        {
            _world = new ECSBuilder().Build();
            ComponentPlayback.EnsureHandler<TestComponentA>();
        }

        [TearDown]
        public void Teardown()
        {
            _world?.Dispose();
        }

        [Test]
        public void Parallel_CommandRecording_And_Playback_ShouldBeStable()
        {
            int threadCount = 20;
            int commandsPerThread = 1000;
            
            // We use one ECB per thread because the current EntityCommandBuffer implementation 
            // is not thread-safe for shared writing (uses NativeList without ParallelWriter).
            var buffers = new EntityCommandBuffer[threadCount];

            StressTestRunner.Run("Parallel ECB Recording", () =>
            {
                Parallel.For(0, threadCount, i =>
                {
                    // Allocator.Persistent is thread-safe for allocation
                    var ecb = new EntityCommandBuffer(Allocator.Persistent);
                    
                    for (int j = 0; j < commandsPerThread; j++)
                    {
                        // Deferred entity creation
                        int index = ecb.CreateEntity();
                        ecb.AddComponent(index, new TestComponentA { Value = i * commandsPerThread + j });
                    }
                    
                    buffers[i] = ecb;
                });
            });

            StressTestRunner.Run("Main Thread Playback", () =>
            {
                for (int i = 0; i < threadCount; i++)
                {
                    buffers[i].Playback(_world.EntityManager);
                    buffers[i].Dispose();
                }
            });

            Assert.AreEqual(threadCount * commandsPerThread, _world.EntityManager.EntityCount);
            
            // Verify data integrity
            int verifiedCount = 0;
            foreach (var index in _world.EntityManager.GetAllEntities())
            {
                var entity = _world.EntityManager.GetEntity(index);
                if (_world.EntityManager.HasComponent<TestComponentA>(entity))
                {
                    verifiedCount++;
                }
            }
            Assert.AreEqual(threadCount * commandsPerThread, verifiedCount);
        }

        private struct TestComponentA : IComponent
        {
            public int Value;
        }
    }
}
