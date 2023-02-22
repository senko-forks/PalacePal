using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pal.Client.Database;

namespace Pal.Client.Floors.Tasks
{
    internal abstract class DbTask<T>
        where T : DbTask<T>
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;

        protected DbTask(IServiceScopeFactory serviceScopeFactory)
        {
            _serviceScopeFactory = serviceScopeFactory;
        }

        public void Start()
        {
            Task.Run(() =>
            {
                using var scope = _serviceScopeFactory.CreateScope();
                ILogger<T> logger = scope.ServiceProvider.GetRequiredService<ILogger<T>>();
                using var dbContext = scope.ServiceProvider.GetRequiredService<PalClientContext>();

                Run(dbContext, logger);
            });
        }

        protected abstract void Run(PalClientContext dbContext, ILogger<T> logger);
    }
}
