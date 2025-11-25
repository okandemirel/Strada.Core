using System;

namespace Strada.Core.ECS.Archetypes
{
    public interface IEntityDescriptor
    {
        Type[] ComponentTypes { get; }
        void InitializeComponents(EntityManager manager, Entity entity);
    }
}
