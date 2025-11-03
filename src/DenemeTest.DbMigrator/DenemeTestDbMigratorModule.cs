using DenemeTest.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace DenemeTest.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(DenemeTestEntityFrameworkCoreModule),
    typeof(DenemeTestApplicationContractsModule)
)]
public class DenemeTestDbMigratorModule : AbpModule
{
}
