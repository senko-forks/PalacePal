extern alias PalServer;
using System.Numerics;
using FluentAssertions;
using FluentAssertions.Primitives;
using PalServer::Pal.Server.Database;

namespace Pal.Server.Tests.TestUtils.Assertions
{
    public sealed class ServerLocationAssertions : ReferenceTypeAssertions<ServerLocation, ServerLocationAssertions>
    {
        public ServerLocationAssertions(ServerLocation subject)
            : base(subject)
        {
        }

        protected override string Identifier => "ServerLocation";
        private Vector3 Location => new(Subject.X, Subject.Y, Subject.Z);

        public AndConstraint<ServerLocationAssertions> BeLocatedAt(float x, float y, float z, string because = "",
            params object[] becauseArgs)
        {
            Location.Should().BeAt(x, y, z);
            return new AndConstraint<ServerLocationAssertions>(this);
        }
    }
}
