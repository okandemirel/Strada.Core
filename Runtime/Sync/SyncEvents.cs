namespace Strada.Core.Sync
{
    using Strada.Core.ECS;

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

    public readonly struct EntityCreated
    {
        public readonly Entity Entity;

        public EntityCreated(Entity entity)
        {
            Entity = entity;
        }
    }

    public readonly struct EntityDestroyed
    {
        public readonly Entity Entity;

        public EntityDestroyed(Entity entity)
        {
            Entity = entity;
        }
    }
}
