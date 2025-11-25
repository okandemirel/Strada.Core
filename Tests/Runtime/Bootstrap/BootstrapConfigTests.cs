using System.Linq;
using NUnit.Framework;
using Strada.Core.Bootstrap;

namespace Strada.Core.Tests.Tests.Runtime.Bootstrap
{
    [TestFixture]
    public class BootstrapConfigTests
    {
        [Test]
        public void CreateDefault_CreatesValidConfiguration()
        {
            var config = BootstrapConfig.CreateDefault();

            Assert.IsNotNull(config);
            Assert.IsTrue(config.AutoDiscoverModules);
            Assert.IsTrue(config.VerboseLogging);
            Assert.IsTrue(config.ValidateDependencies);
            Assert.IsTrue(config.FailOnValidationError);
            Assert.IsFalse(config.AsyncInitialization);
        }

        [Test]
        public void CreateDefault_HasDefaultAssemblyPatterns()
        {
            var config = BootstrapConfig.CreateDefault();

            Assert.IsNotNull(config.AssemblyIncludePatterns);
            Assert.Greater(config.AssemblyIncludePatterns.Count, 0);
            Assert.IsTrue(config.AssemblyIncludePatterns.Contains("Strada.*"));

            Assert.IsNotNull(config.AssemblyExcludePatterns);
            Assert.Greater(config.AssemblyExcludePatterns.Count, 0);
            Assert.IsTrue(config.AssemblyExcludePatterns.Contains("Unity.*"));
        }

        [Test]
        public void ManualModules_InitiallyEmpty()
        {
            var config = BootstrapConfig.CreateDefault();

            Assert.IsNotNull(config.ManualModules);
            Assert.AreEqual(0, config.ManualModules.Count);
        }

        [Test]
        public void ModuleReference_CanSetProperties()
        {
            var moduleRef = new ModuleReference
            {
                TypeName = "Test.Module",
                Priority = 10,
                Enabled = false
            };

            Assert.AreEqual("Test.Module", moduleRef.TypeName);
            Assert.AreEqual(10, moduleRef.Priority);
            Assert.IsFalse(moduleRef.Enabled);
        }

        [Test]
        public void ModuleReference_DefaultsToEnabled()
        {
            var moduleRef = new ModuleReference();

            Assert.IsTrue(moduleRef.Enabled);
        }
    }
}
