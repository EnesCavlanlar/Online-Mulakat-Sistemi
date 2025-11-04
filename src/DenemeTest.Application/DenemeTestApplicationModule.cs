using DenemeTest.Application.Exams;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Account;
using Volo.Abp.AutoMapper;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.TenantManagement;

namespace DenemeTest;

[DependsOn(
    typeof(DenemeTestDomainModule),
    typeof(DenemeTestApplicationContractsModule),
    typeof(AbpPermissionManagementApplicationModule),
    typeof(AbpFeatureManagementApplicationModule),
    typeof(AbpIdentityApplicationModule),
    typeof(AbpAccountApplicationModule),
    typeof(AbpTenantManagementApplicationModule),
    typeof(AbpSettingManagementApplicationModule)
    )]
public class DenemeTestApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddTransient<IClassicScoringProvider, ClassicScoringStub>();
        context.Services.AddTransient<IClassicScoringProvider, ClassicScoringStub>();


        Configure<AbpAutoMapperOptions>(options =>
        {
            options.AddMaps<DenemeTestApplicationModule>();
        });
    }
}
