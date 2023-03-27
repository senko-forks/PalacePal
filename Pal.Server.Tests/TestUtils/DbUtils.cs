extern alias PalServer;
using System;
using System.Collections.Generic;
using Pal.Common;
using PalServer::Pal.Server.Database;
using DbAccount = Account;

namespace Pal.Server.Tests.TestUtils
{
    public static class DbUtils
    {
        public static readonly Guid DefaultAccountId = Guid.Parse("e658b479-63cd-4dcb-9888-4e222c8a3feb");

        public static void ResetToDefaultAccount(this PalServerContext dbContext, List<string>? roles = null)
        {
            dbContext.Accounts.RemoveRange(dbContext.Accounts);
            dbContext.Locations.RemoveRange(dbContext.Locations);

            dbContext.Accounts.Add(new PalServer::Pal.Server.Database.Account
            {
                Id = DefaultAccountId,
                CreatedAt = DateTime.UtcNow,
                Roles = roles ?? new()
            });

            for (int i = -5; i <= 10; ++i)
            {
                if (i == 0)
                    continue;

                dbContext.Locations.Add(new ServerLocation
                {
                    Id = Guid.NewGuid(),
                    TerritoryType = (ushort)ETerritoryType.Palace_1_10,
                    Type = i > 0 ? ServerLocation.EType.Trap : ServerLocation.EType.Hoard,
                    X = i,
                    Y = 50,
                    Z = 100,
                    AccountId = Guid.Empty,
                    CreatedAt = DateTime.UtcNow,
                });
            }

            dbContext.SaveChanges();
        }
    }
}
