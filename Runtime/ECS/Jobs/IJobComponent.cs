namespace Strada.Core.ECS.Jobs
{
    public interface IJobComponent<T1> where T1 : unmanaged, IComponent
    {
        void Execute(int entity, ref T1 c1);
    }

    public interface IJobComponent<T1, T2>
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
    {
        void Execute(int entity, ref T1 c1, ref T2 c2);
    }

    public interface IJobComponent<T1, T2, T3>
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
    {
        void Execute(int entity, ref T1 c1, ref T2 c2, ref T3 c3);
    }

    public interface IJobComponent<T1, T2, T3, T4>
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
    {
        void Execute(int entity, ref T1 c1, ref T2 c2, ref T3 c3, ref T4 c4);
    }
}
