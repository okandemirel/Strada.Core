namespace Strada.Core.StateMachine
{
    public interface IState
    {
        void OnEnter();
        void OnUpdate(float deltaTime);
        void OnExit();
    }

    public interface IState<TContext> : IState
    {
        void SetContext(TContext context);
    }
}
