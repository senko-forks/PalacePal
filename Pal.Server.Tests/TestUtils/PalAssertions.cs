extern alias PalServer;
using System;
using System.Numerics;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Pal.Server.Tests.TestUtils.Assertions;
using Palace;
using PalServer::Pal.Server.Database;

namespace Pal.Server.Tests.TestUtils
{
    public static class PalAssertions
    {
        public static AndConstraint<StringAssertions> BeGuid(this StringAssertions assertions,
            string because = "", params object[] becauseArgs)
        {
            Execute.Assertion.ForCondition(Guid.TryParse(assertions.Subject, out _))
                .BecauseOf(because, becauseArgs)
                .FailWith("Expected a GUID, but found {0}", assertions.Subject);

            return new AndConstraint<StringAssertions>(assertions);
        }

        public static Vector3Assertions Should(this Vector3 v) => new(v);
        public static PalaceObjectAssertions Should(this PalaceObject palaceObject) => new(palaceObject);
        public static ServerLocationAssertions Should(this ServerLocation serverLocation) => new(serverLocation);
    }
}
