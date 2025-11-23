using System;
using UnityEngine;

namespace Strada.Core.ECS.Baking
{
    /// <summary>
    /// Interface for bakers that convert authoring components to ECS components.
    /// </summary>
    /// <remarks>
    /// Bakers are invoked during the Unity baking process to convert GameObjects
    /// and ScriptableObjects into ECS entities and components.
    ///
    /// Baking occurs at edit time and build time, NOT at runtime.
    /// This allows for optimal runtime performance by pre-processing data.
    ///
    /// Key Concepts:
    /// - Bakers run in the Unity Editor during baking passes
    /// - They convert authoring data (ScriptableObjects, MonoBehaviours) to ECS components
    /// - Baking is incremental - only changed assets are rebaked
    /// - Bakers should be deterministic and side-effect free
    ///
    /// Best Practices:
    /// - Keep baking logic simple and focused
    /// - Validate data during baking, not at runtime
    /// - Use DependsOn to track dependencies
    /// - Log errors for invalid configurations
    /// - Avoid expensive operations in bakers
    /// </remarks>
    public interface IBaker
    {
        /// <summary>
        /// Gets the authoring component type this baker operates on.
        /// </summary>
        Type AuthoringType { get; }

        /// <summary>
        /// Gets whether this baker is enabled.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Validates the authoring data before baking.
        /// </summary>
        /// <param name="authoring">The authoring component</param>
        /// <param name="errorMessage">Error message if validation fails</param>
        /// <returns>True if validation succeeds</returns>
        bool Validate(object authoring, out string errorMessage);
    }

    /// <summary>
    /// Generic interface for typed bakers.
    /// </summary>
    /// <typeparam name="TAuthoring">The authoring component type</typeparam>
    /// <remarks>
    /// Implement this interface to create a baker for a specific authoring type.
    ///
    /// Example:
    /// <code>
    /// public class BallPhysicsBaker : IBaker&lt;CD_BallPhysics&gt;
    /// {
    ///     public void Bake(CD_BallPhysics authoring, IBakerContext context)
    ///     {
    ///         if (!authoring.Config.IsValid())
    ///         {
    ///             context.LogError("Invalid ball physics configuration");
    ///             return;
    ///         }
    ///
    ///         var entity = context.GetEntity(TransformUsageFlags.Dynamic);
    ///         context.AddComponent(entity, new PhysicsComponent
    ///         {
    ///             Mass = authoring.Config.Mass,
    ///             Bounciness = authoring.Config.Bounciness
    ///         });
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public interface IBaker<TAuthoring> : IBaker where TAuthoring : class
    {
        /// <summary>
        /// Bakes the authoring component into ECS components.
        /// </summary>
        /// <param name="authoring">The authoring component to bake</param>
        /// <param name="context">The baking context</param>
        void Bake(TAuthoring authoring, IBakerContext context);
    }

    /// <summary>
    /// Context provided to bakers during the baking process.
    /// </summary>
    /// <remarks>
    /// The baker context provides access to:
    /// - Entity creation and retrieval
    /// - Component addition and removal
    /// - Dependency tracking
    /// - Logging and error reporting
    /// </remarks>
    public interface IBakerContext
    {
        /// <summary>
        /// Gets the primary entity for the current GameObject.
        /// </summary>
        /// <param name="flags">Transform usage flags</param>
        /// <returns>The entity handle</returns>
        Entity GetEntity(TransformUsageFlags flags);

        /// <summary>
        /// Creates an additional entity during baking.
        /// </summary>
        /// <param name="flags">Transform usage flags</param>
        /// <returns>The created entity handle</returns>
        Entity CreateAdditionalEntity(TransformUsageFlags flags);

        /// <summary>
        /// Adds a component to an entity.
        /// </summary>
        /// <typeparam name="T">The component type</typeparam>
        /// <param name="entity">The target entity</param>
        /// <param name="component">The component data</param>
        void AddComponent<T>(Entity entity, T component) where T : struct, IStradaComponent;

        /// <summary>
        /// Adds a component to an entity with default values.
        /// </summary>
        /// <typeparam name="T">The component type</typeparam>
        /// <param name="entity">The target entity</param>
        void AddComponent<T>(Entity entity) where T : struct, IStradaComponent;

        /// <summary>
        /// Adds a buffer component to an entity.
        /// </summary>
        /// <typeparam name="T">The buffer element type</typeparam>
        /// <param name="entity">The target entity</param>
        /// <returns>The buffer</returns>
        DynamicBuffer<T> AddBuffer<T>(Entity entity) where T : struct, IBufferComponent;

        /// <summary>
        /// Declares a dependency on a Unity object.
        /// The baker will re-run if this object changes.
        /// </summary>
        /// <param name="dependency">The dependent object</param>
        void DependsOn(UnityEngine.Object dependency);

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The message to log</param>
        void LogInfo(string message);

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The warning message</param>
        void LogWarning(string message);

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The error message</param>
        void LogError(string message);

        /// <summary>
        /// Gets whether this baking pass is for runtime or editor.
        /// </summary>
        bool IsRuntime { get; }
    }

    /// <summary>
    /// Flags indicating how transform data should be used.
    /// </summary>
    [Flags]
    public enum TransformUsageFlags
    {
        /// <summary>
        /// No transform usage (static entity).
        /// </summary>
        None = 0,

        /// <summary>
        /// Entity can be moved at runtime.
        /// </summary>
        Dynamic = 1 << 0,

        /// <summary>
        /// Entity position is used for rendering.
        /// </summary>
        Renderable = 1 << 1,

        /// <summary>
        /// Entity can have world-space UI.
        /// </summary>
        WorldSpace = 1 << 2,

        /// <summary>
        /// Entity needs full transform hierarchy.
        /// </summary>
        ManualOverride = 1 << 3
    }

    /// <summary>
    /// Dynamic buffer for variable-length component arrays.
    /// </summary>
    /// <typeparam name="T">The buffer element type</typeparam>
    /// <remarks>
    /// Dynamic buffers store variable-length arrays of data on entities.
    /// Small buffers (< 8 elements) are stored inline with the entity.
    /// Larger buffers are allocated externally.
    ///
    /// Example:
    /// <code>
    /// // In baker
    /// var buffer = context.AddBuffer&lt;PathPoint&gt;(entity);
    /// buffer.Add(new PathPoint { Position = new float3(0, 0, 0) });
    /// buffer.Add(new PathPoint { Position = new float3(1, 0, 0) });
    ///
    /// // In system
    /// foreach (var buffer in SystemAPI.Query&lt;DynamicBuffer&lt;PathPoint&gt;&gt;())
    /// {
    ///     for (int i = 0; i < buffer.Length; i++)
    ///     {
    ///         var point = buffer[i];
    ///         // Process point
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public struct DynamicBuffer<T> where T : struct, IBufferComponent
    {
        private T[] _buffer;
        private int _length;

        /// <summary>
        /// Gets the number of elements in the buffer.
        /// </summary>
        public int Length => _length;

        /// <summary>
        /// Gets or sets an element at the specified index.
        /// </summary>
        /// <param name="index">The element index</param>
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

        /// <summary>
        /// Adds an element to the buffer.
        /// </summary>
        /// <param name="element">The element to add</param>
        public void Add(T element)
        {
            EnsureCapacity(_length + 1);
            _buffer[_length++] = element;
        }

        /// <summary>
        /// Clears all elements from the buffer.
        /// </summary>
        public void Clear()
        {
            _length = 0;
        }

        /// <summary>
        /// Resizes the buffer to the specified length.
        /// </summary>
        /// <param name="length">The new length</param>
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

    /// <summary>
    /// Attribute to mark a baker for auto-discovery.
    /// </summary>
    /// <remarks>
    /// Bakers marked with this attribute are automatically discovered
    /// and registered during the baking process.
    ///
    /// Example:
    /// <code>
    /// [StradaBaker]
    /// public class MyBaker : StradaBaker&lt;MyAuthoring&gt;
    /// {
    ///     public override void Bake(MyAuthoring authoring, IBakerContext context)
    ///     {
    ///         // Baking logic
    ///     }
    /// }
    /// </code>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class StradaBakerAttribute : Attribute
    {
        /// <summary>
        /// Whether this baker is enabled by default.
        /// </summary>
        public bool EnabledByDefault { get; set; } = true;

        /// <summary>
        /// The priority of this baker (lower values execute first).
        /// </summary>
        public int Priority { get; set; } = 0;
    }
}
