using System;
using System.Threading.Tasks;
using Account;
using FluentAssertions;
using Grpc.Core;
using Pal.Server.Tests.TestUtils;
using Xunit;

namespace Pal.Server.Tests.Services
{
    public sealed class AnonymousAccountServiceTests : IClassFixture<AnonymousGrpc>
    {
        private readonly AnonymousGrpc _grpc;

        public AnonymousAccountServiceTests(AnonymousGrpc grpc)
        {
            _grpc = grpc;
        }

        [Fact]
        public async Task CreateAccountAndLogin()
        {
            CreateAccountReply? createAccountReply =
                await _grpc.AccountsClient.CreateAccountAsync(new CreateAccountRequest());
            createAccountReply.Error.Should().Be(CreateAccountError.None);
            createAccountReply.Success.Should().BeTrue();
            createAccountReply.AccountId.Should().BeGuid();

            LoginReply? loginReply = await _grpc.AccountsClient.LoginAsync(new LoginRequest
            {
                AccountId = createAccountReply.AccountId
            });
            loginReply.Error.Should().Be(LoginError.None);
            loginReply.Success.Should().BeTrue();
            loginReply.AuthToken.Should().NotBeEmpty().And.Contain(".", Exactly.Twice());

            Action verify = () => _grpc.AccountsClient.Verify(new VerifyRequest(),
                new Metadata { { "Authorization", $"Bearer {loginReply.AuthToken}" } });
            verify.Should().NotThrow();
        }

        [Fact]
        public async Task LoginWithRandomIdShouldFail()
        {
            LoginReply? loginReply = await _grpc.AccountsClient.LoginAsync(new LoginRequest
            {
                AccountId = Guid.NewGuid().ToString()
            });
            loginReply.Error.Should().Be(LoginError.InvalidAccountId);
            loginReply.Success.Should().BeFalse();
            loginReply.AuthToken.Should().BeEmpty();
        }

        [Fact]
        public async Task LoginWithNonGuidIdShouldFail()
        {
            LoginReply? loginReply = await _grpc.AccountsClient.LoginAsync(new LoginRequest
            {
                AccountId = "aaaaaaaaaaaaaaaaaaaaa"
            });
            loginReply.Error.Should().Be(LoginError.InvalidAccountId);
            loginReply.Success.Should().BeFalse();
            loginReply.AuthToken.Should().BeEmpty();
        }
    }
}
