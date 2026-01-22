using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Strada.Core.StateMachine
{
    /// <summary>
    /// Base class for state machines. Contains all shared logic.
    /// </summary>
    public abstract class StateMachineBase<TState> where TState : class, IState
    {
        protected readonly Dictionary<Type, TState> States = new(8);
        protected readonly Dictionary<Type, List<Transition<TState>>> Transitions = new(8);
        protected readonly List<Transition<TState>> AnyTransitions = new(4);
        protected TState CurrentStateInternal;
        protected Type CurrentStateTypeInternal;
        protected bool IsTransitioningInternal;

        public TState CurrentState => CurrentStateInternal;
        public Type CurrentStateType => CurrentStateTypeInternal;
        public bool IsRunning => CurrentStateInternal != null;

        public event Action<TState, TState> OnStateChanged;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddTransition<TFrom, TTo>(Func<bool> condition) where TFrom : TState where TTo : TState
        {
            var fromType = typeof(TFrom);
            if (!Transitions.TryGetValue(fromType, out var list))
            {
                list = new List<Transition<TState>>(4);
                Transitions[fromType] = list;
            }
            list.Add(new Transition<TState>(typeof(TTo), condition));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddAnyTransition<TTo>(Func<bool> condition) where TTo : TState
        {
            AnyTransitions.Add(new Transition<TState>(typeof(TTo), condition));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Start<T>() where T : TState
        {
            if (CurrentStateInternal != null) return;
            SetState(typeof(T));
        }

        public void Update(float deltaTime)
        {
            if (CurrentStateInternal == null || IsTransitioningInternal) return;

            CheckTransitions();
            CurrentStateInternal.OnUpdate(deltaTime);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetState<T>() where T : TState
        {
            SetState(typeof(T));
        }

        public void Stop()
        {
            if (CurrentStateInternal == null) return;
            CurrentStateInternal.OnExit();
            CurrentStateInternal = null;
            CurrentStateTypeInternal = null;
        }

        protected void SetState(Type stateType)
        {
            if (stateType == CurrentStateTypeInternal) return;
            if (!States.TryGetValue(stateType, out var newState)) return;

            IsTransitioningInternal = true;

            var previousState = CurrentStateInternal;
            CurrentStateInternal?.OnExit();

            CurrentStateInternal = newState;
            CurrentStateTypeInternal = stateType;
            CurrentStateInternal.OnEnter();

            IsTransitioningInternal = false;

            OnStateChanged?.Invoke(previousState, CurrentStateInternal);
        }

        private void CheckTransitions()
        {
            foreach (var transition in AnyTransitions)
            {
                if (transition.ToType != CurrentStateTypeInternal && transition.Condition())
                {
                    SetState(transition.ToType);
                    return;
                }
            }

            if (CurrentStateTypeInternal != null && Transitions.TryGetValue(CurrentStateTypeInternal, out var stateTransitions))
            {
                foreach (var transition in stateTransitions)
                {
                    if (transition.Condition())
                    {
                        SetState(transition.ToType);
                        return;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Simple state machine without context.
    /// </summary>
    public sealed class StateMachine<TState> : StateMachineBase<TState> where TState : class, IState
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddState<T>(T state) where T : TState
        {
            States[typeof(T)] = state;
        }
    }

    /// <summary>
    /// State machine with context that is shared between states.
    /// </summary>
    public sealed class StateMachine<TState, TContext> : StateMachineBase<TState> where TState : class, IState<TContext>
    {
        private readonly TContext _context;

        public TContext Context => _context;

        public StateMachine(TContext context)
        {
            _context = context;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddState<T>(T state) where T : TState
        {
            state.SetContext(_context);
            States[typeof(T)] = state;
        }
    }

    public readonly struct Transition<TState> where TState : class, IState
    {
        public readonly Type ToType;
        public readonly Func<bool> Condition;

        public Transition(Type toType, Func<bool> condition)
        {
            ToType = toType;
            Condition = condition;
        }
    }
}
