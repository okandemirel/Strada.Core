namespace Strada.Core.StateMachine
{
    public abstract class StateBase : IState
    {
        public virtual void OnEnter() { }
        public virtual void OnUpdate(float deltaTime) { }
        public virtual void OnExit() { }
    }

    public abstract class StateBase<TContext> : IState<TContext>
    {
        protected TContext Context { get; private set; }

        public void SetContext(TContext context) => Context = context;

        public virtual void OnEnter() { }
        public virtual void OnUpdate(float deltaTime) { }
        public virtual void OnExit() { }
    }
}
