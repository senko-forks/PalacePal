using Grpc.Core;
using Grpc.Net.Client;
using Palace;

namespace Pal.StandaloneClient
{
    internal class Program
    {
        private const string remoteUrl = "http://localhost:5415";
        private static readonly Guid accountId = Guid.Parse("ce7b109a-5e29-4b63-ab3e-b6f89eb5e19e"); // manually created account id

        static async Task Main(string[] args)
        {
            GrpcChannel channel = GrpcChannel.ForAddress(remoteUrl);
            var accountClient = new Account.AccountService.AccountServiceClient(channel);
            var loginReply = await accountClient.LoginAsync(new Account.LoginRequest
            {
                AccountId = accountId.ToString()
            });
            if (loginReply == null || !loginReply.Success)
                throw new Exception($"Login failed: {loginReply?.Error}");

            var headers = new Metadata()
            {
                { "Authorization", $"Bearer {loginReply.AuthToken}" }
            };
            var palaceClient = new Palace.PalaceService.PalaceServiceClient(channel);
            var markAsSeenRequest = new MarkObjectsSeenRequest { TerritoryType = 772 };
            markAsSeenRequest.NetworkIds.Add("0c635960-0e2e-4ec6-9fb5-443d0e7a3315"); // this is an already existing entry
            var markAsSeenReply = await palaceClient.MarkObjectsSeenAsync(markAsSeenRequest, headers: headers);
            Console.WriteLine($"Reply = {markAsSeenReply.Success}");
        }
    }
}
