using System;
using System.Collections.Generic;
using FsCheck;
using NUnit.Framework;
using Strada.Core.Bridge;
using Strada.Core.Tests.Generators;

namespace Strada.Core.Tests.Runtime.Bridge
{
    /// <summary>
    /// Property-based tests for ReactiveProperty system.
    /// Tests verify correctness properties that must hold across all valid inputs.
    /// </summary>
    [TestFixture]
    public class ReactivePropertyPBT
    {
        [OneTimeSetUp]
        public void Setup()
        {
            StradaArbitraries.RegisterAll();
        }

        #region Generators

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

        #endregion

        #region Property 14: ReactiveProperty Notification

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
                    // Arrange
                    var reactiveProp = new ReactiveProperty<int>(valuePair.oldValue);
                    var receivedValues = new List<int>();
                    var notifyCounts = new int[subscriberCount];

                    // Subscribe N handlers
                    for (int i = 0; i < subscriberCount; i++)
                    {
                        int index = i;
                        reactiveProp.Subscribe(v =>
                        {
                            receivedValues.Add(v);
                            notifyCounts[index]++;
                        });
                    }

                    // Act - change value
                    reactiveProp.Value = valuePair.newValue;

                    // Assert - all subscribers received exactly once with new value
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
                    // Arrange
                    var reactiveProp = new ReactiveProperty<string>(valuePair.oldValue);
                    var receivedValues = new List<string>();

                    for (int i = 0; i < subscriberCount; i++)
                    {
                        reactiveProp.Subscribe(v => receivedValues.Add(v));
                    }

                    // Act
                    reactiveProp.Value = valuePair.newValue;

                    // Assert
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
                    // Arrange
                    var reactiveProp = new ReactiveProperty<int>(0);
                    var receivedValues = new List<int>();

                    reactiveProp.Subscribe(v => receivedValues.Add(v));

                    // Act - make multiple distinct changes
                    for (int i = 1; i <= changeCount; i++)
                    {
                        reactiveProp.Value = i * 100; // Ensure distinct values
                    }

                    // Assert - received notification for each change
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

        #endregion

        #region Property 15: ReactiveProperty Idempotence

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
                    // Skip if initial == new (no notification expected at all)
                    if (initialValue == newValue)
                        return true;

                    // Arrange
                    var reactiveProp = new ReactiveProperty<int>(initialValue);
                    int notifyCount = 0;

                    reactiveProp.Subscribe(_ => notifyCount++);

                    // Act - set new value twice
                    reactiveProp.Value = newValue;
                    reactiveProp.Value = newValue;

                    // Assert - only one notification
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
                    // Arrange
                    var reactiveProp = new ReactiveProperty<int>(value);
                    int notifyCount = 0;

                    reactiveProp.Subscribe(_ => notifyCount++);

                    // Act - set same value as initial
                    reactiveProp.Value = value;

                    // Assert - no notification
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
                    // Arrange
                    var reactiveProp = new ReactiveProperty<int>(valuePair.oldValue);
                    int notifyCount = 0;

                    reactiveProp.Subscribe(_ => notifyCount++);

                    // Act - set new value multiple times
                    for (int i = 0; i < repeatCount; i++)
                    {
                        reactiveProp.Value = valuePair.newValue;
                    }

                    // Assert - only one notification
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
                    // Arrange
                    var reactiveProp = new ReactiveProperty<string>(valuePair.oldValue);
                    int notifyCount = 0;

                    reactiveProp.Subscribe(_ => notifyCount++);

                    // Act - set new value twice
                    reactiveProp.Value = valuePair.newValue;
                    reactiveProp.Value = valuePair.newValue;

                    // Assert - only one notification
                    return notifyCount == 1;
                });

            property.Check(config);
        }

        #endregion

        #region Property 16: SetWithoutNotify Silence

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
                    // Arrange
                    var reactiveProp = new ReactiveProperty<int>(initialValue);
                    int totalNotifyCount = 0;

                    for (int i = 0; i < subscriberCount; i++)
                    {
                        reactiveProp.Subscribe(_ => totalNotifyCount++);
                    }

                    // Act
                    reactiveProp.SetWithoutNotify(newValue);

                    // Assert - no notifications and value updated
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
                    // Arrange
                    var reactiveProp = new ReactiveProperty<int>(initialValue);

                    // Act
                    reactiveProp.SetWithoutNotify(newValue);

                    // Assert - value is updated
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
                    // Arrange
                    var reactiveProp = new ReactiveProperty<int>(0);
                    int notifyCount = 0;

                    reactiveProp.Subscribe(_ => notifyCount++);

                    // Act - multiple SetWithoutNotify calls
                    for (int i = 1; i <= callCount; i++)
                    {
                        reactiveProp.SetWithoutNotify(i * 100);
                    }

                    // Assert - no notifications, final value correct
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
                    // Skip if silentValue == notifyValue (no notification expected)
                    if (silentValue == notifyValue)
                        return true;

                    // Arrange
                    var reactiveProp = new ReactiveProperty<int>(initial);
                    int notifyCount = 0;
                    int lastNotifiedValue = 0;

                    reactiveProp.Subscribe(v =>
                    {
                        notifyCount++;
                        lastNotifiedValue = v;
                    });

                    // Act
                    reactiveProp.SetWithoutNotify(silentValue);
                    reactiveProp.Value = notifyValue;

                    // Assert - only one notification with notifyValue
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
                    // Arrange
                    var reactiveProp = new ReactiveProperty<string>(initialValue);
                    int notifyCount = 0;

                    reactiveProp.Subscribe(_ => notifyCount++);

                    // Act
                    reactiveProp.SetWithoutNotify(newValue);

                    // Assert
                    return notifyCount == 0 && reactiveProp.Value == newValue;
                });

            property.Check(config);
        }

        #endregion
    }
}
