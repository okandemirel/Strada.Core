namespace Strada.Core.DI
{
    internal interface IIndexResolver
    {
        object ResolveByIndex(int index);
    }
}