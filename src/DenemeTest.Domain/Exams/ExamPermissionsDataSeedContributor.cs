using System.Threading.Tasks;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;

namespace DenemeTest.Exams
{
    /// <summary>
    /// Varsayılan admin rolüne sınav (Exams) izinlerini atayan data seed.
    /// Uygulama ilk ayağa kalktığında bir kere çalışır.
    /// </summary>
    public class ExamPermissionsDataSeedContributor : IDataSeedContributor, ITransientDependency
    {
        private readonly IIdentityRoleRepository _roleRepository;
        private readonly IPermissionDataSeeder _permissionDataSeeder;

        public ExamPermissionsDataSeedContributor(
            IIdentityRoleRepository roleRepository,
            IPermissionDataSeeder permissionDataSeeder)
        {
            _roleRepository = roleRepository;
            _permissionDataSeeder = permissionDataSeeder;
        }

        public async Task SeedAsync(DataSeedContext context)
        {
            // Varsayılan admin rolünü bul
            var adminRole = await _roleRepository.FindByNormalizedNameAsync("ADMIN");
            if (adminRole == null)
            {
                return;
            }

            // Permission adlarını string olarak veriyoruz
            var permissions = new[]
            {
                "DenemeTest.Exams",
                "DenemeTest.Exams.Reports",
                "DenemeTest.Exams.Questions",
                "DenemeTest.Exams.Candidates",
                "DenemeTest.Exams.Invitations",
                "DenemeTest.Exams.Tests"
            };

            await _permissionDataSeeder.SeedAsync(
                RolePermissionValueProvider.ProviderName,
                adminRole.Name,      // providerKey = Rol adı
                permissions,
                context.TenantId
            );
        }
    }
}
