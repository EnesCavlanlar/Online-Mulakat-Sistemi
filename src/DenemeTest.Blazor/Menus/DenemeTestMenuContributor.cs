using DenemeTest.Localization;
using DenemeTest.MultiTenancy;
using DenemeTest.Blazor.Menus; // DenemeTestMenus için
using System.Threading.Tasks;
using Volo.Abp.Identity.Blazor;
using Volo.Abp.SettingManagement.Blazor.Menus;
using Volo.Abp.TenantManagement.Blazor.Navigation;
using Volo.Abp.UI.Navigation;

namespace DenemeTest.Blazor.Menus;

public class DenemeTestMenuContributor : IMenuContributor
{
    public async Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        if (context.Menu.Name == StandardMenus.Main)
        {
            await ConfigureMainMenuAsync(context);
        }
    }

    private Task ConfigureMainMenuAsync(MenuConfigurationContext context)
    {
        var l = context.GetLocalizer<DenemeTestResource>();

        // Home
        context.Menu.Items.Insert(
            0,
            new ApplicationMenuItem(
                DenemeTestMenus.Home,
                l["Menu:Home"],
                "/",
                icon: "fas fa-home",
                order: 1
            )
        );

        // ---- Admin kökü ve alt menüler ----
        var admin = new ApplicationMenuItem(DenemeTestMenus.Admin, "Admin", icon: "fas fa-tools", order: 2);

        admin.AddItem(new ApplicationMenuItem(DenemeTestMenus.Questions, "Sorular", url: "/admin/questions", icon: "fas fa-question"));
        admin.AddItem(new ApplicationMenuItem(DenemeTestMenus.Tests, "Testler", url: "/admin/tests", icon: "fas fa-list-check"));
        admin.AddItem(new ApplicationMenuItem(DenemeTestMenus.Candidates, "Adaylar", url: "/admin/candidates", icon: "fas fa-user"));
        admin.AddItem(new ApplicationMenuItem(DenemeTestMenus.Invitations, "Davet Gönder", url: "/admin/invitations", icon: "fas fa-envelope"));
        admin.AddItem(new ApplicationMenuItem(DenemeTestMenus.Reports, "Raporlar", url: "/admin/reports", icon: "fas fa-chart-line"));

        context.Menu.AddItem(admin);

        // ---- ABP Administration grubu ----
        var administration = context.Menu.GetAdministration();
        administration.Order = 6;

        if (MultiTenancyConsts.IsEnabled)
        {
            administration.SetSubItemOrder(TenantManagementMenuNames.GroupName, 1);
        }
        else
        {
            administration.TryRemoveMenuItem(TenantManagementMenuNames.GroupName);
        }

        administration.SetSubItemOrder(IdentityMenuNames.GroupName, 2);
        administration.SetSubItemOrder(SettingManagementMenus.GroupName, 3);

        return Task.CompletedTask;
    }
}
