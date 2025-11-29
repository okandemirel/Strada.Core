using System;
using System.Runtime.CompilerServices;
using Strada.Core.ECS.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Strada.Core.ECS.Jobs
{
    public enum EntityOperation : byte
    {
        CreateEntity,
        DestroyEntity,
        AddComponent,
        RemoveComponent,
        SetComponent
    }

    [BurstCompile]
    public unsafe struct EntityCommandBuffer : IDisposable
    {
        private NativeList<byte> _commandStream;
        private NativeList<Entity> _createdEntities;
        private int _createEntityCount;
        private Allocator _allocator;
        private bool _isCreated;

        public int CommandCount { get; private set; }
        public int CreatedEntityCount => _createEntityCount;
        public bool IsCreated => _isCreated;

        public EntityCommandBuffer(Allocator allocator, int initialCapacity = 1024)
        {
            _allocator = allocator;
            _commandStream = new NativeList<byte>(initialCapacity, allocator);
            _createdEntities = new NativeList<Entity>(64, allocator);
            _createEntityCount = 0;
            _isCreated = true;
            CommandCount = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CreateEntity()
        {
            WriteCommand(EntityOperation.CreateEntity);
            return _createEntityCount++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DestroyEntity(Entity entity)
        {
            WriteCommand(EntityOperation.DestroyEntity);
            WriteEntity(entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddComponent<T>(Entity entity, T component) where T : unmanaged, IComponent
        {
            WriteCommand(EntityOperation.AddComponent);
            WriteEntity(entity);
            WriteTypeHash<T>();
            WriteComponent(component);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddComponent<T>(int deferredEntityIndex, T component) where T : unmanaged, IComponent
        {
            WriteCommand(EntityOperation.AddComponent);
            WriteDeferredEntity(deferredEntityIndex);
            WriteTypeHash<T>();
            WriteComponent(component);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveComponent<T>(Entity entity) where T : unmanaged, IComponent
        {
            WriteCommand(EntityOperation.RemoveComponent);
            WriteEntity(entity);
            WriteTypeHash<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetComponent<T>(Entity entity, T component) where T : unmanaged, IComponent
        {
            WriteCommand(EntityOperation.SetComponent);
            WriteEntity(entity);
            WriteTypeHash<T>();
            WriteComponent(component);
        }

        public void Playback(EntityManager entityManager)
        {
            if (_commandStream.Length == 0) return;

            _createdEntities.Clear();
            for (int i = 0; i < _createEntityCount; i++)
                _createdEntities.Add(entityManager.CreateEntity());

            var reader = new CommandReader(_commandStream.AsArray());
            while (reader.HasRemaining)
            {
                var cmd = reader.ReadCommand();
                switch (cmd)
                {
                    case EntityOperation.CreateEntity:
                        break;
                    case EntityOperation.DestroyEntity:
                        PlaybackDestroyEntity(ref reader, entityManager);
                        break;
                    case EntityOperation.AddComponent:
                        PlaybackAddComponent(ref reader, entityManager);
                        break;
                    case EntityOperation.RemoveComponent:
                        PlaybackRemoveComponent(ref reader, entityManager);
                        break;
                    case EntityOperation.SetComponent:
                        PlaybackSetComponent(ref reader, entityManager);
                        break;
                }
            }
        }

        public void Clear()
        {
            _commandStream.Clear();
            _createdEntities.Clear();
            _createEntityCount = 0;
            CommandCount = 0;
        }

        public void Dispose()
        {
            if (!_isCreated) return;
            if (_commandStream.IsCreated) _commandStream.Dispose();
            if (_createdEntities.IsCreated) _createdEntities.Dispose();
            _isCreated = false;
        }

        private void WriteCommand(EntityOperation cmd)
        {
            _commandStream.Add((byte)cmd);
            CommandCount++;
        }

        private void WriteEntity(Entity entity)
        {
            WriteInt(entity.Index);
            WriteInt(entity.Version);
            WriteByte(0);
        }

        private void WriteDeferredEntity(int deferredIndex)
        {
            WriteInt(deferredIndex);
            WriteInt(0);
            WriteByte(1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteTypeHash<T>() where T : unmanaged
        {
            ulong hash = TypeHash<T>.Value;
            var bytes = (byte*)&hash;
            for (int i = 0; i < 8; i++)
                _commandStream.Add(bytes[i]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteComponent<T>(T component) where T : unmanaged
        {
            int size = sizeof(T);
            WriteInt(size);
            var ptr = (byte*)&component;
            for (int i = 0; i < size; i++)
                _commandStream.Add(ptr[i]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteInt(int value)
        {
            var bytes = (byte*)&value;
            _commandStream.Add(bytes[0]);
            _commandStream.Add(bytes[1]);
            _commandStream.Add(bytes[2]);
            _commandStream.Add(bytes[3]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteByte(byte value) => _commandStream.Add(value);

        private void PlaybackDestroyEntity(ref CommandReader reader, EntityManager entityManager)
        {
            var entity = ReadEntity(ref reader);
            entityManager.DestroyEntity(entity);
        }

        private void PlaybackAddComponent(ref CommandReader reader, EntityManager entityManager)
        {
            var entity = ReadEntity(ref reader);
            ulong typeHash = reader.ReadULong();
            int size = reader.ReadInt();
            var data = reader.ReadBytes(size);

            ComponentPlayback.AddComponent(entityManager, entity, typeHash, data, size);
        }

        private void PlaybackRemoveComponent(ref CommandReader reader, EntityManager entityManager)
        {
            var entity = ReadEntity(ref reader);
            ulong typeHash = reader.ReadULong();

            ComponentPlayback.RemoveComponent(entityManager, entity, typeHash);
        }

        private void PlaybackSetComponent(ref CommandReader reader, EntityManager entityManager)
        {
            var entity = ReadEntity(ref reader);
            ulong typeHash = reader.ReadULong();
            int size = reader.ReadInt();
            var data = reader.ReadBytes(size);

            ComponentPlayback.SetComponent(entityManager, entity, typeHash, data, size);
        }

        private Entity ReadEntity(ref CommandReader reader)
        {
            int index = reader.ReadInt();
            int version = reader.ReadInt();
            byte isDeferred = reader.ReadByte();

            if (isDeferred == 1)
                return _createdEntities[index];

            return new Entity(index, version);
        }

        private struct CommandReader
        {
            private NativeArray<byte> _data;
            private int _position;

            public bool HasRemaining => _position < _data.Length;

            public CommandReader(NativeArray<byte> data)
            {
                _data = data;
                _position = 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public EntityOperation ReadCommand() => (EntityOperation)_data[_position++];

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public byte ReadByte() => _data[_position++];

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe int ReadInt()
            {
                int value = 0;
                var ptr = (byte*)&value;
                ptr[0] = _data[_position++];
                ptr[1] = _data[_position++];
                ptr[2] = _data[_position++];
                ptr[3] = _data[_position++];
                return value;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe ulong ReadULong()
            {
                ulong value = 0;
                var ptr = (byte*)&value;
                for (int i = 0; i < 8; i++)
                    ptr[i] = _data[_position++];
                return value;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe byte* ReadBytes(int count)
            {
                byte* result = (byte*)_data.GetUnsafeReadOnlyPtr() + _position;
                _position += count;
                return result;
            }
        }
    }

    internal static class TypeHash<T> where T : unmanaged
    {
        public static readonly ulong Value = ComputeHash();

        private static ulong ComputeHash()
        {
            string name = typeof(T).FullName ?? typeof(T).Name;
            ulong hash = 14695981039346656037UL;
            foreach (char c in name)
            {
                hash ^= c;
                hash *= 1099511628211UL;
            }
            return hash;
        }
    }

    public static class ComponentPlayback
    {
        private static readonly System.Collections.Generic.Dictionary<ulong, IComponentPlaybackHandler> _handlers = new(64);

        public static void RegisterHandler<T>(IComponentPlaybackHandler handler) where T : unmanaged, IComponent
        {
            _handlers[TypeHash<T>.Value] = handler;
        }

        public static unsafe void AddComponent(EntityManager em, Entity entity, ulong typeHash, byte* data, int size)
        {
            if (_handlers.TryGetValue(typeHash, out var handler))
                handler.AddComponent(em, entity, data, size);
        }

        public static void RemoveComponent(EntityManager em, Entity entity, ulong typeHash)
        {
            if (_handlers.TryGetValue(typeHash, out var handler))
                handler.RemoveComponent(em, entity);
        }

        public static unsafe void SetComponent(EntityManager em, Entity entity, ulong typeHash, byte* data, int size)
        {
            if (_handlers.TryGetValue(typeHash, out var handler))
                handler.SetComponent(em, entity, data, size);
        }

        public static void EnsureHandler<T>() where T : unmanaged, IComponent
        {
            ulong hash = TypeHash<T>.Value;
            if (!_handlers.ContainsKey(hash))
                _handlers[hash] = new ComponentPlaybackHandler<T>();
        }
    }

    public interface IComponentPlaybackHandler
    {
        unsafe void AddComponent(EntityManager em, Entity entity, byte* data, int size);
        void RemoveComponent(EntityManager em, Entity entity);
        unsafe void SetComponent(EntityManager em, Entity entity, byte* data, int size);
    }

    internal class ComponentPlaybackHandler<T> : IComponentPlaybackHandler where T : unmanaged, IComponent
    {
        public unsafe void AddComponent(EntityManager em, Entity entity, byte* data, int size)
        {
            T component = *(T*)data;
            em.AddComponent(entity, component);
        }

        public void RemoveComponent(EntityManager em, Entity entity)
        {
            em.RemoveComponent<T>(entity);
        }

        public unsafe void SetComponent(EntityManager em, Entity entity, byte* data, int size)
        {
            T component = *(T*)data;
            em.SetComponent(entity, component);
        }
    }
}
