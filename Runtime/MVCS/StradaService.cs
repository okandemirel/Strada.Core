using Strada.Core.MVCS.Interfaces;

namespace Strada.Core.MVCS
{
    /// <summary>
    /// Base class for services. Services contain business logic and can be shared across modules.
    /// </summary>
    public abstract class StradaService : StradaBase, IService
    {
    }

    /// <summary>
    /// Service that receives Tick updates every frame.
    /// </summary>
    public abstract class TickableService : StradaService, ITickable
    {
        public virtual void Tick(float deltaTime) { }
    }

    /// <summary>
    /// Service that receives FixedTick updates at fixed intervals.
    /// </summary>
    public abstract class FixedTickableService : StradaService, IFixedTickable
    {
        public virtual void FixedTick(float fixedDeltaTime) { }
    }

    /// <summary>
    /// Service with explicit initialization ordering.
    /// </summary>
    public abstract class OrderedService : StradaService, IOrderedService
    {
        public virtual int InitializationOrder => 0;
    }
}
