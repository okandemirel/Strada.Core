using System;
using UnityEngine;
using Strada.Core.DI;

namespace Strada.Core.Modules
{
    /// <summary>
    /// Serializable entry for configuring a service in a ModuleConfig.
    /// Allows registering services via the Inspector instead of code.
    /// </summary>
    [Serializable]
    public class ServiceEntry
    {
        [Tooltip("The service interface type (optional - if not set, implementation type is used)")]
        [SerializeField] private SerializableType _interfaceType;

        [Tooltip("The service implementation type")]
        [SerializeField] private SerializableType _implementationType;

        [Tooltip("Service lifetime")]
        [SerializeField] private Lifetime _lifetime = Lifetime.Singleton;

        [Tooltip("Whether this service is enabled")]
        [SerializeField] private bool _enabled = true;

        /// <summary>
        /// Gets the service interface type. If not set, returns the implementation type.
        /// </summary>
        public SerializableType InterfaceType => _interfaceType;

        /// <summary>
        /// Gets the service implementation type.
        /// </summary>
        public SerializableType ImplementationType => _implementationType;

        /// <summary>
        /// Gets the service lifetime.
        /// </summary>
        public Lifetime Lifetime => _lifetime;

        /// <summary>
        /// Gets whether this service is enabled.
        /// </summary>
        public bool Enabled => _enabled;

        /// <summary>
        /// Gets the resolved interface Type. Returns implementation type if interface is not set.
        /// </summary>
        public Type GetInterfaceType()
        {
            var interfaceType = _interfaceType?.Type;
            return interfaceType ?? _implementationType?.Type;
        }

        /// <summary>
        /// Gets the resolved implementation Type.
        /// </summary>
        public Type GetImplementationType() => _implementationType?.Type;

        /// <summary>
        /// Gets whether this entry has a valid implementation type assigned.
        /// </summary>
        public bool IsValid => _implementationType != null && _implementationType.IsValid;

        /// <summary>
        /// Gets the display name for this service.
        /// </summary>
        public string DisplayName
        {
            get
            {
                var interfaceTypeName = _interfaceType?.TypeName;
                var implTypeName = _implementationType?.TypeName ?? "(None)";

                if (!string.IsNullOrEmpty(interfaceTypeName) && interfaceTypeName != implTypeName)
                {
                    return $"{interfaceTypeName} → {implTypeName}";
                }
                return implTypeName;
            }
        }

        public ServiceEntry() { }

        public ServiceEntry(Type interfaceType, Type implementationType, Lifetime lifetime = Lifetime.Singleton, bool enabled = true)
        {
            _interfaceType = interfaceType != null ? new SerializableType(interfaceType) : null;
            _implementationType = new SerializableType(implementationType);
            _lifetime = lifetime;
            _enabled = enabled;
        }

        public ServiceEntry(Type implementationType, Lifetime lifetime = Lifetime.Singleton, bool enabled = true)
            : this(null, implementationType, lifetime, enabled) { }
    }
}
