using NUnit.Framework;
using Strada.Core.Bridge;

namespace Strada.Core.Tests.Runtime.Bridge
{
    [TestFixture]
    public class ReactiveExtensionsTests
    {
        [Test]
        public void Select_TransformsValue()
        {
            var source = new ReactiveProperty<int>(5);
            var mapped = source.Select(x => x * 2);

            Assert.AreEqual(10, mapped.Value);

            source.Value = 10;
            Assert.AreEqual(20, mapped.Value);

            mapped.Dispose();
        }

        [Test]
        public void Select_NotifiesOnChange()
        {
            var source = new ReactiveProperty<int>(5);
            var mapped = source.Select(x => x * 2);

            int notified = 0;
            mapped.Subscribe(v => notified = v);

            source.Value = 7;
            Assert.AreEqual(14, notified);

            mapped.Dispose();
        }

        [Test]
        public void Where_FiltersValues()
        {
            var source = new ReactiveProperty<int>(10);
            var filtered = source.Where(x => x > 5);

            Assert.AreEqual(10, filtered.Value);

            int lastNotified = 0;
            filtered.Subscribe(v => lastNotified = v);

            source.Value = 3;
            Assert.AreEqual(10, filtered.Value);
            Assert.AreEqual(0, lastNotified);

            source.Value = 8;
            Assert.AreEqual(8, filtered.Value);
            Assert.AreEqual(8, lastNotified);

            filtered.Dispose();
        }

        [Test]
        public void CombineLatest_CombinesTwoSources()
        {
            var a = new ReactiveProperty<int>(2);
            var b = new ReactiveProperty<int>(3);
            var combined = a.CombineLatest(b, (x, y) => x + y);

            Assert.AreEqual(5, combined.Value);

            a.Value = 10;
            Assert.AreEqual(13, combined.Value);

            b.Value = 7;
            Assert.AreEqual(17, combined.Value);

            combined.Dispose();
        }

        [Test]
        public void CombineLatest_NotifiesOnEitherChange()
        {
            var a = new ReactiveProperty<int>(1);
            var b = new ReactiveProperty<int>(1);
            var combined = a.CombineLatest(b, (x, y) => x * y);

            int notifyCount = 0;
            combined.Subscribe(_ => notifyCount++);

            a.Value = 2;
            Assert.AreEqual(1, notifyCount);

            b.Value = 3;
            Assert.AreEqual(2, notifyCount);

            combined.Dispose();
        }

        [Test]
        public void DistinctUntilChanged_IgnoresDuplicates()
        {
            var source = new ReactiveProperty<int>(5);
            var distinct = source.DistinctUntilChanged();

            int notifyCount = 0;
            distinct.Subscribe(_ => notifyCount++);

            source.Value = 5;
            Assert.AreEqual(0, notifyCount);

            source.Value = 10;
            Assert.AreEqual(1, notifyCount);

            source.Value = 10;
            Assert.AreEqual(1, notifyCount);

            source.Value = 5;
            Assert.AreEqual(2, notifyCount);

            distinct.Dispose();
        }

        [Test]
        public void BindTo_SyncsTargetProperty()
        {
            var source = new ReactiveProperty<int>(42);
            var target = new ReactiveProperty<int>(0);

            var binding = source.BindTo(target);

            Assert.AreEqual(42, target.Value);

            source.Value = 100;
            Assert.AreEqual(100, target.Value);

            binding.Dispose();
        }

        [Test]
        public void BindTo_WithConverter_ConvertsValues()
        {
            var source = new ReactiveProperty<int>(10);
            var target = new ReactiveProperty<string>("");

            var binding = source.BindTo(target, x => x.ToString());

            Assert.AreEqual("10", target.Value);

            source.Value = 99;
            Assert.AreEqual("99", target.Value);

            binding.Dispose();
        }
    }

    [TestFixture]
    public class ComputedPropertyTests
    {
        [Test]
        public void ComputedProperty_CalculatesFromSingleDependency()
        {
            var source = new ReactiveProperty<int>(5);
            var computed = ComputedProperty<int>.From(source, x => x * x);

            Assert.AreEqual(25, computed.Value);

            source.Value = 4;
            Assert.AreEqual(16, computed.Value);

            computed.Dispose();
        }

        [Test]
        public void ComputedProperty_CalculatesFromTwoDependencies()
        {
            var width = new ReactiveProperty<int>(10);
            var height = new ReactiveProperty<int>(5);
            var area = ComputedProperty<int>.From(width, height, (w, h) => w * h);

            Assert.AreEqual(50, area.Value);

            width.Value = 20;
            Assert.AreEqual(100, area.Value);

            height.Value = 10;
            Assert.AreEqual(200, area.Value);

            area.Dispose();
        }

        [Test]
        public void ComputedProperty_NotifiesOnDependencyChange()
        {
            var source = new ReactiveProperty<int>(2);
            var computed = ComputedProperty<int>.From(source, x => x + 10);

            int notified = 0;
            computed.Subscribe(v => notified = v);

            source.Value = 5;
            Assert.AreEqual(15, notified);

            computed.Dispose();
        }

        [Test]
        public void ComputedProperty_DoesNotNotifyIfResultUnchanged()
        {
            var source = new ReactiveProperty<int>(5);
            var computed = ComputedProperty<int>.From(source, x => x > 0 ? 1 : 0);

            int notifyCount = 0;
            computed.Subscribe(_ => notifyCount++);

            source.Value = 10;
            Assert.AreEqual(0, notifyCount);

            source.Value = -1;
            Assert.AreEqual(1, notifyCount);

            computed.Dispose();
        }
    }

    [TestFixture]
    public class BindingScopeTests
    {
        [Test]
        public void BindingScope_DisposesTrackedItems()
        {
            var scope = new BindingScope();

            var source = new ReactiveProperty<int>(5);
            var mapped = scope.Select(source, x => x * 2);

            int lastValue = 0;
            scope.Subscribe(mapped, v => lastValue = v);

            source.Value = 10;
            Assert.AreEqual(20, lastValue);

            scope.Dispose();

            source.Value = 100;
            Assert.AreEqual(20, lastValue);
        }

        [Test]
        public void BindingScope_UnsubscribesOnDispose()
        {
            var scope = new BindingScope();
            var prop = new ReactiveProperty<int>(0);

            int callCount = 0;
            scope.Subscribe(prop, _ => callCount++);

            prop.Value = 1;
            Assert.AreEqual(1, callCount);

            scope.Dispose();

            prop.Value = 2;
            Assert.AreEqual(1, callCount);
        }

        [Test]
        public void TwoWayBinding_SyncsBothDirections()
        {
            var a = new ReactiveProperty<int>(10);
            var b = new ReactiveProperty<int>(0);

            var binding = new TwoWayBinding<int>(a, b);

            Assert.AreEqual(10, b.Value);

            a.Value = 20;
            Assert.AreEqual(20, b.Value);

            b.Value = 30;
            Assert.AreEqual(30, a.Value);

            binding.Dispose();
        }

        [Test]
        public void TwoWayBinding_WithConversion_ConvertsInBothDirections()
        {
            var intProp = new ReactiveProperty<int>(42);
            var strProp = new ReactiveProperty<string>("");

            var binding = new TwoWayBinding<int, string>(
                intProp, strProp,
                i => i.ToString(),
                s => int.TryParse(s, out var v) ? v : 0);

            Assert.AreEqual("42", strProp.Value);

            intProp.Value = 100;
            Assert.AreEqual("100", strProp.Value);

            strProp.Value = "55";
            Assert.AreEqual(55, intProp.Value);

            binding.Dispose();
        }

        [Test]
        public void ValidatedBinding_RejectsInvalidValues()
        {
            var source = new ReactiveProperty<int>(50);
            var target = new ReactiveProperty<int>(0);

            int invalidCount = 0;
            var binding = new ValidatedBinding<int>(
                source, target,
                v => v >= 0 && v <= 100,
                _ => invalidCount++);

            Assert.AreEqual(50, target.Value);

            source.Value = 75;
            Assert.AreEqual(75, target.Value);
            Assert.AreEqual(0, invalidCount);

            source.Value = 150;
            Assert.AreEqual(75, target.Value);
            Assert.AreEqual(1, invalidCount);

            binding.Dispose();
        }
    }
}
