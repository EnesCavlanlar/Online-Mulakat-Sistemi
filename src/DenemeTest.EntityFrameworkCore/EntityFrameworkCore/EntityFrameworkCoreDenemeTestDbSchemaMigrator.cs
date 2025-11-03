using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DenemeTest.Data;
using Volo.Abp.DependencyInjection;

namespace DenemeTest.EntityFrameworkCore;

public class EntityFrameworkCoreDenemeTestDbSchemaMigrator
    : IDenemeTestDbSchemaMigrator, ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public EntityFrameworkCoreDenemeTestDbSchemaMigrator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        /* We intentionally resolving the DenemeTestDbContext
         * from IServiceProvider (instead of directly injecting it)
         * to properly get the connection string of the current tenant in the
         * current scope.
         */

        await _serviceProvider
            .GetRequiredService<DenemeTestDbContext>()
            .Database
            .MigrateAsync();
    }
}
