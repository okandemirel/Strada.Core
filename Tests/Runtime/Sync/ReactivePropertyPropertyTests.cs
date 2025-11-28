using System.Collections.Generic;
using FsCheck;
using NUnit.Framework;
using Strada.Core.Sync;
using Strada.Core.Tests.Tests.Runtime.Generators;

namespace Strada.Core.Tests.Tests.Runtime.Sync
{
    /// <summary>
    /// Property-based tests for ReactiveProperty system.
    /// Tests verify correctness properties that must hold across all valid inputs.
    /// </summary>
    [TestFixture]
    public class ReactivePropertyPropertyTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            StradaArbitraries.RegisterAll();
        }

        /// <summary>
        /// Generator for integer values.
        /// </summary>
        private static Gen<int> IntValueGen => Gen.Choose(-10000, 10000);

        /// <summary>
        /// Generator for positive integer values (for subscriber counts).
        /// </summary>
        private static Gen<int> SubscriberCountGen => Gen.Choose(1, 20);

        /// <summary>
        /// Generator for distinct value pairs (old != new).
        /// </summary>
        private static Gen<(int oldValue, int newValue)> DistinctValuePairGen =>
            from oldVal in IntValueGen
            from delta in Gen.Choose(1, 1000)
            select (oldVal, oldVal + delta);

        /// <summary>
        /// Generator for string values.
        /// </summary>
        private static Gen<string> StringValueGen =>
            from length in Gen.Choose(1, 50)
            from chars in Gen.ArrayOf(length, Gen.Choose('a', 'z').Select(c => (char)c))
            select new string(chars);

        /// <summary>
        /// Generator for distinct string pairs.
        /// </summary>
        private static Gen<(string oldValue, string newValue)> DistinctStringPairGen =>
            from s1 in StringValueGen
            from s2 in StringValueGen
            where s1 != s2
            select (s1, s2);

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 14: ReactiveProperty Notification**
        /// For any ReactiveProperty value change (old != new),
        /// all subscribers SHALL be notified with the new value.
        /// **Validates: Requirements 5.1**
        /// </summary>
        [Test]
        public void ReactivePropertyNotification_AllSubscribersNotifiedOnChange()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                DistinctValuePairGen.ToArbitrary(),
                SubscriberCountGen.ToArbitrary(),
                (valuePair, subscriberCount) =>
                {
                    var reactiveProp = new ReactiveProperty<int>(valuePair.oldValue);
                    var receivedValues = new List<int>();
                    var notifyCounts = new int[subscriberCount];

                    for (int i = 0; i < subscriberCount; i++)
                    {
                        int index = i;
                        reactiveProp.Subscribe(v =>
                        {
                            receivedValues.Add(v);
                            notifyCounts[index]++;
                        });
                    }

                    reactiveProp.Value = valuePair.newValue;

                    if (receivedValues.Count != subscriberCount)
                        return false;

                    foreach (var value in receivedValues)
                    {
                        if (value != valuePair.newValue)
                            return false;
                    }

                    foreach (var count in notifyCounts)
                    {
                        if (count != 1)
                            return false;
                    }

                    return true;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 14: ReactiveProperty Notification**
        /// Additional test: String values also notify correctly.
        /// **Validates: Requirements 5.1**
        /// </summary>
        [Test]
        public void ReactivePropertyNotification_StringValues_AllSubscribersNotified()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                DistinctStringPairGen.ToArbitrary(),
                SubscriberCountGen.ToArbitrary(),
                (valuePair, subscriberCount) =>
                {
                    var reactiveProp = new ReactiveProperty<string>(valuePair.oldValue);
                    var receivedValues = new List<string>();

                    for (int i = 0; i < subscriberCount; i++)
                    {
                        reactiveProp.Subscribe(v => receivedValues.Add(v));
                    }

                    reactiveProp.Value = valuePair.newValue;

                    if (receivedValues.Count != subscriberCount)
                        return false;

                    foreach (var value in receivedValues)
                    {
                        if (value != valuePair.newValue)
                            return false;
                    }

                    return true;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 14: ReactiveProperty Notification**
        /// Additional test: Multiple value changes notify for each change.
        /// **Validates: Requirements 5.1**
        /// </summary>
        [Test]
        public void ReactivePropertyNotification_MultipleChanges_NotifiesEachTime()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                Gen.Choose(2, 10).ToArbitrary(),
                (changeCount) =>
                {
                    var reactiveProp = new ReactiveProperty<int>(0);
                    var receivedValues = new List<int>();

                    reactiveProp.Subscribe(v => receivedValues.Add(v));

                    for (int i = 1; i <= changeCount; i++)
                    {
                        reactiveProp.Value = i * 100;
                    }

                    if (receivedValues.Count != changeCount)
                        return false;

                    for (int i = 0; i < changeCount; i++)
                    {
                        if (receivedValues[i] != (i + 1) * 100)
                            return false;
                    }

                    return true;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 15: ReactiveProperty Idempotence**
        /// For any ReactiveProperty, setting the same value twice SHALL result
        /// in exactly one notification (on first set only).
        /// **Validates: Requirements 5.2**
        /// </summary>
        [Test]
        public void ReactivePropertyIdempotence_SameValueTwice_NotifiesOnce()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                IntValueGen.ToArbitrary(),
                IntValueGen.ToArbitrary(),
                (initialValue, newValue) =>
                {
                    if (initialValue == newValue)
                        return true;

                    var reactiveProp = new ReactiveProperty<int>(initialValue);
                    int notifyCount = 0;

                    reactiveProp.Subscribe(_ => notifyCount++);

                    reactiveProp.Value = newValue;
                    reactiveProp.Value = newValue;

                    return notifyCount == 1;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 15: ReactiveProperty Idempotence**
        /// Additional test: Setting initial value does not notify.
        /// **Validates: Requirements 5.2**
        /// </summary>
        [Test]
        public void ReactivePropertyIdempotence_SetSameAsInitial_NoNotification()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                IntValueGen.ToArbitrary(),
                (value) =>
                {
                    var reactiveProp = new ReactiveProperty<int>(value);
                    int notifyCount = 0;

                    reactiveProp.Subscribe(_ => notifyCount++);

                    reactiveProp.Value = value;

                    return notifyCount == 0;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 15: ReactiveProperty Idempotence**
        /// Additional test: Multiple identical sets still only notify once.
        /// **Validates: Requirements 5.2**
        /// </summary>
        [Test]
        public void ReactivePropertyIdempotence_MultipleIdenticalSets_NotifiesOnce()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                DistinctValuePairGen.ToArbitrary(),
                Gen.Choose(2, 10).ToArbitrary(),
                (valuePair, repeatCount) =>
                {
                    var reactiveProp = new ReactiveProperty<int>(valuePair.oldValue);
                    int notifyCount = 0;

                    reactiveProp.Subscribe(_ => notifyCount++);

                    for (int i = 0; i < repeatCount; i++)
                    {
                        reactiveProp.Value = valuePair.newValue;
                    }

                    return notifyCount == 1;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 15: ReactiveProperty Idempotence**
        /// Additional test: String idempotence works correctly.
        /// **Validates: Requirements 5.2**
        /// </summary>
        [Test]
        public void ReactivePropertyIdempotence_StringValues_NotifiesOnce()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                DistinctStringPairGen.ToArbitrary(),
                (valuePair) =>
                {
                    var reactiveProp = new ReactiveProperty<string>(valuePair.oldValue);
                    int notifyCount = 0;

                    reactiveProp.Subscribe(_ => notifyCount++);

                    reactiveProp.Value = valuePair.newValue;
                    reactiveProp.Value = valuePair.newValue;

                    return notifyCount == 1;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 16: SetWithoutNotify Silence**
        /// For any ReactiveProperty, calling SetWithoutNotify SHALL update Value
        /// without invoking any subscriber.
        /// **Validates: Requirements 5.3**
        /// </summary>
        [Test]
        public void SetWithoutNotifySilence_NoSubscribersInvoked()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                IntValueGen.ToArbitrary(),
                IntValueGen.ToArbitrary(),
                SubscriberCountGen.ToArbitrary(),
                (initialValue, newValue, subscriberCount) =>
                {
                    var reactiveProp = new ReactiveProperty<int>(initialValue);
                    int totalNotifyCount = 0;

                    for (int i = 0; i < subscriberCount; i++)
                    {
                        reactiveProp.Subscribe(_ => totalNotifyCount++);
                    }

                    reactiveProp.SetWithoutNotify(newValue);

                    return totalNotifyCount == 0 && reactiveProp.Value == newValue;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 16: SetWithoutNotify Silence**
        /// Additional test: SetWithoutNotify updates value correctly.
        /// **Validates: Requirements 5.3**
        /// </summary>
        [Test]
        public void SetWithoutNotifySilence_ValueIsUpdated()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                IntValueGen.ToArbitrary(),
                IntValueGen.ToArbitrary(),
                (initialValue, newValue) =>
                {
                    var reactiveProp = new ReactiveProperty<int>(initialValue);

                    reactiveProp.SetWithoutNotify(newValue);

                    return reactiveProp.Value == newValue;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 16: SetWithoutNotify Silence**
        /// Additional test: Multiple SetWithoutNotify calls remain silent.
        /// **Validates: Requirements 5.3**
        /// </summary>
        [Test]
        public void SetWithoutNotifySilence_MultipleCalls_AllSilent()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                Gen.Choose(2, 10).ToArbitrary(),
                (callCount) =>
                {
                    var reactiveProp = new ReactiveProperty<int>(0);
                    int notifyCount = 0;

                    reactiveProp.Subscribe(_ => notifyCount++);

                    for (int i = 1; i <= callCount; i++)
                    {
                        reactiveProp.SetWithoutNotify(i * 100);
                    }

                    return notifyCount == 0 && reactiveProp.Value == callCount * 100;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 16: SetWithoutNotify Silence**
        /// Additional test: SetWithoutNotify followed by Value set notifies correctly.
        /// **Validates: Requirements 5.3**
        /// </summary>
        [Test]
        public void SetWithoutNotifySilence_FollowedByValueSet_NotifiesOnce()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                IntValueGen.ToArbitrary(),
                IntValueGen.ToArbitrary(),
                IntValueGen.ToArbitrary(),
                (initial, silentValue, notifyValue) =>
                {
                    if (silentValue == notifyValue)
                        return true;

                    var reactiveProp = new ReactiveProperty<int>(initial);
                    int notifyCount = 0;
                    int lastNotifiedValue = 0;

                    reactiveProp.Subscribe(v =>
                    {
                        notifyCount++;
                        lastNotifiedValue = v;
                    });

                    reactiveProp.SetWithoutNotify(silentValue);
                    reactiveProp.Value = notifyValue;

                    return notifyCount == 1 && lastNotifiedValue == notifyValue;
                });

            property.Check(config);
        }

        /// <summary>
        /// **Feature: strada-codebase-audit, Property 16: SetWithoutNotify Silence**
        /// Additional test: String SetWithoutNotify is silent.
        /// **Validates: Requirements 5.3**
        /// </summary>
        [Test]
        public void SetWithoutNotifySilence_StringValues_NoNotification()
        {
            var config = PropertyTestConfig.CreateConfig();

            var property = Prop.ForAll(
                StringValueGen.ToArbitrary(),
                StringValueGen.ToArbitrary(),
                (initialValue, newValue) =>
                {
                    var reactiveProp = new ReactiveProperty<string>(initialValue);
                    int notifyCount = 0;

                    reactiveProp.Subscribe(_ => notifyCount++);

                    reactiveProp.SetWithoutNotify(newValue);

                    return notifyCount == 0 && reactiveProp.Value == newValue;
                });

            property.Check(config);
        }
    }
}
