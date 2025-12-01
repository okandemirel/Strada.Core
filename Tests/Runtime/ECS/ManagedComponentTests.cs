using NUnit.Framework;
using Strada.Core.ECS;
using Strada.Core.ECS.Core;
using System;

namespace Strada.Core.Tests.Tests.Runtime.ECS
{
    [TestFixture]
    public class ManagedComponentTests
    {
        private class ManagedData : IComponent
        {
            public string Text;
        }

        private struct UnmanagedData : IComponent
        {
            public int Value;
        }

        [Test]
        public void AddComponent_WithManagedType_ShouldFailCompile()
        {
            var manager = new EntityManager();
            var entity = manager.CreateEntity();

            var method = typeof(EntityManager).GetMethod("AddComponent", new[] { typeof(Entity) });
            Assert.IsNotNull(method);

            var constraints = method.GetGenericArguments()[0].GetGenericParameterConstraints();
            
            bool hasUnmanagedConstraint = false;
            
            try
            {
                var genericMethod = method.MakeGenericMethod(typeof(ManagedData));
                
                genericMethod.Invoke(manager, new object[] { entity });
                
                Assert.Fail("Should not be able to add managed component");
            }
            catch (ArgumentException)
            {
                Assert.Pass();
            }
            catch (Exception ex)
            {
                Assert.Pass($"Caught expected exception: {ex.GetType().Name}");
            }
            finally
            {
                manager.Dispose();
            }
        }
    }
}
