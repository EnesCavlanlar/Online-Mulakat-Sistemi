using System.Threading.Tasks;

namespace DenemeTest.Data;

public interface IDenemeTestDbSchemaMigrator
{
    Task MigrateAsync();
}
