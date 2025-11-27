using NUnit.Framework;
using Strada.Core.ECS;
using Strada.Core.ECS.Storage;
using Unity.Collections;

namespace Strada.Core.Tests.Tests.Runtime.ECS.Storage
{
    [TestFixture]
    public class SparseSetTests
    {
        struct TestComponent : IComponent
        {
            public int Value;
        }

        [Test]
        public void Add_SingleElement_Success()
        {
            var set = new SparseSet<TestComponent>(10, 10, Allocator.Temp);

            set.Add(5, new TestComponent { Value = 42 });

            Assert.IsTrue(set.Contains(5));
            Assert.AreEqual(42, set.Get(5).Value);
            Assert.AreEqual(1, set.Count);

            set.Dispose();
        }

        [Test]
        public void Add_MultipleElements_Success()
        {
            var set = new SparseSet<TestComponent>(10, 10, Allocator.Temp);

            for (int i = 0; i < 5; i++)
            {
                set.Add(i, new TestComponent { Value = i * 10 });
            }

            Assert.AreEqual(5, set.Count);
            for (int i = 0; i < 5; i++)
            {
                Assert.IsTrue(set.Contains(i));
                Assert.AreEqual(i * 10, set.Get(i).Value);
            }

            set.Dispose();
        }

        [Test]
        public void Remove_ExistingElement_Success()
        {
            var set = new SparseSet<TestComponent>(10, 10, Allocator.Temp);

            set.Add(5, new TestComponent { Value = 42 });
            bool removed = set.Remove(5);

            Assert.IsTrue(removed);
            Assert.IsFalse(set.Contains(5));
            Assert.AreEqual(0, set.Count);

            set.Dispose();
        }

        [Test]
        public void Remove_NonExistingElement_ReturnsFalse()
        {
            var set = new SparseSet<TestComponent>(10, 10, Allocator.Temp);

            bool removed = set.Remove(5);

            Assert.IsFalse(removed);

            set.Dispose();
        }

        [Test]
        public void Remove_SwapAndPop_MaintainsDensity()
        {
            var set = new SparseSet<TestComponent>(10, 10, Allocator.Temp);

            set.Add(0, new TestComponent { Value = 10 });
            set.Add(1, new TestComponent { Value = 20 });
            set.Add(2, new TestComponent { Value = 30 });

            set.Remove(1);

            Assert.AreEqual(2, set.Count);
            Assert.IsFalse(set.Contains(1));
            Assert.IsTrue(set.Contains(0));
            Assert.IsTrue(set.Contains(2));

            set.Dispose();
        }

        [Test]
        public void Set_ExistingElement_UpdatesValue()
        {
            var set = new SparseSet<TestComponent>(10, 10, Allocator.Temp);

            set.Add(5, new TestComponent { Value = 42 });
            set.Set(5, new TestComponent { Value = 100 });

            Assert.AreEqual(100, set.Get(5).Value);

            set.Dispose();
        }

        [Test]
        public void TryGet_ExistingElement_ReturnsTrue()
        {
            var set = new SparseSet<TestComponent>(10, 10, Allocator.Temp);

            set.Add(5, new TestComponent { Value = 42 });

            bool success = set.TryGet(5, out var component);

            Assert.IsTrue(success);
            Assert.AreEqual(42, component.Value);

            set.Dispose();
        }

        [Test]
        public void TryGet_NonExistingElement_ReturnsFalse()
        {
            var set = new SparseSet<TestComponent>(10, 10, Allocator.Temp);

            bool success = set.TryGet(5, out var component);

            Assert.IsFalse(success);
            Assert.AreEqual(0, component.Value);

            set.Dispose();
        }

        [Test]
        public void Clear_RemovesAllElements()
        {
            var set = new SparseSet<TestComponent>(10, 10, Allocator.Temp);

            for (int i = 0; i < 5; i++)
            {
                set.Add(i, new TestComponent { Value = i });
            }

            set.Clear();

            Assert.AreEqual(0, set.Count);
            for (int i = 0; i < 5; i++)
            {
                Assert.IsFalse(set.Contains(i));
            }

            set.Dispose();
        }

        [Test]
        public void Add_ExistingElement_UpdatesValue()
        {
            var set = new SparseSet<TestComponent>(10, 10, Allocator.Temp);

            set.Add(5, new TestComponent { Value = 42 });
            set.Add(5, new TestComponent { Value = 100 });

            Assert.AreEqual(1, set.Count);
            Assert.AreEqual(100, set.Get(5).Value);

            set.Dispose();
        }

        [Test]
        public void Iteration_DenseArray_CacheFriendly()
        {
            var set = new SparseSet<TestComponent>(100, 100, Allocator.Temp);

            for (int i = 0; i < 50; i += 2)
            {
                set.Add(i, new TestComponent { Value = i });
            }

            int sum = 0;
            unsafe
            {
                TestComponent* data = set.GetDataReadOnlyPtr();

                for (int i = 0; i < set.Count; i++)
                {
                    sum += data[i].Value;
                }
            }

            int expectedSum = 0;
            for (int i = 0; i < 50; i += 2)
            {
                expectedSum += i;
            }

            Assert.AreEqual(expectedSum, sum);

            set.Dispose();
        }

        [Test]
        public void GrowCapacity_Automatically()
        {
            var set = new SparseSet<TestComponent>(2, 2, Allocator.Temp);

            for (int i = 0; i < 10; i++)
            {
                set.Add(i, new TestComponent { Value = i });
            }

            Assert.AreEqual(10, set.Count);
            for (int i = 0; i < 10; i++)
            {
                Assert.IsTrue(set.Contains(i));
                Assert.AreEqual(i, set.Get(i).Value);
            }

            set.Dispose();
        }
    }
}
