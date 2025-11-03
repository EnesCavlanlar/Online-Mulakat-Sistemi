using Volo.Abp.Modularity;

namespace DenemeTest;

public abstract class DenemeTestApplicationTestBase<TStartupModule> : DenemeTestTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
