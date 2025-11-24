using System;
using UnityEngine;

namespace Strada.Core.ECS.Baking
{
    public interface IBaker
    {
        Type AuthoringType { get; }
        bool IsEnabled { get; }
        bool Validate(object authoring, out string errorMessage);
    }

    public interface IBaker<TAuthoring> : IBaker where TAuthoring : class
    {
        void Bake(TAuthoring authoring, IBakerContext context);
    }

    public interface IBakerContext
    {
        Entity GetEntity(TransformUsageFlags flags);
        Entity CreateAdditionalEntity(TransformUsageFlags flags);
        void AddComponent<T>(Entity entity, T component) where T : unmanaged, IStradaComponent;
        void AddComponent<T>(Entity entity) where T : unmanaged, IStradaComponent;
        DynamicBuffer<T> AddBuffer<T>(Entity entity) where T : struct, IBufferComponent;
        void DependsOn(UnityEngine.Object dependency);
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message);
        bool IsRuntime { get; }
    }

    [Flags]
    public enum TransformUsageFlags
    {
        None = 0,
        Dynamic = 1 << 0,
        Renderable = 1 << 1,
        WorldSpace = 1 << 2,
        ManualOverride = 1 << 3
    }

    public struct DynamicBuffer<T> where T : struct, IBufferComponent
    {
        private T[] _buffer;
        private int _length;

        public int Length => _length;

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= _length)
                    throw new IndexOutOfRangeException($"Index {index} is out of range [0, {_length})");
                return _buffer[index];
            }
            set
            {
                if (index < 0 || index >= _length)
                    throw new IndexOutOfRangeException($"Index {index} is out of range [0, {_length})");
                _buffer[index] = value;
            }
        }

        public void Add(T element)
        {
            EnsureCapacity(_length + 1);
            _buffer[_length++] = element;
        }

        public void Clear()
        {
            _length = 0;
        }

        public void Resize(int length)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            EnsureCapacity(length);
            _length = length;
        }

        private void EnsureCapacity(int capacity)
        {
            if (_buffer == null)
            {
                _buffer = new T[Math.Max(8, capacity)];
            }
            else if (_buffer.Length < capacity)
            {
                var newCapacity = Math.Max(_buffer.Length * 2, capacity);
                var newBuffer = new T[newCapacity];
                Array.Copy(_buffer, newBuffer, _length);
                _buffer = newBuffer;
            }
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class StradaBakerAttribute : Attribute
    {
        public bool EnabledByDefault { get; set; } = true;
        public int Priority { get; set; } = 0;
    }
}
