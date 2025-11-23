using NUnit.Framework;
using System;
using System.Linq;
using Strada.Core.DI;
using Strada.Core.Modules;

namespace Strada.Core.Tests.Modules
{
    [TestFixture]
    public class ModuleRegistryTests
    {
        private ModuleRegistry _registry;

        #region Test Module Installers

        public class TestModuleA : IModuleInstaller
        {
            public bool InstallCalled { get; private set; }
            public bool InitializeCalled { get; private set; }
            public bool ShutdownCalled { get; private set; }

            public void Install(IContainerBuilder builder)
            {
                InstallCalled = true;
            }

            public void Initialize(IContainer container)
            {
                InitializeCalled = true;
            }

            public void Shutdown()
            {
                ShutdownCalled = true;
            }
        }

        public class TestModuleB : IModuleInstaller
        {
            public void Install(IContainerBuilder builder) { }
        }

        [ModulePriority(10)]
        public class HighPriorityModule : IModuleInstaller
        {
            public void Install(IContainerBuilder builder) { }
        }

        [ModulePriority(-10)]
        public class LowPriorityModule : IModuleInstaller
        {
            public void Install(IContainerBuilder builder) { }
        }

        [ModuleDependsOn(typeof(TestModuleA))]
        public class DependentModule : IModuleInstaller
        {
            public void Install(IContainerBuilder builder) { }
        }

        [ModuleDependsOn(typeof(DependentModule))]
        public class ChainedDependentModule : IModuleInstaller
        {
            public void Install(IContainerBuilder builder) { }
        }

        [ModuleDependsOn(typeof(CircularModuleB))]
        public class CircularModuleA : IModuleInstaller
        {
            public void Install(IContainerBuilder builder) { }
        }

        [ModuleDependsOn(typeof(CircularModuleA))]
        public class CircularModuleB : IModuleInstaller
        {
            public void Install(IContainerBuilder builder) { }
        }

        #endregion

        [SetUp]
        public void SetUp()
        {
            _registry = new ModuleRegistry();
        }

        [TearDown]
        public void TearDown()
        {
            _registry.Clear();
        }

        #region Registration Tests

        [Test]
        public void RegisterModule_WithValidInstaller_RegistersSuccessfully()
        {
            var installer = new TestModuleA();

            _registry.RegisterModule(installer);

            Assert.AreEqual(1, _registry.Modules.Count);
            Assert.AreEqual(installer, _registry.Modules[0].Installer);
            Assert.AreEqual(typeof(TestModuleA), _registry.Modules[0].Type);
        }

        [Test]
        public void RegisterModule_WithPriority_StoresPriority()
        {
            var installer = new TestModuleA();

            _registry.RegisterModule(installer, priority: 42);

            Assert.AreEqual(42, _registry.Modules[0].Priority);
        }

        [Test]
        public void RegisterModule_WithNullInstaller_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _registry.RegisterModule(null));
        }

        [Test]
        public void RegisterModule_WithDuplicateModule_DoesNotRegisterTwice()
        {
            var installer = new TestModuleA();

            _registry.RegisterModule(installer);
            _registry.RegisterModule(installer);

            Assert.AreEqual(1, _registry.Modules.Count);
        }

        [Test]
        public void RegisterModule_WithMultipleModules_RegistersAll()
        {
            var installerA = new TestModuleA();
            var installerB = new TestModuleB();

            _registry.RegisterModule(installerA);
            _registry.RegisterModule(installerB);

            Assert.AreEqual(2, _registry.Modules.Count);
        }

        #endregion

        #region Discovery Tests

        [Test]
        public void DiscoverModules_FindsModulesInCurrentAssembly()
        {
            _registry.DiscoverModules(assembly => assembly == GetType().Assembly);

            var moduleTypes = _registry.Modules.Select(m => m.Type).ToList();
            Assert.IsTrue(moduleTypes.Contains(typeof(TestModuleA)));
            Assert.IsTrue(moduleTypes.Contains(typeof(TestModuleB)));
        }

        [Test]
        public void DiscoverModules_RespectsAssemblyFilter()
        {
            _registry.DiscoverModules(assembly => false);

            Assert.AreEqual(0, _registry.Modules.Count);
        }

        [Test]
        public void DiscoverModules_ExtractsPriorityFromAttribute()
        {
            _registry.DiscoverModules(assembly => assembly == GetType().Assembly);

            var highPriorityModule = _registry.Modules.FirstOrDefault(m => m.Type == typeof(HighPriorityModule));
            var lowPriorityModule = _registry.Modules.FirstOrDefault(m => m.Type == typeof(LowPriorityModule));

            Assert.IsNotNull(highPriorityModule);
            Assert.AreEqual(10, highPriorityModule.Priority);

            Assert.IsNotNull(lowPriorityModule);
            Assert.AreEqual(-10, lowPriorityModule.Priority);
        }

        #endregion

        #region Dependency Tests

        [Test]
        public void RegisterModule_ExtractsDependenciesFromAttribute()
        {
            _registry.RegisterModule(new DependentModule());

            var module = _registry.Modules[0];
            Assert.AreEqual(1, module.Dependencies.Count);
            Assert.AreEqual(typeof(TestModuleA), module.Dependencies[0]);
        }

        [Test]
        public void DiscoverModules_SortsModulesByDependencies()
        {
            _registry.RegisterModule(new DependentModule());
            _registry.RegisterModule(new TestModuleA());
            _registry.Sort();

            var moduleOrder = _registry.Modules.Select(m => m.Type).ToList();

            var indexA = moduleOrder.IndexOf(typeof(TestModuleA));
            var indexDependent = moduleOrder.IndexOf(typeof(DependentModule));

            Assert.Less(indexA, indexDependent, "TestModuleA should come before DependentModule");
        }

        [Test]
        public void DiscoverModules_SortsChainedDependencies()
        {
            _registry.RegisterModule(new ChainedDependentModule());
            _registry.RegisterModule(new DependentModule());
            _registry.RegisterModule(new TestModuleA());
            _registry.Sort();

            var moduleOrder = _registry.Modules.Select(m => m.Type).ToList();

            var indexA = moduleOrder.IndexOf(typeof(TestModuleA));
            var indexDependent = moduleOrder.IndexOf(typeof(DependentModule));
            var indexChained = moduleOrder.IndexOf(typeof(ChainedDependentModule));

            Assert.Less(indexA, indexDependent);
            Assert.Less(indexDependent, indexChained);
        }

        #endregion

        #region Validation Tests

        [Test]
        public void Validate_WithValidModules_ReturnsTrue()
        {
            _registry.RegisterModule(new TestModuleA());
            _registry.RegisterModule(new TestModuleB());

            var isValid = _registry.Validate(out var errorMessage);

            Assert.IsTrue(isValid);
            Assert.IsNull(errorMessage);
        }

        [Test]
        public void Validate_WithCircularDependency_ReturnsFalse()
        {
            _registry.RegisterModule(new CircularModuleA());
            _registry.RegisterModule(new CircularModuleB());

            var isValid = _registry.Validate(out var errorMessage);

            Assert.IsFalse(isValid);
            Assert.IsNotNull(errorMessage);
            Assert.IsTrue(errorMessage.Contains("Circular"));
        }

        [Test]
        public void Validate_WithMissingDependency_ReturnsTrue()
        {
            _registry.RegisterModule(new DependentModule());

            var isValid = _registry.Validate(out var errorMessage);

            Assert.IsTrue(isValid);
        }

        #endregion

        #region Clear Tests

        [Test]
        public void Clear_RemovesAllModules()
        {
            _registry.RegisterModule(new TestModuleA());
            _registry.RegisterModule(new TestModuleB());

            _registry.Clear();

            Assert.AreEqual(0, _registry.Modules.Count);
        }

        [Test]
        public void Clear_AllowsReRegistration()
        {
            var installer = new TestModuleA();

            _registry.RegisterModule(installer);
            _registry.Clear();
            _registry.RegisterModule(installer);

            Assert.AreEqual(1, _registry.Modules.Count);
        }

        #endregion

        #region ModuleInfo Tests

        [Test]
        public void ModuleInfo_ContainsCorrectData()
        {
            var installer = new TestModuleA();
            _registry.RegisterModule(installer, priority: 5);

            var moduleInfo = _registry.Modules[0];

            Assert.AreEqual(installer, moduleInfo.Installer);
            Assert.AreEqual(typeof(TestModuleA), moduleInfo.Type);
            Assert.AreEqual("TestModuleA", moduleInfo.Name);
            Assert.AreEqual(5, moduleInfo.Priority);
            Assert.IsNotNull(moduleInfo.Dependencies);
        }

        #endregion
    }
}
