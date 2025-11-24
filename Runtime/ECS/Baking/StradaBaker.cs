using System;
using UnityEngine;

namespace Strada.Core.ECS.Baking
{
    /// <summary>
    /// Base class for Strada bakers.
    /// Provides common functionality for converting authoring components to ECS components.
    /// </summary>
    /// <typeparam name="TAuthoring">The authoring component type</typeparam>
    /// <remarks>
    /// Inherit from this class to create custom bakers for your authoring types.
    ///
    /// The baker lifecycle:
    /// 1. Unity detects changed authoring components
    /// 2. Baker.Validate() is called
    /// 3. If valid, Baker.Bake() is called
    /// 4. Components are added to the entity
    ///
    /// Example:
    /// <code>
    /// [StradaBaker]
    /// public class BallPhysicsBaker : StradaBaker&lt;CD_BallPhysics&gt;
    /// {
    ///     protected override bool ValidateAuthoring(CD_BallPhysics authoring, out string errorMessage)
    ///     {
    ///         if (!authoring.Config.IsValid())
    ///         {
    ///             errorMessage = "Invalid physics configuration";
    ///             return false;
    ///         }
    ///
    ///         errorMessage = null;
    ///         return true;
    ///     }
    ///
    ///     public override void Bake(CD_BallPhysics authoring, IBakerContext context)
    ///     {
    ///         var entity = context.GetEntity(TransformUsageFlags.Dynamic);
    ///
    ///         context.AddComponent(entity, new PhysicsComponent
    ///         {
    ///             Mass = authoring.Config.Mass,
    ///             Bounciness = authoring.Config.Bounciness,
    ///             Drag = authoring.Config.Drag
    ///         });
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public abstract class StradaBaker<TAuthoring> : IBaker<TAuthoring>
        where TAuthoring : class
    {
        /// <inheritdoc/>
        public Type AuthoringType => typeof(TAuthoring);

        /// <inheritdoc/>
        public virtual bool IsEnabled => true;

        /// <summary>
        /// Bakes the authoring component into ECS components.
        /// Override this method to implement custom baking logic.
        /// </summary>
        /// <param name="authoring">The authoring component</param>
        /// <param name="context">The baking context</param>
        public abstract void Bake(TAuthoring authoring, IBakerContext context);

        /// <summary>
        /// Validates the authoring component before baking.
        /// Override this to implement custom validation logic.
        /// </summary>
        /// <param name="authoring">The authoring component</param>
        /// <param name="errorMessage">Error message if validation fails</param>
        /// <returns>True if validation succeeds</returns>
        protected virtual bool ValidateAuthoring(TAuthoring authoring, out string errorMessage)
        {
            if (authoring == null)
            {
                errorMessage = $"Authoring component of type {typeof(TAuthoring).Name} is null";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <inheritdoc/>
        public bool Validate(object authoring, out string errorMessage)
        {
            if (authoring == null)
            {
                errorMessage = "Authoring component is null";
                return false;
            }

            if (!(authoring is TAuthoring typedAuthoring))
            {
                errorMessage = $"Expected authoring type {typeof(TAuthoring).Name} but got {authoring.GetType().Name}";
                return false;
            }

            return ValidateAuthoring(typedAuthoring, out errorMessage);
        }

        /// <summary>
        /// Helper method to log info messages during baking.
        /// </summary>
        /// <param name="context">The baking context</param>
        /// <param name="message">The message to log</param>
        protected void LogInfo(IBakerContext context, string message)
        {
            context.LogInfo($"[{GetType().Name}] {message}");
        }

        /// <summary>
        /// Helper method to log warning messages during baking.
        /// </summary>
        /// <param name="context">The baking context</param>
        /// <param name="message">The warning message</param>
        protected void LogWarning(IBakerContext context, string message)
        {
            context.LogWarning($"[{GetType().Name}] {message}");
        }

        /// <summary>
        /// Helper method to log error messages during baking.
        /// </summary>
        /// <param name="context">The baking context</param>
        /// <param name="message">The error message</param>
        protected void LogError(IBakerContext context, string message)
        {
            context.LogError($"[{GetType().Name}] {message}");
        }
    }

    /// <summary>
    /// Base class for bakers that convert ScriptableObjects to ECS components.
    /// </summary>
    /// <typeparam name="TScriptableObject">The ScriptableObject type (CD_ prefixed)</typeparam>
    /// <remarks>
    /// Use this class when baking ScriptableObject configurations into ECS components.
    /// This is the most common pattern in Strada for data-driven design.
    ///
    /// Pattern:
    /// - CD_BallPhysics (ScriptableObject) contains BallPhysicsConfig (Value Object)
    /// - Baker extracts data from config and creates PhysicsComponent (IComponentData)
    ///
    /// Example:
    /// <code>
    /// [StradaBaker]
    /// public class BallConfigBaker : StradaScriptableObjectBaker&lt;CD_BallPhysics&gt;
    /// {
    ///     protected override bool ValidateAuthoring(CD_BallPhysics authoring, out string errorMessage)
    ///     {
    ///         if (authoring.Config == null)
    ///         {
    ///             errorMessage = "Config is null";
    ///             return false;
    ///         }
    ///
    ///         if (!authoring.Config.IsValid())
    ///         {
    ///             errorMessage = "Config validation failed";
    ///             return false;
    ///         }
    ///
    ///         errorMessage = null;
    ///         return true;
    ///     }
    ///
    ///     public override void Bake(CD_BallPhysics authoring, IBakerContext context)
    ///     {
    ///         var entity = context.GetEntity(TransformUsageFlags.Dynamic);
    ///
    ///         // Extract value object data and create ECS component
    ///         var config = authoring.Config;
    ///         context.AddComponent(entity, new BallPhysicsComponent
    ///         {
    ///             Mass = config.Mass,
    ///             Radius = config.Radius,
    ///             Bounciness = config.Bounciness
    ///         });
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public abstract class StradaScriptableObjectBaker<TScriptableObject> : StradaBaker<TScriptableObject>
        where TScriptableObject : ScriptableObject
    {
        /// <summary>
        /// Validates the ScriptableObject before baking.
        /// </summary>
        /// <param name="authoring">The ScriptableObject</param>
        /// <param name="errorMessage">Error message if validation fails</param>
        /// <returns>True if validation succeeds</returns>
        protected override bool ValidateAuthoring(TScriptableObject authoring, out string errorMessage)
        {
            if (authoring == null)
            {
                errorMessage = $"ScriptableObject of type {typeof(TScriptableObject).Name} is null";
                return false;
            }

            // Additional validation can be added in derived classes
            errorMessage = null;
            return true;
        }

        /// <summary>
        /// Gets the configuration data from a ScriptableObject.
        /// Override this if your ScriptableObject uses a non-standard property name.
        /// </summary>
        /// <typeparam name="TConfig">The config type</typeparam>
        /// <param name="scriptableObject">The ScriptableObject</param>
        /// <returns>The configuration data</returns>
        protected virtual TConfig GetConfig<TConfig>(TScriptableObject scriptableObject)
        {
            // Try to get Config property via reflection
            var configProperty = typeof(TScriptableObject).GetProperty("Config");
            if (configProperty == null)
            {
                throw new InvalidOperationException(
                    $"ScriptableObject {typeof(TScriptableObject).Name} does not have a 'Config' property. " +
                    "Override GetConfig<T> to specify how to retrieve configuration data.");
            }

            var config = configProperty.GetValue(scriptableObject);
            if (config == null)
            {
                throw new InvalidOperationException(
                    $"Config property on {typeof(TScriptableObject).Name} is null");
            }

            if (!(config is TConfig typedConfig))
            {
                throw new InvalidOperationException(
                    $"Config property on {typeof(TScriptableObject).Name} is {config.GetType().Name}, " +
                    $"expected {typeof(TConfig).Name}");
            }

            return typedConfig;
        }
    }

    /// <summary>
    /// Simple baking context implementation for testing.
    /// </summary>
    internal class SimpleBakerContext : IBakerContext
    {
        private readonly IEntityManager _entityManager;
        private Entity _primaryEntity;
        private bool _hasPrimaryEntity;

        public bool IsRuntime { get; set; }

        public SimpleBakerContext(IEntityManager entityManager)
        {
            _entityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
            IsRuntime = true;
        }

        public Entity GetEntity(TransformUsageFlags flags)
        {
            if (!_hasPrimaryEntity)
            {
                _primaryEntity = _entityManager.CreateEntity();
                _hasPrimaryEntity = true;
            }
            return _primaryEntity;
        }

        public Entity CreateAdditionalEntity(TransformUsageFlags flags)
        {
            return _entityManager.CreateEntity();
        }

        public void AddComponent<T>(Entity entity, T component) where T : unmanaged, IStradaComponent
        {
            _entityManager.AddComponent<T>(entity);
            _entityManager.SetComponent(entity, component);
        }

        public void AddComponent<T>(Entity entity) where T : unmanaged, IStradaComponent
        {
            _entityManager.AddComponent<T>(entity);
        }

        public DynamicBuffer<T> AddBuffer<T>(Entity entity) where T : struct, IBufferComponent
        {
            // Simplified - in real implementation, this would be managed by EntityManager
            return new DynamicBuffer<T>();
        }

        public void DependsOn(UnityEngine.Object dependency)
        {
            // Track dependency (no-op in simple implementation)
        }

        public void LogInfo(string message)
        {
            Debug.Log(message);
        }

        public void LogWarning(string message)
        {
            Debug.LogWarning(message);
        }

        public void LogError(string message)
        {
            Debug.LogError(message);
        }
    }
}
