using Microsoft.EntityFrameworkCore;

namespace Pal.Client.Database
{
    internal class PalClientContext : DbContext
    {
        public DbSet<ImportHistory> Imports { get; set; } = null!;

        public PalClientContext(DbContextOptions<PalClientContext> options)
            : base(options)
        {
        }
    }
}
