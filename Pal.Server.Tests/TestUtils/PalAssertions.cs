using System;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;

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
    }
}
