using Strada.Core.DI.Attributes;
using Strada.Core.MVCS.Interfaces;

namespace Strada.Core.MVCS
{
    /// <summary>
    /// Base class for controllers. Controllers handle input and coordinate between Views and Services.
    /// </summary>
    public abstract class StradaController : StradaBase, IController
    {
        public virtual void Update(float deltaTime) { }
    }

    /// <summary>
    /// Controller with strongly-typed model injection.
    /// </summary>
    public abstract class StradaController<TModel> : StradaController where TModel : class, IModel
    {
        protected TModel Model { get; private set; }

        [Inject]
        public void InjectModel(TModel model)
        {
            Model = model;
        }
    }

    /// <summary>
    /// Controller that receives Tick updates every frame.
    /// </summary>
    public abstract class TickableController : StradaController, ITickable
    {
        public virtual void Tick(float deltaTime) { }
    }

    /// <summary>
    /// Controller that receives FixedTick updates at fixed intervals.
    /// </summary>
    public abstract class FixedTickableController : StradaController, IFixedTickable
    {
        public virtual void FixedTick(float fixedDeltaTime) { }
    }

    /// <summary>
    /// Controller that receives all tick types: Tick, FixedTick, and LateTick.
    /// </summary>
    public abstract class FullTickController : StradaController, ITickable, IFixedTickable, ILateTickable
    {
        public virtual void Tick(float deltaTime) { }
        public virtual void FixedTick(float fixedDeltaTime) { }
        public virtual void LateTick(float deltaTime) { }
    }
}
