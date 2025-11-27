namespace Strada.Core.MVCS.Interfaces
{
    public interface ILateTickable
    {
        void LateTick(float deltaTime);
    }
}
