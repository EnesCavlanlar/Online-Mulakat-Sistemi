using Volo.Abp.Modularity;

namespace DenemeTest;

/* Inherit from this class for your domain layer tests. */
public abstract class DenemeTestDomainTestBase<TStartupModule> : DenemeTestTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
