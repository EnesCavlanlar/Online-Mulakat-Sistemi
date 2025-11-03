using Volo.Abp.Modularity;

namespace DenemeTest;

[DependsOn(
    typeof(DenemeTestDomainModule),
    typeof(DenemeTestTestBaseModule)
)]
public class DenemeTestDomainTestModule : AbpModule
{

}
