using NUnit.Framework;
using Strada.Core.ECS.Groups;

namespace Strada.Core.Tests.ECS.Groups
{
    public struct AliveGroup { }
    public struct DeadGroup { }
    public struct ActiveGroup { }

    [TestFixture]
    public class GroupRegistryTests
    {
        private GroupRegistry _registry;

        [SetUp]
        public void Setup()
        {
            _registry = new GroupRegistry();
        }

        [Test]
        public void GetOrCreate_ReturnsSameIdForSameType()
        {
            var id1 = _registry.GetOrCreate<AliveGroup>();
            var id2 = _registry.GetOrCreate<AliveGroup>();

            Assert.AreEqual(id1, id2);
        }

        [Test]
        public void GetOrCreate_ReturnsDifferentIdForDifferentTypes()
        {
            var id1 = _registry.GetOrCreate<AliveGroup>();
            var id2 = _registry.GetOrCreate<DeadGroup>();

            Assert.AreNotEqual(id1, id2);
        }

        [Test]
        public void AddToGroup_EntityBelongsToGroup()
        {
            var groupId = _registry.GetOrCreate<AliveGroup>();
            _registry.AddToGroup(1, groupId);

            Assert.IsTrue(_registry.IsInGroup(1, groupId));
        }

        [Test]
        public void AddToGroup_Generic_Works()
        {
            _registry.AddToGroup<AliveGroup>(1);

            Assert.IsTrue(_registry.IsInGroup<AliveGroup>(1));
        }

        [Test]
        public void AddToGroup_ChangesGroup_WhenAlreadyInAnother()
        {
            _registry.AddToGroup<AliveGroup>(1);
            _registry.AddToGroup<DeadGroup>(1);

            Assert.IsFalse(_registry.IsInGroup<AliveGroup>(1));
            Assert.IsTrue(_registry.IsInGroup<DeadGroup>(1));
        }

        [Test]
        public void SwapGroup_ChangesEntityGroup()
        {
            _registry.AddToGroup<AliveGroup>(1);
            _registry.SwapGroup<AliveGroup, DeadGroup>(1);

            Assert.IsFalse(_registry.IsInGroup<AliveGroup>(1));
            Assert.IsTrue(_registry.IsInGroup<DeadGroup>(1));
        }

        [Test]
        public void RemoveFromGroup_EntityNoLongerInGroup()
        {
            _registry.AddToGroup<AliveGroup>(1);
            _registry.RemoveFromGroup(1);

            Assert.IsFalse(_registry.IsInGroup<AliveGroup>(1));
            Assert.AreEqual(GroupId.None, _registry.GetEntityGroup(1));
        }

        [Test]
        public void GetEntitiesInGroup_ReturnsCorrectEntities()
        {
            _registry.AddToGroup<AliveGroup>(1);
            _registry.AddToGroup<AliveGroup>(2);
            _registry.AddToGroup<AliveGroup>(3);
            _registry.AddToGroup<DeadGroup>(4);

            var aliveEntities = _registry.GetEntitiesInGroup<AliveGroup>();

            Assert.AreEqual(3, aliveEntities.Count);
            Assert.IsTrue(((System.Collections.Generic.ICollection<int>)aliveEntities).Contains(1));
            Assert.IsTrue(((System.Collections.Generic.ICollection<int>)aliveEntities).Contains(2));
            Assert.IsTrue(((System.Collections.Generic.ICollection<int>)aliveEntities).Contains(3));
        }

        [Test]
        public void GetGroupCount_ReturnsCorrectCount()
        {
            _registry.AddToGroup<AliveGroup>(1);
            _registry.AddToGroup<AliveGroup>(2);
            _registry.AddToGroup<DeadGroup>(3);

            var aliveGroupId = _registry.GetOrCreate<AliveGroup>();

            Assert.AreEqual(2, _registry.GetGroupCount(aliveGroupId));
        }

        [Test]
        public void Clear_RemovesAllEntityGroupAssignments()
        {
            _registry.AddToGroup<AliveGroup>(1);
            _registry.AddToGroup<DeadGroup>(2);

            _registry.Clear();

            Assert.IsFalse(_registry.IsInGroup<AliveGroup>(1));
            Assert.IsFalse(_registry.IsInGroup<DeadGroup>(2));
        }

        [Test]
        public void GetEntityGroup_ReturnsCorrectGroup()
        {
            var expectedGroup = _registry.GetOrCreate<AliveGroup>();
            _registry.AddToGroup(1, expectedGroup);

            var actualGroup = _registry.GetEntityGroup(1);

            Assert.AreEqual(expectedGroup, actualGroup);
        }

        [Test]
        public void GetEntityGroup_ReturnsNone_WhenNotInGroup()
        {
            var group = _registry.GetEntityGroup(999);

            Assert.AreEqual(GroupId.None, group);
        }
    }
}
