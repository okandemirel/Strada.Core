using System;
using System.Threading;

namespace Strada.Core.Commands
{
    public interface ICommand
    {
        void Execute();
    }

    public interface ICommand<in T>
    {
        void Execute(T parameter);
    }

    public interface IAsyncCommand
    {
        void Execute(Action onComplete);
        void Cancel();
    }

    public interface IAsyncCommand<in T>
    {
        void Execute(T parameter, Action onComplete);
        void Cancel();
    }

    public interface ICommandHandler<in TCommand> where TCommand : struct
    {
        void Handle(TCommand command);
    }

    public interface IAsyncCommandHandler<in TCommand> where TCommand : struct
    {
        void Handle(TCommand command, Action onComplete);
        void Cancel();
    }
}
