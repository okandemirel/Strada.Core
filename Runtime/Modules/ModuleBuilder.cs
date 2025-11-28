using System;
using Strada.Core.DI;

namespace Strada.Core.Modules
{
    /// <summary>
    /// Implementation of IModuleBuilder that wraps the DI ContainerBuilder.
    /// Provides a fluent, VContainer-like API for module configuration.
    /// </summary>
    public sealed class ModuleBuilder : IModuleBuilder
    {
        private readonly ContainerBuilder _containerBuilder;

        /// <summary>
        /// Creates a new ModuleBuilder wrapping the given ContainerBuilder.
        /// </summary>
        /// <param name="containerBuilder">The underlying container builder.</param>
        public ModuleBuilder(ContainerBuilder containerBuilder)
        {
            _containerBuilder = containerBuilder ?? throw new ArgumentNullException(nameof(containerBuilder));
        }

        /// <inheritdoc/>
        public IModuleBuilder Register<TInterface, TImplementation>(Lifetime lifetime = Lifetime.Singleton)
            where TInterface : class
            where TImplementation : class, TInterface
        {
            _containerBuilder.Register<TInterface, TImplementation>(lifetime);
            return this;
        }

        /// <inheritdoc/>
        public IModuleBuilder Register<T>(Lifetime lifetime = Lifetime.Singleton) where T : class
        {
            _containerBuilder.Register<T>(lifetime);
            return this;
        }

        /// <inheritdoc/>
        public IModuleBuilder Register(Type interfaceType, Type implementationType, Lifetime lifetime = Lifetime.Singleton)
        {
            if (interfaceType == null || implementationType == null)
            {
                throw new ArgumentNullException(interfaceType == null ? nameof(interfaceType) : nameof(implementationType));
            }

            var registerMethod = typeof(ContainerBuilder)
                .GetMethod(nameof(ContainerBuilder.Register), new[] { typeof(Lifetime) });

            if (registerMethod == null)
            {
                throw new InvalidOperationException("Could not find Register method on ContainerBuilder");
            }

            if (interfaceType == implementationType)
            {
                var genericMethod = registerMethod.MakeGenericMethod(implementationType);
                genericMethod.Invoke(_containerBuilder, new object[] { lifetime });
            }
            else
            {
                var twoTypeRegisterMethod = typeof(ContainerBuilder)
                    .GetMethod(nameof(ContainerBuilder.Register), 2, new[] { typeof(Lifetime) });

                if (twoTypeRegisterMethod != null)
                {
                    var genericMethod = twoTypeRegisterMethod.MakeGenericMethod(interfaceType, implementationType);
                    genericMethod.Invoke(_containerBuilder, new object[] { lifetime });
                }
            }

            return this;
        }

        /// <inheritdoc/>
        public IModuleBuilder RegisterInstance<T>(T instance) where T : class
        {
            _containerBuilder.RegisterInstance(instance);
            return this;
        }

        /// <inheritdoc/>
        public IModuleBuilder RegisterFactory<T>(Func<IServiceLocator, T> factory, Lifetime lifetime = Lifetime.Singleton)
            where T : class
        {
            _containerBuilder.RegisterFactory<T>(container =>
            {
                var serviceLocator = new ServiceLocator(container);
                return factory(serviceLocator);
            }, lifetime);
            return this;
        }

        /// <inheritdoc/>
        public IModuleBuilder RegisterModel<TInterface, TImplementation>()
            where TInterface : class
            where TImplementation : class, TInterface
        {
            return Register<TInterface, TImplementation>(Lifetime.Singleton);
        }

        /// <inheritdoc/>
        public IModuleBuilder RegisterController<T>() where T : class
        {
            return Register<T>(Lifetime.Singleton);
        }

        /// <inheritdoc/>
        public IModuleBuilder RegisterService<TInterface, TImplementation>()
            where TInterface : class
            where TImplementation : class, TInterface
        {
            return Register<TInterface, TImplementation>(Lifetime.Singleton);
        }

        /// <inheritdoc/>
        public IModuleBuilder RegisterFactory<TInterface, TImplementation>()
            where TInterface : class
            where TImplementation : class, TInterface
        {
            return Register<TInterface, TImplementation>(Lifetime.Singleton);
        }
    }
}
