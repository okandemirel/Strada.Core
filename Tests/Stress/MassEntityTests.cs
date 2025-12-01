using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Strada.Core.ECS;
using Strada.Core.ECS.Core;
using Strada.Core.ECS.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace Strada.Core.Tests.Stress
{
    public class MassEntityTests
    {
        private World _world;

        [SetUp]
        public void Setup()
        {
            _world = new ECSBuilder().Build();
            World.Current = _world;
        }

        [TearDown]
        public void Teardown()
        {
            _world?.Dispose();
            World.Current = null;
        }

        [Test]
        public void CreateAndIterate_10k_Entities()
        {
            int entityCount = 10000;
            var entities = new List<Entity>(entityCount);

            StressTestRunner.Run("Create 10k Entities", () =>
            {
                for (int i = 0; i < entityCount; i++)
                {
                    var entity = _world.EntityManager.CreateEntity();
                    _world.EntityManager.AddComponent(entity, new TestComponentA { Value = i });
                    entities.Add(entity);
                }
            });

            Assert.AreEqual(entityCount, _world.EntityManager.EntityCount);

            StressTestRunner.Run("Iterate 10k Entities", () =>
            {
                int count = 0;
                foreach (var entity in entities)
                {
                    if (_world.EntityManager.HasComponent<TestComponentA>(entity))
                    {
                        count++;
                    }
                }
                Assert.AreEqual(entityCount, count);
            });
        }

        private struct TestComponentA : IComponent
        {
            public int Value;
        }
    }
}
