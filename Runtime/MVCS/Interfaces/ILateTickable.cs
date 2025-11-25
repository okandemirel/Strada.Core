namespace Strada.Core.MVCS
{
    public interface ILateTickable
    {
        void LateTick(float deltaTime);
    }
}
