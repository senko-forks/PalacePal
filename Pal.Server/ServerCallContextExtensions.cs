using System.Security.Claims;
using Grpc.Core;

namespace Pal.Server
{
    internal static class ServerCallContextExtensions
    {
        public static bool TryGetAccountId(this ServerCallContext context, out Guid accountId)
        {
            accountId = Guid.Empty;
            ClaimsPrincipal user = context.GetHttpContext().User;
            Claim? claim = user.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier);
            if (claim == null)
                return false;

            return Guid.TryParse(claim.Value, out accountId);
        }

        public static Guid GetAccountId(this ServerCallContext context)
        {
            if (TryGetAccountId(context, out Guid accountId))
                return accountId;

            throw new InvalidOperationException("No account id in context");
        }
    }
}
