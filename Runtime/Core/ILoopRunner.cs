namespace Strada.Core.Core
{
    public interface ILoopRunner
    {
        void OnUpdate(float deltaTime);
        void OnLateUpdate(float deltaTime);
        void OnFixedUpdate(float fixedDeltaTime);
    }
}
