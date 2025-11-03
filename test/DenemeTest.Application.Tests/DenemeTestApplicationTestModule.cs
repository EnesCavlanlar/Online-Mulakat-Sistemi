using Volo.Abp.Modularity;

namespace DenemeTest;

[DependsOn(
    typeof(DenemeTestApplicationModule),
    typeof(DenemeTestDomainTestModule)
)]
public class DenemeTestApplicationTestModule : AbpModule
{

}
