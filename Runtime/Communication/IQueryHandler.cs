namespace Strada.Core.Communication
{
    public interface IQueryHandler<TQuery, TResult> where TQuery : struct, IQuery<TResult>
    {
        TResult Handle(ref TQuery query);
    }
}
