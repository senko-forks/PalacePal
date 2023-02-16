using System;
using System.Collections.Generic;
using System.Linq;
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

        public ImportHistory? FindLast()
        {
            using var scope = _serviceProvider.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<PalClientContext>();

            return dbContext.Imports.OrderByDescending(x => x.ImportedAt).ThenBy(x => x.Id).FirstOrDefault();
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
