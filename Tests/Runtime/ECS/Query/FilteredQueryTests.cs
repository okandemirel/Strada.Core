using NUnit.Framework;
using Strada.Core.ECS;
using Strada.Core.ECS.Query;

namespace Strada.Core.Tests.ECS.Query
{
    public struct Position : IComponent { public float X, Y; }
    public struct Velocity : IComponent { public float X, Y; }
    public struct Alive : IComponent { }
    public struct Dead : IComponent { }
    public struct Stunned : IComponent { }

    [TestFixture]
    public class FilteredQueryTests
    {
        private EntityManager _manager;

        [SetUp]
        public void Setup() => _manager = new EntityManager();

        [TearDown]
        public void TearDown() => _manager?.Dispose();

        [Test]
        public void Filter_Also_OnlyMatchesEntitiesWithBothComponents()
        {
            var e1 = _manager.CreateEntity();
            _manager.AddComponent(e1, new Position { X = 1 });
            _manager.AddComponent(e1, new Alive());

            var e2 = _manager.CreateEntity();
            _manager.AddComponent(e2, new Position { X = 2 });

            var e3 = _manager.CreateEntity();
            _manager.AddComponent(e3, new Position { X = 3 });
            _manager.AddComponent(e3, new Alive());

            int count = 0;
            float sum = 0;

            _manager.Query()
                .Filter<Position>()
                .Also<Alive>()
                .ForEach((int e, ref Position p) =>
                {
                    count++;
                    sum += p.X;
                });

            Assert.AreEqual(2, count);
            Assert.AreEqual(4f, sum);
        }

        [Test]
        public void Filter_None_ExcludesEntitiesWithComponent()
        {
            var e1 = _manager.CreateEntity();
            _manager.AddComponent(e1, new Position { X = 1 });
            _manager.AddComponent(e1, new Alive());

            var e2 = _manager.CreateEntity();
            _manager.AddComponent(e2, new Position { X = 2 });
            _manager.AddComponent(e2, new Dead());

            var e3 = _manager.CreateEntity();
            _manager.AddComponent(e3, new Position { X = 3 });
            _manager.AddComponent(e3, new Alive());

            int count = 0;
            float sum = 0;

            _manager.Query()
                .Filter<Position>()
                .None<Dead>()
                .ForEach((int e, ref Position p) =>
                {
                    count++;
                    sum += p.X;
                });

            Assert.AreEqual(2, count);
            Assert.AreEqual(4f, sum);
        }

        [Test]
        public void Filter_AlsoAndNone_Combined()
        {
            var e1 = _manager.CreateEntity();
            _manager.AddComponent(e1, new Position { X = 1 });
            _manager.AddComponent(e1, new Alive());

            var e2 = _manager.CreateEntity();
            _manager.AddComponent(e2, new Position { X = 2 });
            _manager.AddComponent(e2, new Alive());
            _manager.AddComponent(e2, new Stunned());

            var e3 = _manager.CreateEntity();
            _manager.AddComponent(e3, new Position { X = 3 });
            _manager.AddComponent(e3, new Alive());

            var e4 = _manager.CreateEntity();
            _manager.AddComponent(e4, new Position { X = 4 });

            int count = 0;
            float sum = 0;

            _manager.Query()
                .Filter<Position>()
                .Also<Alive>()
                .None<Stunned>()
                .ForEach((int e, ref Position p) =>
                {
                    count++;
                    sum += p.X;
                });

            Assert.AreEqual(2, count);
            Assert.AreEqual(4f, sum);
        }

        [Test]
        public void Filter_TwoComponents_WithAlso()
        {
            var e1 = _manager.CreateEntity();
            _manager.AddComponent(e1, new Position { X = 1 });
            _manager.AddComponent(e1, new Velocity { X = 10 });
            _manager.AddComponent(e1, new Alive());

            var e2 = _manager.CreateEntity();
            _manager.AddComponent(e2, new Position { X = 2 });
            _manager.AddComponent(e2, new Velocity { X = 20 });

            int count = 0;

            _manager.Query()
                .Filter<Position, Velocity>()
                .Also<Alive>()
                .ForEach((int e, ref Position p, ref Velocity v) =>
                {
                    count++;
                    p.X += v.X;
                });

            Assert.AreEqual(1, count);
            Assert.AreEqual(11f, _manager.GetComponent<Position>(e1).X);
        }

        [Test]
        public void Filter_ThreeComponents_WithNone()
        {
            var e1 = _manager.CreateEntity();
            _manager.AddComponent(e1, new Position { X = 1 });
            _manager.AddComponent(e1, new Velocity { X = 10 });
            _manager.AddComponent(e1, new Alive());

            var e2 = _manager.CreateEntity();
            _manager.AddComponent(e2, new Position { X = 2 });
            _manager.AddComponent(e2, new Velocity { X = 20 });
            _manager.AddComponent(e2, new Alive());
            _manager.AddComponent(e2, new Dead());

            int count = 0;

            _manager.Query()
                .Filter<Position, Velocity, Alive>()
                .None<Dead>()
                .ForEach((int e, ref Position p, ref Velocity v, ref Alive a) =>
                {
                    count++;
                });

            Assert.AreEqual(1, count);
        }

        [Test]
        public void Filter_MultipleNone()
        {
            var e1 = _manager.CreateEntity();
            _manager.AddComponent(e1, new Position { X = 1 });

            var e2 = _manager.CreateEntity();
            _manager.AddComponent(e2, new Position { X = 2 });
            _manager.AddComponent(e2, new Dead());

            var e3 = _manager.CreateEntity();
            _manager.AddComponent(e3, new Position { X = 3 });
            _manager.AddComponent(e3, new Stunned());

            var e4 = _manager.CreateEntity();
            _manager.AddComponent(e4, new Position { X = 4 });
            _manager.AddComponent(e4, new Dead());
            _manager.AddComponent(e4, new Stunned());

            int count = 0;

            _manager.Query()
                .Filter<Position>()
                .None<Dead>()
                .None<Stunned>()
                .ForEach((int e, ref Position p) =>
                {
                    count++;
                });

            Assert.AreEqual(1, count);
        }

        [Test]
        public void Filter_MultipleAlso()
        {
            var e1 = _manager.CreateEntity();
            _manager.AddComponent(e1, new Position { X = 1 });
            _manager.AddComponent(e1, new Alive());
            _manager.AddComponent(e1, new Velocity());

            var e2 = _manager.CreateEntity();
            _manager.AddComponent(e2, new Position { X = 2 });
            _manager.AddComponent(e2, new Alive());

            var e3 = _manager.CreateEntity();
            _manager.AddComponent(e3, new Position { X = 3 });
            _manager.AddComponent(e3, new Velocity());

            int count = 0;

            _manager.Query()
                .Filter<Position>()
                .Also<Alive>()
                .Also<Velocity>()
                .ForEach((int e, ref Position p) =>
                {
                    count++;
                });

            Assert.AreEqual(1, count);
        }
    }
}
