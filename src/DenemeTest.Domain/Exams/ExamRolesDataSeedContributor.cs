using System;
using System.Threading.Tasks;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;

namespace DenemeTest.Exams
{
    /// <summary>
    /// Sınav modülü için özel rollerin (özellikle exam-analyst) oluşturulması ve izinlerinin atanması.
    /// </summary>
    public class ExamRolesDataSeedContributor : IDataSeedContributor, ITransientDependency
    {
        private readonly IIdentityRoleRepository _roleRepository;
        private readonly IPermissionDataSeeder _permissionDataSeeder;

        public ExamRolesDataSeedContributor(
            IIdentityRoleRepository roleRepository,
            IPermissionDataSeeder permissionDataSeeder)
        {
            _roleRepository = roleRepository;
            _permissionDataSeeder = permissionDataSeeder;
        }

        public async Task SeedAsync(DataSeedContext context)
        {
            const string analystRoleName = "exam-analyst";

            // Rol zaten var mı?
            var existing = await _roleRepository.FindByNormalizedNameAsync(
                analystRoleName.ToUpperInvariant()
            );

            if (existing == null)
            {
                var role = new IdentityRole(
                    Guid.NewGuid(),          // rol Id
                    analystRoleName,         // rol sistem adı
                    context.TenantId         // tenant (host ise null)
                )
                {
                    IsDefault = false,
                    IsPublic = false
                };

                await _roleRepository.InsertAsync(role, autoSave: true);
                existing = role;
            }

            // Bu role verilecek izinler
            var permissions = new[]
            {
                "DenemeTest.Exams",
                "DenemeTest.Exams.Questions",
                "DenemeTest.Exams.Tests",
                "DenemeTest.Exams.Candidates",
                "DenemeTest.Exams.Invitations",
                "DenemeTest.Exams.Reports"
            };

            await _permissionDataSeeder.SeedAsync(
                RolePermissionValueProvider.ProviderName,
                existing.Name,   // providerKey: rol adı
                permissions,
                context.TenantId
            );
        }
    }
}
