using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Strada.Core.StateMachine
{
    public abstract class StateMachineCore<TState> where TState : class, IState
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
        public void AddState<T>(T state) where T : TState
        {
            OnStateAdded(state);
            States[typeof(T)] = state;
        }

        protected virtual void OnStateAdded(TState state) { }

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
            if (!States.TryGetValue(stateType, out var newState))
            {
                Debug.LogWarning($"Attempted transition to unregistered state: {stateType}");
                return;
            }

            IsTransitioningInternal = true;
            var previousState = CurrentStateInternal;

            try
            {
                previousState?.OnExit();
                CurrentStateInternal = newState;
                CurrentStateTypeInternal = stateType;
                CurrentStateInternal.OnEnter();
                OnStateChanged?.Invoke(previousState, CurrentStateInternal);
            }
            finally
            {
                IsTransitioningInternal = false;
            }
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

    public sealed class StateMachine<TState> : StateMachineCore<TState> where TState : class, IState
    {
    }

    public sealed class StateMachine<TState, TContext> : StateMachineCore<TState> where TState : class, IState<TContext>
    {
        private readonly TContext _context;

        public TContext Context => _context;

        public StateMachine(TContext context)
        {
            _context = context;
        }

        protected override void OnStateAdded(TState state)
        {
            state.SetContext(_context);
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
