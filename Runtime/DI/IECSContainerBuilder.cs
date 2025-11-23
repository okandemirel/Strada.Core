using System;
using Strada.Core.ECS;

namespace Strada.Core.DI
{
    public interface IECSContainerBuilder : IContainerBuilder
    {
        IECSContainerBuilder RegisterWorld(string worldName);

        IECSContainerBuilder RegisterSystem<TSystem>(string worldName) where TSystem : IStradaSystem;

        IECSContainerBuilder RegisterSystem(Type systemType, string worldName);
    }
}
