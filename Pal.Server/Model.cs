using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.ComponentModel.DataAnnotations;

namespace Pal.Server
{
    public class Account
    {
        public Guid Id { get; set; }

        /// <summary>
        /// Anti-Spam: This is a hash of the IP address used to create the account - if you try to create an account later and have the same IP hash
        /// (which should only happen if you have the same IP), this will return the old account id.
        ///
        /// This will be deleted after a set time after account creation.
        /// </summary>
        /// <seealso cref="Pal.Server.Services.RemoveIpHashService"/>
        [MaxLength(20)]
        public string? IpHash { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<string> Roles { get; set; } = new List<string>();
    }

    public class PalaceLocation
    {
        public Guid Id { get; set; }
        public ushort TerritoryType { get; set; }
        public EType Type { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public Guid AccountId { get; set; }
        public DateTime CreatedAt { get; set; }

        public enum EType
        {
            Trap = 1,
            Hoard = 2
        }
    }

    public class GlobalSetting
    {
        public GlobalSetting(string key, string value)
        {
            Key = key;
            Value = value;
        }

        [Key]
        public string Key { get; set; }

        [MaxLength(128)]
        public string Value { get; set; }
    }

    public class PalContext : DbContext
    {
        public DbSet<Account> Accounts { get; set; } = null!;
        public DbSet<PalaceLocation> Locations { get; set; } = null!;
        public DbSet<GlobalSetting> GlobalSettings { get; set; } = null!;

        public string DbPath { get; }

        public PalContext()
        {
#if DEBUG
            DbPath = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "pal.db");
#else
            DbPath = "palace-pal.db";
#endif
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite($"Data Source={DbPath}");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Account>()
                .Property(a => a.Roles)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                    new ValueComparer<List<string>>(
                        (c1, c2) => c1.SequenceEqual(c2),
                        c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                        c => c.ToList()));
        }
    }
}
