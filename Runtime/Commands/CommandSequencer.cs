using System;
using System.Collections.Generic;

namespace Strada.Core.Commands
{
    public interface ICommandSequence
    {
        ICommandSequence Then(ICommand command);
        ICommandSequence Then(IAsyncCommand command);
        ICommandSequence Then(Action action);
        ICommandSequence Parallel(params ICommand[] commands);
        void Execute(Action onComplete = null);
        void Cancel();
    }

    public sealed class CommandSequencer : ICommandSequence
    {
        private readonly List<ISequenceStep> _steps = new(8);
        private int _currentStep;
        private bool _executing;
        private bool _cancelled;
        private Action _onComplete;

        public static ICommandSequence Create() => new CommandSequencer();

        public ICommandSequence Then(ICommand command)
        {
            _steps.Add(new SyncCommandStep(command));
            return this;
        }

        public ICommandSequence Then(IAsyncCommand command)
        {
            _steps.Add(new AsyncCommandStep(command));
            return this;
        }

        public ICommandSequence Then(Action action)
        {
            _steps.Add(new ActionStep(action));
            return this;
        }

        public ICommandSequence Parallel(params ICommand[] commands)
        {
            _steps.Add(new ParallelStep(commands));
            return this;
        }

        public void Execute(Action onComplete = null)
        {
            if (_executing) return;

            _executing = true;
            _cancelled = false;
            _currentStep = 0;
            _onComplete = onComplete;

            ExecuteNextStep();
        }

        public void Cancel()
        {
            _cancelled = true;

            if (_currentStep < _steps.Count)
                _steps[_currentStep].Cancel();

            Reset();
        }

        private void ExecuteNextStep()
        {
            if (_cancelled || _currentStep >= _steps.Count)
            {
                Complete();
                return;
            }

            _steps[_currentStep].Execute(OnStepComplete);
        }

        private void OnStepComplete()
        {
            if (_cancelled) return;

            _currentStep++;
            ExecuteNextStep();
        }

        private void Complete()
        {
            _executing = false;
            _onComplete?.Invoke();
            _onComplete = null;
        }

        private void Reset()
        {
            _executing = false;
            _currentStep = 0;
            _onComplete = null;
        }

        private interface ISequenceStep
        {
            void Execute(Action onComplete);
            void Cancel();
        }

        private sealed class SyncCommandStep : ISequenceStep
        {
            private readonly ICommand _command;

            public SyncCommandStep(ICommand command) => _command = command;

            public void Execute(Action onComplete)
            {
                _command.Execute();
                onComplete();
            }

            public void Cancel() { }
        }

        private sealed class AsyncCommandStep : ISequenceStep
        {
            private readonly IAsyncCommand _command;

            public AsyncCommandStep(IAsyncCommand command) => _command = command;

            public void Execute(Action onComplete) => _command.Execute(onComplete);

            public void Cancel() => _command.Cancel();
        }

        private sealed class ActionStep : ISequenceStep
        {
            private readonly Action _action;

            public ActionStep(Action action) => _action = action;

            public void Execute(Action onComplete)
            {
                _action();
                onComplete();
            }

            public void Cancel() { }
        }

        private sealed class ParallelStep : ISequenceStep
        {
            private readonly ICommand[] _commands;

            public ParallelStep(ICommand[] commands) => _commands = commands;

            public void Execute(Action onComplete)
            {
                for (int i = 0; i < _commands.Length; i++)
                    _commands[i].Execute();

                onComplete();
            }

            public void Cancel() { }
        }
    }
}
