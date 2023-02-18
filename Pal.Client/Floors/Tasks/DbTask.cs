using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Pal.Client.Database;

namespace Pal.Client.Floors.Tasks
{
    internal abstract class DbTask
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
                using var dbContext = scope.ServiceProvider.GetRequiredService<PalClientContext>();

                Run(dbContext);
            });
        }

        protected abstract void Run(PalClientContext dbContext);
    }
}
