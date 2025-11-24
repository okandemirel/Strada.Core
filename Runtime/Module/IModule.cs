using System.Collections.Generic;
using Strada.Core.DI;
using Strada.Core.ECS;

namespace Strada.Core.Module
{
    public interface IModule
    {
        void RegisterServices(IContainerBuilder builder);
        void RegisterSystems(EntityManager entityManager);
        void Initialize(IContainer container);
        void Shutdown();
        List<IStradaSystem> GetSystems();
    }
}
