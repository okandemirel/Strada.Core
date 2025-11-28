using Strada.Core.Patterns.Interfaces;

namespace Strada.Core.Patterns
{
    /// <summary>
    /// Services provide global infrastructure shared across features.
    /// Use for: audio, persistence, networking, analytics, input.
    /// Scope: Application lifetime (singleton).
    /// </summary>
    public abstract class Service : Base, IService
    {
    }

    /// <summary>
    /// Service that receives Tick updates every frame.
    /// </summary>
    public abstract class TickableService : Service, ITickable
    {
        public virtual void Tick(float deltaTime) { }
    }

    /// <summary>
    /// Service that receives FixedTick updates at fixed intervals.
    /// </summary>
    public abstract class FixedTickableService : Service, IFixedTickable
    {
        public virtual void FixedTick(float fixedDeltaTime) { }
    }

    /// <summary>
    /// Service with explicit initialization ordering.
    /// </summary>
    public abstract class OrderedService : Service, IOrderedService
    {
        public virtual int InitializationOrder => 0;
    }
}
