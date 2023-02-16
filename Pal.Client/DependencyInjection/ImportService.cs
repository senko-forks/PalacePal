using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pal.Client.Database;

namespace Pal.Client.DependencyInjection
{
    internal sealed class ImportService
    {
        private readonly IServiceProvider _serviceProvider;

        public ImportService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void Add(ImportHistory history)
        {
            using var scope = _serviceProvider.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<PalClientContext>();

            dbContext.Imports.Add(history);
            dbContext.SaveChanges();
        }

        public async Task<ImportHistory?> FindLast(CancellationToken token = default)
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            await using var dbContext = scope.ServiceProvider.GetRequiredService<PalClientContext>();

            return await dbContext.Imports.OrderByDescending(x => x.ImportedAt).ThenBy(x => x.Id).FirstOrDefaultAsync(cancellationToken: token);
        }

        public List<ImportHistory> FindForServer(string server)
        {
            if (string.IsNullOrEmpty(server))
                return new();

            using var scope = _serviceProvider.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<PalClientContext>();

            return dbContext.Imports.Where(x => x.RemoteUrl == server).ToList();
        }

        public void RemoveAllByIds(List<Guid> ids)
        {
            using var scope = _serviceProvider.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<PalClientContext>();

            dbContext.RemoveRange(dbContext.Imports.Where(x => ids.Contains(x.Id)));
            dbContext.SaveChanges();
        }

        public void RemoveById(Guid id)
            => RemoveAllByIds(new List<Guid> { id });
    }
}
