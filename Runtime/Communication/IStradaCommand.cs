using Strada.Core.ECS;

namespace Strada.Core.Communication
{
    public interface IStradaCommand
    {
        void Execute(EntityManager entityManager);
    }
}
