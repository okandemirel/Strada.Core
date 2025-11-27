using System.Collections.Generic;
using FsCheck;
using NUnit.Framework;
using Strada.Core.ECS;
using Strada.Core.ECS.Query;
using Strada.Core.Tests.Generators;

namespace Strada.Core.Tests.ECS.Query
{
    /// <summary>
    /// Property-based tests for ECS query iteration.
    /// Tests verify correctness properties that must hold across all valid inputs.
    /// </summary>
    [TestFixture]
    public class QueryPropertyTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            StradaArbitraries.RegisterAll();
        }

        #region Property 8: Query Completeness

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 8: Query Completeness**
        /// For any set of entities with varying component combinations,
        /// ForEach&lt;T1&gt; SHALL iterate exactly those entities having the specified component type.
        /// **Validates: Requirements 3.4**
        /// </summary>
        [Test]
        public void QueryCompleteness_SingleComponent_IteratesExactlyMatchingEntities()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                Gen.Choose(1, 50).ToArbitrary(),
                Gen.Choose(0, 50).ToArbitrary(),
                (withComponentCount, withoutComponentCount) =>
                {
                    var manager = new EntityManager();

                    try
                    {
                        var entitiesWithComponent = new HashSet<int>();
                        var entitiesWithoutComponent = new HashSet<int>();

                        // Create entities WITH TestComponent
                        for (int i = 0; i < withComponentCount; i++)
                        {
                            var entity = manager.CreateEntity();
                            manager.AddComponent(entity, new TestComponent(i, i * 1.5f, true));
                            entitiesWithComponent.Add(entity.Index);
                        }

                        // Create entities WITHOUT TestComponent
                        for (int i = 0; i < withoutComponentCount; i++)
                        {
                            var entity = manager.CreateEntity();
                            // Don't add TestComponent
                            entitiesWithoutComponent.Add(entity.Index);
                        }

                        // Query and collect iterated entities
                        var iteratedEntities = new HashSet<int>();
                        manager.ForEach<TestComponent>((int entityIndex, ref TestComponent c) =>
                        {
                            iteratedEntities.Add(entityIndex);
                        });

                        // Verify: iterated entities should exactly match entities with component
                        if (iteratedEntities.Count != entitiesWithComponent.Count)
                            return false;

                        foreach (var entityIndex in entitiesWithComponent)
                        {
                            if (!iteratedEntities.Contains(entityIndex))
                                return false;
                        }

                        // Verify: no entity without component was iterated
                        foreach (var entityIndex in entitiesWithoutComponent)
                        {
                            if (iteratedEntities.Contains(entityIndex))
                                return false;
                        }

                        return true;
                    }
                    finally
                    {
                        manager.Dispose();
                    }
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 8: Query Completeness**
        /// For any set of entities with varying component combinations,
        /// ForEach&lt;T1, T2&gt; SHALL iterate exactly those entities having BOTH component types.
        /// **Validates: Requirements 3.4**
        /// </summary>
        [Test]
        public void QueryCompleteness_TwoComponents_IteratesExactlyMatchingEntities()
        {
            var config = PropertyTestConfig.CreateConfig();

            // Use a tuple generator to combine 4 parameters into one
            var countsGen = from bothCount in Gen.Choose(1, 30)
                            from onlyFirstCount in Gen.Choose(0, 20)
                            from onlySecondCount in Gen.Choose(0, 20)
                            from neitherCount in Gen.Choose(0, 20)
                            select (bothCount, onlyFirstCount, onlySecondCount, neitherCount);

            var property = Prop.ForAll(
                countsGen.ToArbitrary(),
                (counts) =>
                {
                    var (bothCount, onlyFirstCount, onlySecondCount, neitherCount) = counts;
                    var manager = new EntityManager();

                    try
                    {
                        var entitiesWithBoth = new HashSet<int>();

                        // Create entities with BOTH components
                        for (int i = 0; i < bothCount; i++)
                        {
                            var entity = manager.CreateEntity();
                            manager.AddComponent(entity, new TestComponent(i, 0, true));
                            manager.AddComponent(entity, new TestComponent2(i, i * 2));
                            entitiesWithBoth.Add(entity.Index);
                        }

                        // Create entities with only TestComponent
                        for (int i = 0; i < onlyFirstCount; i++)
                        {
                            var entity = manager.CreateEntity();
                            manager.AddComponent(entity, new TestComponent(i + 100, 0, false));
                        }

                        // Create entities with only TestComponent2
                        for (int i = 0; i < onlySecondCount; i++)
                        {
                            var entity = manager.CreateEntity();
                            manager.AddComponent(entity, new TestComponent2(i + 200, i));
                        }

                        // Create entities with neither component
                        for (int i = 0; i < neitherCount; i++)
                        {
                            manager.CreateEntity();
                        }

                        // Query and collect iterated entities
                        var iteratedEntities = new HashSet<int>();
                        manager.ForEach<TestComponent, TestComponent2>(
                            (int entityIndex, ref TestComponent c1, ref TestComponent2 c2) =>
                            {
                                iteratedEntities.Add(entityIndex);
                            });

                        // Verify: iterated entities should exactly match entities with both components
                        if (iteratedEntities.Count != entitiesWithBoth.Count)
                            return false;

                        foreach (var entityIndex in entitiesWithBoth)
                        {
                            if (!iteratedEntities.Contains(entityIndex))
                                return false;
                        }

                        return true;
                    }
                    finally
                    {
                        manager.Dispose();
                    }
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 8: Query Completeness**
        /// For any set of entities with varying component combinations,
        /// ForEach&lt;T1, T2, T3&gt; SHALL iterate exactly those entities having ALL THREE component types.
        /// **Validates: Requirements 3.4**
        /// </summary>
        [Test]
        public void QueryCompleteness_ThreeComponents_IteratesExactlyMatchingEntities()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                Gen.Choose(1, 20).ToArbitrary(),
                Gen.Choose(0, 15).ToArbitrary(),
                (allThreeCount, partialCount) =>
                {
                    var manager = new EntityManager();

                    try
                    {
                        var entitiesWithAll = new HashSet<int>();

                        // Create entities with ALL THREE components
                        for (int i = 0; i < allThreeCount; i++)
                        {
                            var entity = manager.CreateEntity();
                            manager.AddComponent(entity, new TestComponent(i, 0, true));
                            manager.AddComponent(entity, new TestComponent2(i, i));
                            manager.AddComponent(entity, new TestComponent3(i, i));
                            entitiesWithAll.Add(entity.Index);
                        }

                        // Create entities with only two components (various combinations)
                        for (int i = 0; i < partialCount; i++)
                        {
                            var entity = manager.CreateEntity();
                            manager.AddComponent(entity, new TestComponent(i + 100, 0, false));
                            manager.AddComponent(entity, new TestComponent2(i + 100, i));
                            // Missing TestComponent3
                        }

                        for (int i = 0; i < partialCount; i++)
                        {
                            var entity = manager.CreateEntity();
                            manager.AddComponent(entity, new TestComponent(i + 200, 0, false));
                            manager.AddComponent(entity, new TestComponent3(i + 200, i));
                            // Missing TestComponent2
                        }

                        // Query and collect iterated entities
                        var iteratedEntities = new HashSet<int>();
                        manager.ForEach<TestComponent, TestComponent2, TestComponent3>(
                            (int entityIndex, ref TestComponent c1, ref TestComponent2 c2, ref TestComponent3 c3) =>
                            {
                                iteratedEntities.Add(entityIndex);
                            });

                        // Verify: iterated entities should exactly match entities with all three components
                        if (iteratedEntities.Count != entitiesWithAll.Count)
                            return false;

                        foreach (var entityIndex in entitiesWithAll)
                        {
                            if (!iteratedEntities.Contains(entityIndex))
                                return false;
                        }

                        return true;
                    }
                    finally
                    {
                        manager.Dispose();
                    }
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 8: Query Completeness**
        /// Additional test: Query returns correct component data for each entity.
        /// **Validates: Requirements 3.4**
        /// </summary>
        [Test]
        public void QueryCompleteness_ReturnsCorrectComponentData()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                Gen.Choose(1, 50).ToArbitrary(),
                (entityCount) =>
                {
                    var manager = new EntityManager();

                    try
                    {
                        var expectedData = new Dictionary<int, int>();

                        // Create entities with unique component values
                        for (int i = 0; i < entityCount; i++)
                        {
                            var entity = manager.CreateEntity();
                            manager.AddComponent(entity, new TestComponent(i * 10, 0, true));
                            expectedData[entity.Index] = i * 10;
                        }

                        // Query and verify data
                        bool allCorrect = true;
                        manager.ForEach<TestComponent>((int entityIndex, ref TestComponent c) =>
                        {
                            if (!expectedData.TryGetValue(entityIndex, out int expected) || c.Value != expected)
                            {
                                allCorrect = false;
                            }
                        });

                        return allCorrect;
                    }
                    finally
                    {
                        manager.Dispose();
                    }
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 8: Query Completeness**
        /// Additional test: Query excludes destroyed entities.
        /// **Validates: Requirements 3.4**
        /// </summary>
        [Test]
        public void QueryCompleteness_ExcludesDestroyedEntities()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                Gen.Choose(2, 50).ToArbitrary(),
                Gen.Choose(1, 25).ToArbitrary(),
                (totalCount, destroyCount) =>
                {
                    var manager = new EntityManager();

                    try
                    {
                        int actualDestroyCount = System.Math.Min(destroyCount, totalCount - 1);
                        var entities = new List<Entity>();
                        var survivingEntities = new HashSet<int>();

                        // Create entities
                        for (int i = 0; i < totalCount; i++)
                        {
                            var entity = manager.CreateEntity();
                            manager.AddComponent(entity, new TestComponent(i, 0, true));
                            entities.Add(entity);
                            survivingEntities.Add(entity.Index);
                        }

                        // Destroy some entities
                        for (int i = 0; i < actualDestroyCount; i++)
                        {
                            manager.DestroyEntity(entities[i]);
                            survivingEntities.Remove(entities[i].Index);
                        }

                        // Query and collect iterated entities
                        var iteratedEntities = new HashSet<int>();
                        manager.ForEach<TestComponent>((int entityIndex, ref TestComponent c) =>
                        {
                            iteratedEntities.Add(entityIndex);
                        });

                        // Verify: only surviving entities are iterated
                        if (iteratedEntities.Count != survivingEntities.Count)
                            return false;

                        foreach (var entityIndex in survivingEntities)
                        {
                            if (!iteratedEntities.Contains(entityIndex))
                                return false;
                        }

                        return true;
                    }
                    finally
                    {
                        manager.Dispose();
                    }
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 8: Query Completeness**
        /// Additional test: Empty query iterates zero entities.
        /// **Validates: Requirements 3.4**
        /// </summary>
        [Test]
        public void QueryCompleteness_EmptyQuery_IteratesZeroEntities()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                Gen.Choose(0, 50).ToArbitrary(),
                (entityCount) =>
                {
                    var manager = new EntityManager();

                    try
                    {
                        // Create entities WITHOUT the queried component
                        for (int i = 0; i < entityCount; i++)
                        {
                            var entity = manager.CreateEntity();
                            // Add a different component
                            manager.AddComponent(entity, new TestComponent2(i, i));
                        }

                        // Query for TestComponent (which no entity has)
                        int iteratedCount = 0;
                        manager.ForEach<TestComponent>((int entityIndex, ref TestComponent c) =>
                        {
                            iteratedCount++;
                        });

                        return iteratedCount == 0;
                    }
                    finally
                    {
                        manager.Dispose();
                    }
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 8: Query Completeness**
        /// Additional test: Query can modify component data.
        /// **Validates: Requirements 3.4**
        /// </summary>
        [Test]
        public void QueryCompleteness_CanModifyComponentData()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                Gen.Choose(1, 50).ToArbitrary(),
                Gen.Choose(1, 100).ToArbitrary(),
                (entityCount, multiplier) =>
                {
                    var manager = new EntityManager();

                    try
                    {
                        var entities = new List<Entity>();

                        // Create entities
                        for (int i = 0; i < entityCount; i++)
                        {
                            var entity = manager.CreateEntity();
                            manager.AddComponent(entity, new TestComponent(i, 0, true));
                            entities.Add(entity);
                        }

                        // Modify via query
                        manager.ForEach<TestComponent>((int entityIndex, ref TestComponent c) =>
                        {
                            c.Value *= multiplier;
                        });

                        // Verify modifications
                        for (int i = 0; i < entityCount; i++)
                        {
                            var component = manager.GetComponent<TestComponent>(entities[i]);
                            if (component.Value != i * multiplier)
                                return false;
                        }

                        return true;
                    }
                    finally
                    {
                        manager.Dispose();
                    }
                });

            property.Check(config);
        }

        #endregion
    }
}
