namespace Strada.Core.Bridge
{
    using Strada.Core.ECS;

    /// <summary>
    /// Event published when an ECS component changes.
    /// ECS Systems publish this; MVCS Controllers subscribe to it.
    /// </summary>
    public readonly struct ComponentChanged<T> where T : unmanaged, IComponent
    {
        public readonly Entity Entity;
        public readonly T OldValue;
        public readonly T NewValue;

        public ComponentChanged(Entity entity, T oldValue, T newValue)
        {
            Entity = entity;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }

    /// <summary>
    /// Event published when an ECS component is added to an entity.
    /// </summary>
    public readonly struct ComponentAdded<T> where T : unmanaged, IComponent
    {
        public readonly Entity Entity;
        public readonly T Value;

        public ComponentAdded(Entity entity, T value)
        {
            Entity = entity;
            Value = value;
        }
    }

    /// <summary>
    /// Event published when an ECS component is removed from an entity.
    /// </summary>
    public readonly struct ComponentRemoved<T> where T : unmanaged, IComponent
    {
        public readonly Entity Entity;
        public readonly T Value;

        public ComponentRemoved(Entity entity, T value)
        {
            Entity = entity;
            Value = value;
        }
    }

    /// <summary>
    /// Event published when an entity is created.
    /// </summary>
    public readonly struct EntityCreated
    {
        public readonly Entity Entity;

        public EntityCreated(Entity entity)
        {
            Entity = entity;
        }
    }

    /// <summary>
    /// Event published when an entity is destroyed.
    /// </summary>
    public readonly struct EntityDestroyed
    {
        public readonly Entity Entity;

        public EntityDestroyed(Entity entity)
        {
            Entity = entity;
        }
    }
}
