using System.Numerics;
using FluentAssertions;
using FluentAssertions.Primitives;
using Palace;

namespace Pal.Server.Tests.TestUtils.Assertions
{
    public sealed class PalaceObjectAssertions : ReferenceTypeAssertions<PalaceObject, PalaceObjectAssertions>
    {
        public PalaceObjectAssertions(PalaceObject instance)
            : base(instance)
        {
        }

        protected override string Identifier => "PalaceObject";
        private Vector3 Location => new(Subject.X, Subject.Y, Subject.Z);

        public AndConstraint<PalaceObjectAssertions> BeLocatedAt(float x, float y, float z, string because = "",
            params object[] becauseArgs)
        {
            Location.Should().BeAt(x, y, z);
            return new AndConstraint<PalaceObjectAssertions>(this);
        }
    }
}
