using System.Collections.Generic;
using FsCheck;
using NUnit.Framework;
using Strada.Core.ECS;
using Strada.Core.ECS.Core;
using Strada.Core.Tests.Tests.Runtime.Generators;

namespace Strada.Core.Tests.Tests.Runtime.ECS.Core
{
    /// <summary>
    /// Property-based tests for ECS entity lifecycle.
    /// Tests verify correctness properties that must hold across all valid inputs.
    /// </summary>
    [TestFixture]
    public class EntityPropertyTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            StradaArbitraries.RegisterAll();
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 5: Entity Uniqueness**
        /// For any sequence of N entity creations (without destruction),
        /// all N entities SHALL have unique (Index, Version) pairs.
        /// **Validates: Requirements 3.1**
        /// </summary>
        [Test]
        public void EntityUniqueness_AllCreatedEntitiesHaveUniqueIndexVersionPairs()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                Gen.Choose(1, 100).ToArbitrary(),
                (entityCount) =>
                {
                    var manager = new EntityManager();
                    var entities = new List<Entity>();

                    try
                    {
                        for (int i = 0; i < entityCount; i++)
                        {
                            entities.Add(manager.CreateEntity());
                        }

                        var seen = new HashSet<(int Index, int Version)>();
                        foreach (var entity in entities)
                        {
                            var pair = (entity.Index, entity.Version);
                            if (!seen.Add(pair))
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
        /// **Feature: strada-codebase-audit, Property 5: Entity Uniqueness**
        /// Additional test: Created entities have valid (non-null) indices.
        /// **Validates: Requirements 3.1**
        /// </summary>
        [Test]
        public void EntityUniqueness_CreatedEntitiesAreNotNull()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                Gen.Choose(1, 100).ToArbitrary(),
                (entityCount) =>
                {
                    var manager = new EntityManager();

                    try
                    {
                        for (int i = 0; i < entityCount; i++)
                        {
                            var entity = manager.CreateEntity();
                            if (entity.IsNull)
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
        /// **Feature: strada-codebase-audit, Property 5: Entity Uniqueness**
        /// Additional test: All created entities exist in the manager.
        /// **Validates: Requirements 3.1**
        /// </summary>
        [Test]
        public void EntityUniqueness_AllCreatedEntitiesExist()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                Gen.Choose(1, 100).ToArbitrary(),
                (entityCount) =>
                {
                    var manager = new EntityManager();
                    var entities = new List<Entity>();

                    try
                    {
                        for (int i = 0; i < entityCount; i++)
                        {
                            entities.Add(manager.CreateEntity());
                        }

                        foreach (var entity in entities)
                        {
                            if (!manager.Exists(entity))
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
        /// **Feature: strada-codebase-audit, Property 6: Entity Version Increment**
        /// For any entity that is destroyed and its index reused,
        /// the new entity SHALL have the same Index but strictly greater Version.
        /// **Validates: Requirements 3.2**
        /// </summary>
        [Test]
        public void EntityVersionIncrement_ReusedIndexHasGreaterVersion()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                Gen.Choose(1, 50).ToArbitrary(),
                (cycleCount) =>
                {
                    var manager = new EntityManager();

                    try
                    {
                        var firstEntity = manager.CreateEntity();
                        int originalIndex = firstEntity.Index;
                        int previousVersion = firstEntity.Version;

                        for (int i = 0; i < cycleCount; i++)
                        {
                            manager.DestroyEntity(firstEntity);

                            var newEntity = manager.CreateEntity();

                            if (newEntity.Index != originalIndex)
                            {
                                firstEntity = newEntity;
                                originalIndex = newEntity.Index;
                                previousVersion = newEntity.Version;
                                continue;
                            }

                            if (newEntity.Version <= previousVersion)
                                return false;

                            previousVersion = newEntity.Version;
                            firstEntity = newEntity;
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
        /// **Feature: strada-codebase-audit, Property 6: Entity Version Increment**
        /// Additional test: Destroyed entity no longer exists.
        /// **Validates: Requirements 3.2**
        /// </summary>
        [Test]
        public void EntityVersionIncrement_DestroyedEntityNoLongerExists()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                Gen.Choose(1, 100).ToArbitrary(),
                (entityCount) =>
                {
                    var manager = new EntityManager();
                    var entities = new List<Entity>();

                    try
                    {
                        for (int i = 0; i < entityCount; i++)
                        {
                            entities.Add(manager.CreateEntity());
                        }

                        foreach (var entity in entities)
                        {
                            manager.DestroyEntity(entity);
                        }

                        foreach (var entity in entities)
                        {
                            if (manager.Exists(entity))
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
        /// **Feature: strada-codebase-audit, Property 6: Entity Version Increment**
        /// Additional test: Old entity reference is invalid after index reuse.
        /// **Validates: Requirements 3.2**
        /// </summary>
        [Test]
        public void EntityVersionIncrement_OldEntityReferenceInvalidAfterReuse()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                Gen.Choose(1, 20).ToArbitrary(),
                (cycleCount) =>
                {
                    var manager = new EntityManager();
                    var oldEntities = new List<Entity>();

                    try
                    {
                        for (int i = 0; i < cycleCount; i++)
                        {
                            var entity = manager.CreateEntity();
                            oldEntities.Add(entity);
                            manager.DestroyEntity(entity);
                        }

                        for (int i = 0; i < cycleCount; i++)
                        {
                            manager.CreateEntity();
                        }

                        foreach (var oldEntity in oldEntities)
                        {
                            if (manager.Exists(oldEntity))
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
        /// **Feature: strada-codebase-audit, Property 6: Entity Version Increment**
        /// Additional test: Entity count is correct after create/destroy cycles.
        /// **Validates: Requirements 3.2**
        /// </summary>
        [Test]
        public void EntityVersionIncrement_EntityCountCorrectAfterCycles()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                Gen.Choose(1, 50).ToArbitrary(),
                Gen.Choose(0, 30).ToArbitrary(),
                (createCount, destroyCount) =>
                {
                    var manager = new EntityManager();
                    var entities = new List<Entity>();
                    int actualDestroyCount = System.Math.Min(destroyCount, createCount);

                    try
                    {
                        for (int i = 0; i < createCount; i++)
                        {
                            entities.Add(manager.CreateEntity());
                        }

                        for (int i = 0; i < actualDestroyCount; i++)
                        {
                            manager.DestroyEntity(entities[i]);
                        }

                        int expectedCount = createCount - actualDestroyCount;
                        return manager.EntityCount == expectedCount;
                    }
                    finally
                    {
                        manager.Dispose();
                    }
                });

            property.Check(config);
        }
    }
}
