using NUnit.Framework;
using Strada.Core.StateMachine;

namespace Strada.Core.Tests.Runtime.StateMachine
{
    [TestFixture]
    public sealed class StateMachineTests
    {
        private sealed class IdleState : StateBase
        {
            public int EnterCount;
            public int UpdateCount;
            public int ExitCount;

            public override void OnEnter() => EnterCount++;
            public override void OnUpdate(float deltaTime) => UpdateCount++;
            public override void OnExit() => ExitCount++;
        }

        private sealed class RunningState : StateBase
        {
            public int EnterCount;
            public int UpdateCount;
            public int ExitCount;

            public override void OnEnter() => EnterCount++;
            public override void OnUpdate(float deltaTime) => UpdateCount++;
            public override void OnExit() => ExitCount++;
        }

        private sealed class JumpingState : StateBase
        {
            public override void OnEnter() { }
            public override void OnUpdate(float deltaTime) { }
            public override void OnExit() { }
        }

        [Test]
        public void Start_EntersInitialState()
        {
            var sm = new StateMachine<StateBase>();
            var idle = new IdleState();
            sm.AddState(idle);

            sm.Start<IdleState>();

            Assert.AreEqual(1, idle.EnterCount);
            Assert.AreSame(idle, sm.CurrentState);
        }

        [Test]
        public void Update_CallsCurrentStateUpdate()
        {
            var sm = new StateMachine<StateBase>();
            var idle = new IdleState();
            sm.AddState(idle);
            sm.Start<IdleState>();

            sm.Update(0.1f);
            sm.Update(0.1f);
            sm.Update(0.1f);

            Assert.AreEqual(3, idle.UpdateCount);
        }

        [Test]
        public void SetState_TransitionsCorrectly()
        {
            var sm = new StateMachine<StateBase>();
            var idle = new IdleState();
            var running = new RunningState();
            sm.AddState(idle);
            sm.AddState(running);
            sm.Start<IdleState>();

            sm.SetState<RunningState>();

            Assert.AreEqual(1, idle.ExitCount);
            Assert.AreEqual(1, running.EnterCount);
            Assert.AreSame(running, sm.CurrentState);
        }

        [Test]
        public void AutoTransition_TriggersWhenConditionMet()
        {
            var sm = new StateMachine<StateBase>();
            var idle = new IdleState();
            var running = new RunningState();
            var shouldRun = false;

            sm.AddState(idle);
            sm.AddState(running);
            sm.AddTransition<IdleState, RunningState>(() => shouldRun);
            sm.Start<IdleState>();

            sm.Update(0.1f);
            Assert.AreSame(idle, sm.CurrentState);

            shouldRun = true;
            sm.Update(0.1f);
            Assert.AreSame(running, sm.CurrentState);
        }

        [Test]
        public void AnyTransition_WorksFromAnyState()
        {
            var sm = new StateMachine<StateBase>();
            var idle = new IdleState();
            var running = new RunningState();
            var jumping = new JumpingState();
            var shouldJump = false;

            sm.AddState(idle);
            sm.AddState(running);
            sm.AddState(jumping);
            sm.AddAnyTransition<JumpingState>(() => shouldJump);
            sm.Start<IdleState>();

            shouldJump = true;
            sm.Update(0.1f);
            Assert.IsInstanceOf<JumpingState>(sm.CurrentState);
        }

        [Test]
        public void OnStateChanged_FiresEvent()
        {
            var sm = new StateMachine<StateBase>();
            var idle = new IdleState();
            var running = new RunningState();
            StateBase previous = null;
            StateBase current = null;

            sm.AddState(idle);
            sm.AddState(running);
            sm.OnStateChanged += (p, c) =>
            {
                previous = p;
                current = c;
            };
            sm.Start<IdleState>();
            sm.SetState<RunningState>();

            Assert.AreSame(idle, previous);
            Assert.AreSame(running, current);
        }

        [Test]
        public void Stop_ExitsCurrentState()
        {
            var sm = new StateMachine<StateBase>();
            var idle = new IdleState();
            sm.AddState(idle);
            sm.Start<IdleState>();

            sm.Stop();

            Assert.AreEqual(1, idle.ExitCount);
            Assert.IsNull(sm.CurrentState);
            Assert.IsFalse(sm.IsRunning);
        }
    }
}
