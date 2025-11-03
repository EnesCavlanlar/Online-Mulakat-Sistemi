using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace DenemeTest.Data;

/* This is used if database provider does't define
 * IDenemeTestDbSchemaMigrator implementation.
 */
public class NullDenemeTestDbSchemaMigrator : IDenemeTestDbSchemaMigrator, ITransientDependency
{
    public Task MigrateAsync()
    {
        return Task.CompletedTask;
    }
}
