namespace Strada.Core.ECS.Reactive
{
    public interface IReactOnAdd<T> where T : unmanaged, IComponent
    {
        void OnAdd(int entity, ref T component);
    }

    public interface IReactOnRemove<T> where T : unmanaged, IComponent
    {
        void OnRemove(int entity, ref T component);
    }

    public interface IReactOnChange<T> where T : unmanaged, IComponent
    {
        void OnChange(int entity, ref T oldValue, ref T newValue);
    }
}
