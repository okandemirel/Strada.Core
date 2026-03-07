using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Strada.Core.StateMachine
{
    public sealed class StateMachine<TState> where TState : class, IState
    {
        private readonly Dictionary<Type, TState> _states = new(8);
        private readonly Dictionary<Type, List<Transition<TState>>> _transitions = new(8);
        private readonly List<Transition<TState>> _anyTransitions = new(4);
        private TState _currentState;
        private Type _currentStateType;
        private bool _isTransitioning;

        public TState CurrentState => _currentState;
        public Type CurrentStateType => _currentStateType;
        public bool IsRunning => _currentState != null;

        public event Action<TState, TState> OnStateChanged;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddState<T>(T state) where T : TState
        {
            _states[typeof(T)] = state;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddTransition<TFrom, TTo>(Func<bool> condition) where TFrom : TState where TTo : TState
        {
            var fromType = typeof(TFrom);
            if (!_transitions.TryGetValue(fromType, out var list))
            {
                list = new List<Transition<TState>>(4);
                _transitions[fromType] = list;
            }
            list.Add(new Transition<TState>(typeof(TTo), condition));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddAnyTransition<TTo>(Func<bool> condition) where TTo : TState
        {
            _anyTransitions.Add(new Transition<TState>(typeof(TTo), condition));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Start<T>() where T : TState
        {
            if (_currentState != null) return;
            SetState(typeof(T));
        }

        public void Update(float deltaTime)
        {
            if (_currentState == null || _isTransitioning) return;

            CheckTransitions();
            _currentState.OnUpdate(deltaTime);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetState<T>() where T : TState
        {
            SetState(typeof(T));
        }

        public void Stop()
        {
            if (_currentState == null) return;
            _currentState.OnExit();
            _currentState = null;
            _currentStateType = null;
        }

        private void SetState(Type stateType)
        {
            if (stateType == _currentStateType) return;
            if (!_states.TryGetValue(stateType, out var newState))
            {
                Debug.LogWarning($"Attempted transition to unregistered state: {stateType}");
                return;
            }

            _isTransitioning = true;

            var previousState = _currentState;
            _currentState?.OnExit();

            _currentState = newState;
            _currentStateType = stateType;
            _currentState.OnEnter();

            OnStateChanged?.Invoke(previousState, _currentState);

            _isTransitioning = false;
        }

        private void CheckTransitions()
        {
            foreach (var transition in _anyTransitions)
            {
                if (transition.ToType != _currentStateType && transition.Condition())
                {
                    SetState(transition.ToType);
                    return;
                }
            }

            if (_currentStateType != null && _transitions.TryGetValue(_currentStateType, out var stateTransitions))
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

    public sealed class StateMachine<TState, TContext> where TState : class, IState<TContext>
    {
        private readonly Dictionary<Type, TState> _states = new(8);
        private readonly Dictionary<Type, List<Transition<TState>>> _transitions = new(8);
        private readonly List<Transition<TState>> _anyTransitions = new(4);
        private readonly TContext _context;
        private TState _currentState;
        private Type _currentStateType;
        private bool _isTransitioning;

        public TState CurrentState => _currentState;
        public Type CurrentStateType => _currentStateType;
        public TContext Context => _context;
        public bool IsRunning => _currentState != null;

        public event Action<TState, TState> OnStateChanged;

        public StateMachine(TContext context)
        {
            _context = context;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddState<T>(T state) where T : TState
        {
            state.SetContext(_context);
            _states[typeof(T)] = state;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddTransition<TFrom, TTo>(Func<bool> condition) where TFrom : TState where TTo : TState
        {
            var fromType = typeof(TFrom);
            if (!_transitions.TryGetValue(fromType, out var list))
            {
                list = new List<Transition<TState>>(4);
                _transitions[fromType] = list;
            }
            list.Add(new Transition<TState>(typeof(TTo), condition));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddAnyTransition<TTo>(Func<bool> condition) where TTo : TState
        {
            _anyTransitions.Add(new Transition<TState>(typeof(TTo), condition));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Start<T>() where T : TState
        {
            if (_currentState != null) return;
            SetState(typeof(T));
        }

        public void Update(float deltaTime)
        {
            if (_currentState == null || _isTransitioning) return;

            CheckTransitions();
            _currentState.OnUpdate(deltaTime);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetState<T>() where T : TState
        {
            SetState(typeof(T));
        }

        public void Stop()
        {
            if (_currentState == null) return;
            _currentState.OnExit();
            _currentState = null;
            _currentStateType = null;
        }

        private void SetState(Type stateType)
        {
            if (stateType == _currentStateType) return;
            if (!_states.TryGetValue(stateType, out var newState))
            {
                Debug.LogWarning($"Attempted transition to unregistered state: {stateType}");
                return;
            }

            _isTransitioning = true;

            var previousState = _currentState;
            _currentState?.OnExit();

            _currentState = newState;
            _currentStateType = stateType;
            _currentState.OnEnter();

            OnStateChanged?.Invoke(previousState, _currentState);

            _isTransitioning = false;
        }

        private void CheckTransitions()
        {
            foreach (var transition in _anyTransitions)
            {
                if (transition.ToType != _currentStateType && transition.Condition())
                {
                    SetState(transition.ToType);
                    return;
                }
            }

            if (_currentStateType != null && _transitions.TryGetValue(_currentStateType, out var stateTransitions))
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

    internal readonly struct Transition<TState> where TState : class, IState
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
