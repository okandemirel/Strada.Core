using System;
using FsCheck;
using Strada.Core.ECS;

namespace Strada.Core.Tests.Generators
{
    /// <summary>
    /// Test component for property-based testing.
    /// Simple unmanaged struct that implements IComponent.
    /// </summary>
    public struct TestComponent : IComponent
    {
        public int Value;
        public float FloatValue;
        public bool IsActive;

        public TestComponent(int value, float floatValue, bool isActive)
        {
            Value = value;
            FloatValue = floatValue;
            IsActive = isActive;
        }

        public override string ToString() => $"TestComponent(Value={Value}, Float={FloatValue}, Active={IsActive})";
    }

    /// <summary>
    /// Another test component for multi-component testing scenarios.
    /// </summary>
    public struct TestComponent2 : IComponent
    {
        public int Id;
        public int Data;

        public TestComponent2(int id, int data)
        {
            Id = id;
            Data = data;
        }

        public override string ToString() => $"TestComponent2(Id={Id}, Data={Data})";
    }

    /// <summary>
    /// Third test component for query testing with multiple component types.
    /// </summary>
    public struct TestComponent3 : IComponent
    {
        public float X;
        public float Y;

        public TestComponent3(float x, float y)
        {
            X = x;
            Y = y;
        }

        public override string ToString() => $"TestComponent3(X={X}, Y={Y})";
    }

    /// <summary>
    /// FsCheck generators for test components.
    /// </summary>
    public static class ComponentGenerator
    {
        /// <summary>
        /// Generator for TestComponent with random values.
        /// </summary>
        public static Gen<TestComponent> TestComponentGen =>
            from value in Gen.Choose(-1000, 1000)
            from floatValue in Gen.Choose(-10000, 10000).Select(x => x / 100f)
            from isActive in Arb.Generate<bool>()
            select new TestComponent(value, floatValue, isActive);

        /// <summary>
        /// Generator for TestComponent2 with random values.
        /// </summary>
        public static Gen<TestComponent2> TestComponent2Gen =>
            from id in Gen.Choose(1, 10000)
            from data in Gen.Choose(-1000, 1000)
            select new TestComponent2(id, data);

        /// <summary>
        /// Generator for TestComponent3 with random position values.
        /// </summary>
        public static Gen<TestComponent3> TestComponent3Gen =>
            from x in Gen.Choose(-10000, 10000).Select(v => v / 100f)
            from y in Gen.Choose(-10000, 10000).Select(v => v / 100f)
            select new TestComponent3(x, y);

        /// <summary>
        /// Arbitrary instance for TestComponent.
        /// </summary>
        public static Arbitrary<TestComponent> TestComponentArbitrary =>
            Arb.From(TestComponentGen, ShrinkTestComponent);

        /// <summary>
        /// Arbitrary instance for TestComponent2.
        /// </summary>
        public static Arbitrary<TestComponent2> TestComponent2Arbitrary =>
            Arb.From(TestComponent2Gen, ShrinkTestComponent2);

        /// <summary>
        /// Arbitrary instance for TestComponent3.
        /// </summary>
        public static Arbitrary<TestComponent3> TestComponent3Arbitrary =>
            Arb.From(TestComponent3Gen, ShrinkTestComponent3);

        private static System.Collections.Generic.IEnumerable<TestComponent> ShrinkTestComponent(TestComponent c)
        {
            if (c.Value != 0)
                yield return new TestComponent(c.Value / 2, c.FloatValue, c.IsActive);
            
            if (Math.Abs(c.FloatValue) > 0.01f)
                yield return new TestComponent(c.Value, c.FloatValue / 2, c.IsActive);
        }

        private static System.Collections.Generic.IEnumerable<TestComponent2> ShrinkTestComponent2(TestComponent2 c)
        {
            if (c.Id > 1)
                yield return new TestComponent2(c.Id / 2, c.Data);
            
            if (c.Data != 0)
                yield return new TestComponent2(c.Id, c.Data / 2);
        }

        private static System.Collections.Generic.IEnumerable<TestComponent3> ShrinkTestComponent3(TestComponent3 c)
        {
            if (Math.Abs(c.X) > 0.01f)
                yield return new TestComponent3(c.X / 2, c.Y);
            
            if (Math.Abs(c.Y) > 0.01f)
                yield return new TestComponent3(c.X, c.Y / 2);
        }
    }
}
