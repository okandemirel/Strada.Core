using Strada.Core.DI;
using Strada.Core.ECS;

namespace Strada.Core.Modules
{
    public interface IECSModuleInstaller : IModuleInstaller
    {
        void InstallECS(IECSContainerBuilder builder);
    }
}
