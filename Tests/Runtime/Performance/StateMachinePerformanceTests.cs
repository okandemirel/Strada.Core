using NUnit.Framework;
using Strada.Core.StateMachine;
using Unity.PerformanceTesting;

namespace Strada.Core.Tests.Tests.Runtime.Performance
{
    [TestFixture]
    [Category("Performance")]
    public sealed class StateMachinePerformanceTests
    {
        private sealed class TestState : StateBase
        {
            public int UpdateCount;
            public override void OnUpdate(float deltaTime) => UpdateCount++;
        }

        [Test, Performance]
        public void Benchmark_StateMachine_Update_10k()
        {
            var sm = new StateMachine<StateBase>();
            sm.AddState(new TestState());
            sm.Start<TestState>();

            Measure.Method(() =>
            {
                for (var i = 0; i < 10000; i++)
                {
                    sm.Update(0.016f);
                }
            })
            .WarmupCount(3)
            .MeasurementCount(10)
            .Run();
        }

        [Test, Performance]
        public void Benchmark_StateMachine_TransitionCheck_10k()
        {
            var sm = new StateMachine<StateBase>();
            var state1 = new TestState();
            var state2 = new TestState();

            sm.AddState(state1);
            sm.AddState(state2);
            sm.AddTransition<TestState, TestState>(() => false);
            sm.AddTransition<TestState, TestState>(() => false);
            sm.AddTransition<TestState, TestState>(() => false);
            sm.AddTransition<TestState, TestState>(() => false);
            sm.AddTransition<TestState, TestState>(() => false);
            sm.Start<TestState>();

            Measure.Method(() =>
            {
                for (var i = 0; i < 10000; i++)
                {
                    sm.Update(0.016f);
                }
            })
            .WarmupCount(3)
            .MeasurementCount(10)
            .Run();
        }

        [Test, Performance]
        public void Benchmark_StateTransitions_1k()
        {
            var sm = new StateMachine<StateBase>();
            var state1 = new TestState();
            var state2 = new TestState();
            var toggle = false;

            sm.AddState(state1);
            sm.AddState(state2);
            sm.AddTransition<TestState, TestState>(() => toggle);
            sm.Start<TestState>();

            Measure.Method(() =>
            {
                for (var i = 0; i < 1000; i++)
                {
                    toggle = true;
                    sm.Update(0.016f);
                    toggle = false;
                    sm.Update(0.016f);
                }
            })
            .WarmupCount(3)
            .MeasurementCount(10)
            .Run();
        }
    }
}
