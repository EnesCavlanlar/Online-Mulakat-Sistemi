using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DenemeTest.EntityFrameworkCore;

public class DenemeTestDbContextFactory : IDesignTimeDbContextFactory<DenemeTestDbContext>
{
    public DenemeTestDbContext CreateDbContext(string[] args)
    {
        // DbMigrator içindeki appsettings.json'u yükle
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "DenemeTest.DbMigrator");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var connectionString = configuration.GetConnectionString("Default");

        var builder = new DbContextOptionsBuilder<DenemeTestDbContext>()
            .UseNpgsql(connectionString);

        return new DenemeTestDbContext(builder.Options);
    }
}
