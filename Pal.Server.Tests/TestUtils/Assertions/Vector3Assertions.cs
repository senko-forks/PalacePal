using System.Numerics;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;

namespace Pal.Server.Tests.TestUtils.Assertions
{
    public sealed class Vector3Assertions : ReferenceTypeAssertions<Vector3, Vector3Assertions>
    {
        public Vector3Assertions(Vector3 subject)
            : base(subject)
        {
        }

        protected override string Identifier => "Vector3";

        public AndConstraint<Vector3Assertions> BeAt(float x, float y, float z, string because = "",
            params object[] becauseArgs)
        {
            return BeAt(new Vector3(x, y, z), because, becauseArgs);
        }

        public AndConstraint<Vector3Assertions> BeAt(Vector3 expected, string because = "",
            params object[] becauseArgs)
        {
            Execute.Assertion.ForCondition((expected - Subject).Length() < 0.01f)
                .BecauseOf(because, becauseArgs)
                .FailWith("Expected {0}, got {1}", expected, Subject);

            return new AndConstraint<Vector3Assertions>(this);
        }
    }
}
