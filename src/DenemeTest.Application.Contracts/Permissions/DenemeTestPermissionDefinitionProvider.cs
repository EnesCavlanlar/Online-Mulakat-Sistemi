using DenemeTest.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;

namespace DenemeTest.Permissions;

public class DenemeTestPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        // Grup
        var myGroup = context.AddGroup(
            DenemeTestPermissions.GroupName,
            L("Permission:DenemeTest")
        );

        // Exams ana permission
        var exams = myGroup.AddPermission(
            DenemeTestPermissions.Exams.Default,
            L("Permission:Exams")
        );

        exams.AddChild(
            DenemeTestPermissions.Exams.Reports,
            L("Permission:Exams.Reports")
        );

        exams.AddChild(
            DenemeTestPermissions.Exams.Questions,
            L("Permission:Exams.Questions")
        );

        exams.AddChild(
            DenemeTestPermissions.Exams.Candidates,
            L("Permission:Exams.Candidates")
        );

        exams.AddChild(
            DenemeTestPermissions.Exams.Invitations,
            L("Permission:Exams.Invitations")
        );

        exams.AddChild(
            DenemeTestPermissions.Exams.Tests,
            L("Permission:Exams.Tests")
        );
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<DenemeTestResource>(name);
    }
}
