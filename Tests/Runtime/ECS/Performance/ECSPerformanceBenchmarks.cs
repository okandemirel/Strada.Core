using NUnit.Framework;
using Unity.PerformanceTesting;
using Strada.Core.ECS;
using Strada.Core.ECS.Storage;
using Unity.Collections;
using System.Diagnostics;

namespace Strada.Core.Tests.Performance
{
    [TestFixture]
    public class ECSPerformanceBenchmarks
    {
        struct TestComponent : IStradaComponent
        {
            public float X;
            public float Y;
            public float Z;
        }

        struct VelocityComponent : IStradaComponent
        {
            public float VX;
            public float VY;
            public float VZ;
        }

        struct HealthComponent : IStradaComponent
        {
            public int Health;
            public int MaxHealth;
        }

        [Test, Performance]
        public void SparseSet_Add_10k()
        {
            var sparseSet = new SparseSet<TestComponent>(10000, 10000, Allocator.Temp);

            Measure.Method(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    sparseSet.Add(i, new TestComponent { X = i, Y = i * 2, Z = i * 3 });
                }
            })
            .WarmupCount(5)
            .MeasurementCount(10)
            .Run();

            sparseSet.Dispose();
        }

        [Test, Performance]
        public void SparseSet_Remove_10k()
        {
            var sparseSet = new SparseSet<TestComponent>(10000, 10000, Allocator.Temp);

            for (int i = 0; i < 10000; i++)
            {
                sparseSet.Add(i, new TestComponent { X = i, Y = i * 2, Z = i * 3 });
            }

            Measure.Method(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    sparseSet.Remove(i);
                }
            })
            .WarmupCount(5)
            .MeasurementCount(10)
            .Run();

            sparseSet.Dispose();
        }

        [Test, Performance]
        public void SparseSet_Iterate_100k()
        {
            var sparseSet = new SparseSet<TestComponent>(100000, 100000, Allocator.Temp);

            for (int i = 0; i < 100000; i++)
            {
                sparseSet.Add(i, new TestComponent { X = i, Y = i * 2, Z = i * 3 });
            }

            Measure.Method(() =>
            {
                unsafe
                {
                    int* entities = sparseSet.GetDenseEntityReadOnlyPtr();
                    TestComponent* data = sparseSet.GetDataReadOnlyPtr();
                    int count = sparseSet.Count;

                    float sum = 0;
                    for (int i = 0; i < count; i++)
                    {
                        sum += data[i].X + data[i].Y + data[i].Z;
                    }
                }
            })
            .WarmupCount(5)
            .MeasurementCount(20)
            .Run();

            sparseSet.Dispose();
        }

        [Test, Performance]
        public void EntityManager_CreateEntity_10k()
        {
            var manager = new EntityManager();

            Measure.Method(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    manager.CreateEntity();
                }
            })
            .WarmupCount(5)
            .MeasurementCount(10)
            .Run();

            manager.Dispose();
        }

        [Test, Performance]
        public void EntityManager_AddComponent_10k()
        {
            var manager = new EntityManager();
            var entities = new Entity[10000];

            for (int i = 0; i < 10000; i++)
            {
                entities[i] = manager.CreateEntity();
            }

            Measure.Method(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    manager.AddComponent(entities[i], new TestComponent { X = i, Y = i * 2, Z = i * 3 });
                }
            })
            .WarmupCount(5)
            .MeasurementCount(10)
            .Run();

            manager.Dispose();
        }

        [Test, Performance]
        public void EntityManager_RemoveComponent_10k()
        {
            var manager = new EntityManager();
            var entities = new Entity[10000];

            for (int i = 0; i < 10000; i++)
            {
                entities[i] = manager.CreateEntity();
                manager.AddComponent(entities[i], new TestComponent { X = i, Y = i * 2, Z = i * 3 });
            }

            Measure.Method(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    manager.RemoveComponent<TestComponent>(entities[i]);
                }
            })
            .WarmupCount(5)
            .MeasurementCount(10)
            .Run();

            manager.Dispose();
        }

        [Test, Performance]
        public void EntityManager_GetComponent_10k()
        {
            var manager = new EntityManager();
            var entities = new Entity[10000];

            for (int i = 0; i < 10000; i++)
            {
                entities[i] = manager.CreateEntity();
                manager.AddComponent(entities[i], new TestComponent { X = i, Y = i * 2, Z = i * 3 });
            }

            Measure.Method(() =>
            {
                float sum = 0;
                for (int i = 0; i < 10000; i++)
                {
                    var comp = manager.GetComponent<TestComponent>(entities[i]);
                    sum += comp.X;
                }
            })
            .WarmupCount(5)
            .MeasurementCount(10)
            .Run();

            manager.Dispose();
        }

        [Test, Performance]
        public void EntityManager_DestroyEntity_10k()
        {
            var manager = new EntityManager();
            var entities = new Entity[10000];

            for (int i = 0; i < 10000; i++)
            {
                entities[i] = manager.CreateEntity();
                manager.AddComponent(entities[i], new TestComponent { X = i, Y = i * 2, Z = i * 3 });
            }

            Measure.Method(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    manager.DestroyEntity(entities[i]);
                }
            })
            .WarmupCount(5)
            .MeasurementCount(10)
            .Run();

            manager.Dispose();
        }

        [Test, Performance]
        public void EntityQuery_Cache_FirstAccess()
        {
            var manager = new EntityManager();

            for (int i = 0; i < 10000; i++)
            {
                var entity = manager.CreateEntity();
                manager.AddComponent(entity, new TestComponent { X = i, Y = i * 2, Z = i * 3 });
                manager.AddComponent(entity, new VelocityComponent { VX = 1, VY = 0, VZ = 0 });
            }

            Measure.Method(() =>
            {
                var query = manager.Query<TestComponent, VelocityComponent>();
                var entities = query.GetEntities();
            })
            .WarmupCount(5)
            .MeasurementCount(10)
            .Run();

            manager.Dispose();
        }

        [Test, Performance]
        public void EntityQuery_Cache_RepeatedAccess()
        {
            var manager = new EntityManager();

            for (int i = 0; i < 10000; i++)
            {
                var entity = manager.CreateEntity();
                manager.AddComponent(entity, new TestComponent { X = i, Y = i * 2, Z = i * 3 });
                manager.AddComponent(entity, new VelocityComponent { VX = 1, VY = 0, VZ = 0 });
            }

            var query = manager.Query<TestComponent, VelocityComponent>();
            query.GetEntities();

            Measure.Method(() =>
            {
                var entities = query.GetEntities();
            })
            .WarmupCount(5)
            .MeasurementCount(20)
            .Run();

            manager.Dispose();
        }

        [Test, Performance]
        public void ComponentStore_MultiType_10k()
        {
            var store = new ComponentStore();

            Measure.Method(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    var testStorage = store.GetOrCreateStorage<TestComponent>();
                    testStorage.Add(i, new TestComponent { X = i, Y = i * 2, Z = i * 3 });

                    var velStorage = store.GetOrCreateStorage<VelocityComponent>();
                    velStorage.Add(i, new VelocityComponent { VX = 1, VY = 0, VZ = 0 });

                    var healthStorage = store.GetOrCreateStorage<HealthComponent>();
                    healthStorage.Add(i, new HealthComponent { Health = 100, MaxHealth = 100 });
                }
            })
            .WarmupCount(5)
            .MeasurementCount(10)
            .Run();

            store.Dispose();
        }

        [Test, Performance]
        public void FullECSWorkflow_10k()
        {
            Measure.Method(() =>
            {
                var manager = new EntityManager();

                for (int i = 0; i < 10000; i++)
                {
                    var entity = manager.CreateEntity();
                    manager.AddComponent(entity, new TestComponent { X = i, Y = i * 2, Z = i * 3 });
                    manager.AddComponent(entity, new VelocityComponent { VX = 1, VY = 0, VZ = 0 });
                }

                var query = manager.Query<TestComponent, VelocityComponent>();
                var entities = query.GetEntities();

                var testStorage = manager.Store.GetOrCreateStorage<TestComponent>();
                var velStorage = manager.Store.GetOrCreateStorage<VelocityComponent>();

                foreach (var entityIndex in entities)
                {
                    var test = testStorage.Get(entityIndex);
                    var vel = velStorage.Get(entityIndex);
                    test.X += vel.VX;
                    test.Y += vel.VY;
                    test.Z += vel.VZ;
                    testStorage.Set(entityIndex, test);
                }

                manager.Dispose();
            })
            .WarmupCount(3)
            .MeasurementCount(10)
            .Run();
        }

        [Test, Performance]
        public void MemoryAllocation_EntityCreation()
        {
            var manager = new EntityManager();

            Measure.Method(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    manager.CreateEntity();
                }
            })
            .GC()
            .WarmupCount(5)
            .MeasurementCount(10)
            .Run();

            manager.Dispose();
        }

        [Test, Performance]
        public void MemoryAllocation_ComponentIteration()
        {
            var manager = new EntityManager();

            for (int i = 0; i < 10000; i++)
            {
                var entity = manager.CreateEntity();
                manager.AddComponent(entity, new TestComponent { X = i, Y = i * 2, Z = i * 3 });
            }

            var storage = manager.Store.GetOrCreateStorage<TestComponent>();

            Measure.Method(() =>
            {
                unsafe
                {
                    ref var sparseSet = ref storage.GetSparseSet();
                    int* entities = sparseSet.GetDenseEntityReadOnlyPtr();
                    TestComponent* data = sparseSet.GetDataReadOnlyPtr();
                    int count = sparseSet.Count;

                    float sum = 0;
                    for (int i = 0; i < count; i++)
                    {
                        sum += data[i].X;
                    }
                }
            })
            .GC()
            .WarmupCount(5)
            .MeasurementCount(20)
            .Run();

            manager.Dispose();
        }

        [Test, Performance]
        public void EntityManager_AddComponentBatch_10k()
        {
            var manager = new EntityManager();
            var entities = new Entity[10000];

            for (int i = 0; i < 10000; i++)
            {
                entities[i] = manager.CreateEntity();
            }

            Measure.Method(() =>
            {
                manager.AddComponentBatch(entities, new TestComponent { X = 1, Y = 2, Z = 3 });
            })
            .WarmupCount(5)
            .MeasurementCount(10)
            .Run();

            manager.Dispose();
        }

        [Test, Performance]
        public void EntityManager_RemoveComponentBatch_10k()
        {
            var manager = new EntityManager();
            var entities = new Entity[10000];

            for (int i = 0; i < 10000; i++)
            {
                entities[i] = manager.CreateEntity();
            }

            manager.AddComponentBatch(entities, new TestComponent { X = 1, Y = 2, Z = 3 });

            Measure.Method(() =>
            {
                manager.RemoveComponentBatch<TestComponent>(entities);
            })
            .WarmupCount(5)
            .MeasurementCount(10)
            .Run();

            manager.Dispose();
        }
    }
}
