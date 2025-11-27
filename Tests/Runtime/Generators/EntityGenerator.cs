using System;
using FsCheck;
using Strada.Core.ECS;

namespace Strada.Core.Tests.Runtime.Generators
{
    /// <summary>
    /// FsCheck generators for Entity type.
    /// Generates valid entities with index 1-10000 and version 1-100.
    /// </summary>
    public static class EntityGenerator
    {
        /// <summary>
        /// Generator for valid Entity instances.
        /// Index range: 1-10000, Version range: 1-100
        /// </summary>
        public static Gen<Entity> ValidEntity =>
            from index in Gen.Choose(1, 10000)
            from version in Gen.Choose(1, 100)
            select new Entity(index, version);

        /// <summary>
        /// Generator for Entity.Null
        /// </summary>
        public static Gen<Entity> NullEntity =>
            Gen.Constant(Entity.Null);

        /// <summary>
        /// Generator for any Entity (including null)
        /// </summary>
        public static Gen<Entity> AnyEntity =>
            Gen.Frequency(
                Tuple.Create(9, ValidEntity),
                Tuple.Create(1, NullEntity)
            );

        /// <summary>
        /// Generator for a list of unique entities
        /// </summary>
        public static Gen<Entity[]> UniqueEntities(int count) =>
            from indices in Gen.ArrayOf(count, Gen.Choose(1, 100000))
            from versions in Gen.ArrayOf(count, Gen.Choose(1, 100))
            select CreateUniqueEntities(indices, versions, count);

        private static Entity[] CreateUniqueEntities(int[] indices, int[] versions, int count)
        {
            var entities = new Entity[count];
            var usedIndices = new System.Collections.Generic.HashSet<int>();
            
            for (int i = 0; i < count; i++)
            {
                // Ensure unique indices by offsetting if collision
                int index = indices[i];
                while (usedIndices.Contains(index))
                {
                    index++;
                }
                usedIndices.Add(index);
                entities[i] = new Entity(index, versions[i]);
            }
            
            return entities;
        }

        /// <summary>
        /// Arbitrary instance for Entity (for automatic property-based testing)
        /// </summary>
        public static Arbitrary<Entity> EntityArbitrary =>
            Arb.From(ValidEntity, ShrinkEntity);

        /// <summary>
        /// Shrinker for Entity - shrinks towards smaller index and version
        /// </summary>
        private static System.Collections.Generic.IEnumerable<Entity> ShrinkEntity(Entity entity)
        {
            if (entity.Index > 1)
                yield return new Entity(entity.Index / 2, entity.Version);
            
            if (entity.Version > 1)
                yield return new Entity(entity.Index, entity.Version / 2);
            
            if (entity.Index > 1 && entity.Version > 1)
                yield return new Entity(entity.Index / 2, entity.Version / 2);
        }
    }
}
