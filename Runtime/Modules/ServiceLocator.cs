using System;
using Strada.Core.DI;

namespace Strada.Core.Modules
{
    /// <summary>
    /// Implementation of IServiceLocator that wraps an IContainer.
    /// Provides a read-only service resolution API without exposing container internals.
    /// </summary>
    public sealed class ServiceLocator : IServiceLocator
    {
        private readonly IContainer _container;

        /// <summary>
        /// Creates a new ServiceLocator wrapping the given container.
        /// </summary>
        /// <param name="container">The underlying DI container.</param>
        public ServiceLocator(IContainer container)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
        }

        /// <inheritdoc/>
        public T Get<T>() where T : class
        {
            return _container.Resolve<T>();
        }

        /// <inheritdoc/>
        public object Get(Type serviceType)
        {
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            return _container.Resolve(serviceType);
        }

        /// <inheritdoc/>
        public bool TryGet<T>(out T service) where T : class
        {
            return _container.TryResolve(out service);
        }

        /// <inheritdoc/>
        public bool TryGet(Type serviceType, out object service)
        {
            if (serviceType == null)
            {
                service = null;
                return false;
            }

            try
            {
                service = _container.Resolve(serviceType);
                return service != null;
            }
            catch
            {
                service = null;
                return false;
            }
        }

        /// <inheritdoc/>
        public bool IsRegistered<T>() where T : class
        {
            return _container.IsRegistered<T>();
        }

        /// <inheritdoc/>
        public bool IsRegistered(Type serviceType)
        {
            if (serviceType == null)
                return false;

            return _container.IsRegistered(serviceType);
        }
    }
}
