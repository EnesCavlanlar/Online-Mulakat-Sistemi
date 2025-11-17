using System.Linq;
using System.Threading.Tasks;
using DenemeTest.Localization;
using DenemeTest.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
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

    private async Task ConfigureMainMenuAsync(MenuConfigurationContext context)
    {
        var l = context.GetLocalizer<DenemeTestResource>();
        var auth = context.ServiceProvider.GetRequiredService<IAuthorizationService>();

        // ==========================
        //  HOME
        // ==========================
        context.Menu.Items.Insert(
            0,
            new ApplicationMenuItem(
                DenemeTestMenus.Home,
                l["Menu:Home"],
                "/",
                icon: "fa fa-home"
            )
        );

        // ==========================
        //  EXAM ADMIN MENÜSÜ
        // ==========================
        var adminGroup = new ApplicationMenuItem(
            "Admin",
            l["Menu:Admin"],
            icon: "fa fa-tools"
        );

        // Sorular
        if (await auth.IsGrantedAsync(DenemeTestPermissions.Exams.Questions))
        {
            adminGroup.AddItem(
                new ApplicationMenuItem(
                    "Admin.Questions",
                    "Sorular",
                    url: "/admin/questions"
                )
            );
        }

        // Testler
        if (await auth.IsGrantedAsync(DenemeTestPermissions.Exams.Tests))
        {
            adminGroup.AddItem(
                new ApplicationMenuItem(
                    "Admin.Tests",
                    "Testler",
                    url: "/admin/tests"
                )
            );
        }

        // Adaylar
        if (await auth.IsGrantedAsync(DenemeTestPermissions.Exams.Candidates))
        {
            adminGroup.AddItem(
                new ApplicationMenuItem(
                    "Admin.Candidates",
                    "Adaylar",
                    url: "/admin/candidates"
                )
            );
        }

        // Davet Gönder
        if (await auth.IsGrantedAsync(DenemeTestPermissions.Exams.Invitations))
        {
            adminGroup.AddItem(
                new ApplicationMenuItem(
                    "Admin.Invitations",
                    "Davet Gönder",
                    url: "/admin/invitations"
                )
            );
        }

        // Raporlar
        if (await auth.IsGrantedAsync(DenemeTestPermissions.Exams.Reports))
        {
            adminGroup.AddItem(
                new ApplicationMenuItem(
                    "Admin.Reports",
                    "Raporlar",
                    url: "/admin/reports"
                )
            );
        }

        // Eğer kullanıcının en az bir Exams izni varsa Admin grubu menüye eklensin
        if (adminGroup.Items.Any())
        {
            context.Menu.AddItem(adminGroup);
        }

        // Not: ABP'nin kendi "Administration" grubu (Identity, Tenant, Settings)
        // başka modüller tarafından ekleniyor; burada özel bir sıralama yapmıyoruz.
    }
}
