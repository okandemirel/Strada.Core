using System.Collections.Generic;
using FsCheck;
using NUnit.Framework;
using Strada.Core.ECS;
using Strada.Core.ECS.Storage;
using Strada.Core.Tests.Generators;
using Unity.Collections;

namespace Strada.Core.Tests.ECS.Storage
{
    /// <summary>
    /// Property-based tests for ECS component storage.
    /// Tests verify correctness properties that must hold across all valid inputs.
    /// </summary>
    [TestFixture]
    public class ComponentPropertyTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            StradaArbitraries.RegisterAll();
        }

        #region Property 7: Component Storage Integrity

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 7: Component Storage Integrity**
        /// For any entity and component type, after AddComponent the entity SHALL have
        /// HasComponent=true and GetComponent SHALL return the added value.
        /// **Validates: Requirements 3.3**
        /// </summary>
        [Test]
        public void ComponentStorageIntegrity_AddedComponentCanBeRetrieved()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                Gen.Choose(1, 100).ToArbitrary(),
                ComponentGenerator.TestComponentArbitrary,
                (entityCount, component) =>
                {
                    var manager = new EntityManager();

                    try
                    {
                        var entities = new List<Entity>();

                        // Create entities and add component to each
                        for (int i = 0; i < entityCount; i++)
                        {
                            var entity = manager.CreateEntity();
                            entities.Add(entity);
                            manager.AddComponent(entity, component);
                        }

                        // Verify all entities have the component with correct value
                        foreach (var entity in entities)
                        {
                            if (!manager.HasComponent<TestComponent>(entity))
                                return false;

                            var retrieved = manager.GetComponent<TestComponent>(entity);
                            if (retrieved.Value != component.Value ||
                                retrieved.FloatValue != component.FloatValue ||
                                retrieved.IsActive != component.IsActive)
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
        /// **Feature: strada-codebase-audit, Property 7: Component Storage Integrity**
        /// Additional test: Each entity can have different component values.
        /// **Validates: Requirements 3.3**
        /// </summary>
        [Test]
        public void ComponentStorageIntegrity_DifferentEntitiesHaveDifferentValues()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                Gen.Choose(2, 50).ToArbitrary(),
                (entityCount) =>
                {
                    var manager = new EntityManager();

                    try
                    {
                        var entities = new List<Entity>();

                        // Create entities with unique component values
                        for (int i = 0; i < entityCount; i++)
                        {
                            var entity = manager.CreateEntity();
                            entities.Add(entity);
                            manager.AddComponent(entity, new TestComponent(i, i * 1.5f, i % 2 == 0));
                        }

                        // Verify each entity has its unique value
                        for (int i = 0; i < entityCount; i++)
                        {
                            var retrieved = manager.GetComponent<TestComponent>(entities[i]);
                            if (retrieved.Value != i)
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
        /// **Feature: strada-codebase-audit, Property 7: Component Storage Integrity**
        /// Additional test: SetComponent updates the value correctly.
        /// **Validates: Requirements 3.3**
        /// </summary>
        [Test]
        public void ComponentStorageIntegrity_SetComponentUpdatesValue()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                ComponentGenerator.TestComponentArbitrary,
                ComponentGenerator.TestComponentArbitrary,
                (initialComponent, updatedComponent) =>
                {
                    var manager = new EntityManager();

                    try
                    {
                        var entity = manager.CreateEntity();
                        manager.AddComponent(entity, initialComponent);

                        // Update the component
                        manager.SetComponent(entity, updatedComponent);

                        // Verify the updated value
                        var retrieved = manager.GetComponent<TestComponent>(entity);
                        return retrieved.Value == updatedComponent.Value &&
                               retrieved.FloatValue == updatedComponent.FloatValue &&
                               retrieved.IsActive == updatedComponent.IsActive;
                    }
                    finally
                    {
                        manager.Dispose();
                    }
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 7: Component Storage Integrity**
        /// Additional test: RemoveComponent makes HasComponent return false.
        /// **Validates: Requirements 3.3**
        /// </summary>
        [Test]
        public void ComponentStorageIntegrity_RemovedComponentNotPresent()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                Gen.Choose(1, 50).ToArbitrary(),
                ComponentGenerator.TestComponentArbitrary,
                (entityCount, component) =>
                {
                    var manager = new EntityManager();

                    try
                    {
                        var entities = new List<Entity>();

                        // Create entities and add components
                        for (int i = 0; i < entityCount; i++)
                        {
                            var entity = manager.CreateEntity();
                            entities.Add(entity);
                            manager.AddComponent(entity, component);
                        }

                        // Remove components from all entities
                        foreach (var entity in entities)
                        {
                            manager.RemoveComponent<TestComponent>(entity);
                        }

                        // Verify no entity has the component
                        foreach (var entity in entities)
                        {
                            if (manager.HasComponent<TestComponent>(entity))
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

        #region Property 9: SparseSet Count Invariant

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 9: SparseSet Count Invariant**
        /// For any sequence of Add and Remove operations on a SparseSet,
        /// the Count property SHALL equal the number of currently stored items.
        /// **Validates: Requirements 3.5**
        /// </summary>
        [Test]
        public void SparseSetCountInvariant_CountMatchesStoredItems()
        {
            var config = PropertyTestConfig.CreateConfig();

            // Generate operation count and entity indices
            var property = Prop.ForAll(
                Gen.Choose(1, 50).ToArbitrary(),
                Gen.Choose(1, 50).ToArbitrary(),
                (addCount, removeCount) =>
                {
                    var set = new SparseSet<TestComponent>(100, 100, Allocator.Temp);
                    var trackedEntities = new HashSet<int>();

                    try
                    {
                        // Add entities
                        for (int i = 0; i < addCount; i++)
                        {
                            int entityIndex = i;
                            set.Add(entityIndex, new TestComponent(entityIndex, 0, true));
                            trackedEntities.Add(entityIndex);

                            // Invariant check after each add
                            if (set.Count != trackedEntities.Count)
                                return false;
                        }

                        // Remove some entities
                        int actualRemoves = System.Math.Min(removeCount, addCount);
                        for (int i = 0; i < actualRemoves; i++)
                        {
                            if (set.Remove(i))
                            {
                                trackedEntities.Remove(i);
                            }

                            // Invariant check after each remove
                            if (set.Count != trackedEntities.Count)
                                return false;
                        }

                        return true;
                    }
                    finally
                    {
                        set.Dispose();
                    }
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 9: SparseSet Count Invariant**
        /// Additional test: Count is zero after Clear.
        /// **Validates: Requirements 3.5**
        /// </summary>
        [Test]
        public void SparseSetCountInvariant_CountZeroAfterClear()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                Gen.Choose(1, 100).ToArbitrary(),
                (entityCount) =>
                {
                    var set = new SparseSet<TestComponent>(200, 200, Allocator.Temp);

                    try
                    {
                        // Add entities
                        for (int i = 0; i < entityCount; i++)
                        {
                            set.Add(i, new TestComponent(i, 0, true));
                        }

                        // Clear
                        set.Clear();

                        // Count should be zero
                        return set.Count == 0;
                    }
                    finally
                    {
                        set.Dispose();
                    }
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 9: SparseSet Count Invariant**
        /// Additional test: Adding same entity twice doesn't increase count.
        /// **Validates: Requirements 3.5**
        /// </summary>
        [Test]
        public void SparseSetCountInvariant_DuplicateAddDoesNotIncreaseCount()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                Gen.Choose(1, 50).ToArbitrary(),
                Gen.Choose(1, 10).ToArbitrary(),
                (entityCount, duplicateCount) =>
                {
                    var set = new SparseSet<TestComponent>(200, 200, Allocator.Temp);

                    try
                    {
                        // Add unique entities
                        for (int i = 0; i < entityCount; i++)
                        {
                            set.Add(i, new TestComponent(i, 0, true));
                        }

                        int countAfterInitialAdd = set.Count;

                        // Add duplicates (same entity indices)
                        for (int d = 0; d < duplicateCount; d++)
                        {
                            for (int i = 0; i < entityCount; i++)
                            {
                                set.Add(i, new TestComponent(i * 2, 0, false));
                            }
                        }

                        // Count should remain the same
                        return set.Count == countAfterInitialAdd && set.Count == entityCount;
                    }
                    finally
                    {
                        set.Dispose();
                    }
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 9: SparseSet Count Invariant**
        /// Additional test: Remove on non-existent entity doesn't change count.
        /// **Validates: Requirements 3.5**
        /// </summary>
        [Test]
        public void SparseSetCountInvariant_RemoveNonExistentDoesNotChangeCount()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                Gen.Choose(1, 50).ToArbitrary(),
                Gen.Choose(100, 199).ToArbitrary(),
                (entityCount, nonExistentIndex) =>
                {
                    var set = new SparseSet<TestComponent>(200, 200, Allocator.Temp);

                    try
                    {
                        // Add entities with indices 0 to entityCount-1
                        for (int i = 0; i < entityCount; i++)
                        {
                            set.Add(i, new TestComponent(i, 0, true));
                        }

                        int countBefore = set.Count;

                        // Try to remove non-existent entity (index >= 100)
                        set.Remove(nonExistentIndex);

                        // Count should remain the same
                        return set.Count == countBefore;
                    }
                    finally
                    {
                        set.Dispose();
                    }
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 9: SparseSet Count Invariant**
        /// Additional test: Contains returns true for exactly Count items.
        /// **Validates: Requirements 3.5**
        /// </summary>
        [Test]
        public void SparseSetCountInvariant_ContainsMatchesCount()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                Gen.ListOf(Gen.Choose(0, 99)).ToArbitrary(),
                (indicesToAdd) =>
                {
                    var set = new SparseSet<TestComponent>(100, 100, Allocator.Temp);
                    var addedIndices = new HashSet<int>();

                    try
                    {
                        // Add entities (duplicates will be handled by SparseSet)
                        foreach (var index in indicesToAdd)
                        {
                            set.Add(index, new TestComponent(index, 0, true));
                            addedIndices.Add(index);
                        }

                        // Count should match unique indices added
                        if (set.Count != addedIndices.Count)
                            return false;

                        // Contains should return true for exactly those indices
                        int containsCount = 0;
                        for (int i = 0; i < 100; i++)
                        {
                            if (set.Contains(i))
                                containsCount++;
                        }

                        return containsCount == set.Count;
                    }
                    finally
                    {
                        set.Dispose();
                    }
                });

            property.Check(config);
        }

        #endregion
    }
}
