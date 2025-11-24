using Unity.Burst;
using Unity.Collections;

namespace Strada.Core.ECS
{
    public interface IBurstSystem : IStradaSystem
    {
    }

    public struct BurstSystemState
    {
        public float DeltaTime;
        public double Time;
        public bool Enabled;
    }
}
