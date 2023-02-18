using System;
using System.Collections.Generic;
using System.Numerics;
using Pal.Client.Rendering;
using Pal.Common;
using Palace;

namespace Pal.Client.Floors
{
    /// <summary>
    /// Base class for <see cref="MemoryLocation"/> and <see cref="EphemeralLocation"/>.
    /// </summary>
    internal abstract class MemoryLocation
    {
        public required EType Type { get; init; }
        public required Vector3 Position { get; init; }
        public bool Seen { get; set; }

        public IRenderElement? RenderElement { get; set; }

        public enum EType
        {
            Unknown,

            Hoard,
            Trap,

            SilverCoffer,
        }

        public override bool Equals(object? obj)
        {
            return obj is MemoryLocation otherLocation &&
                   Type == otherLocation.Type &&
                   PalaceMath.IsNearlySamePosition(Position, otherLocation.Position);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Type, PalaceMath.GetHashCode(Position));
        }
    }

    internal static class ETypeExtensions
    {
        public static MemoryLocation.EType ToMemoryType(this ObjectType objectType)
        {
            return objectType switch
            {
                ObjectType.Trap => MemoryLocation.EType.Trap,
                ObjectType.Hoard => MemoryLocation.EType.Hoard,
                _ => throw new ArgumentOutOfRangeException(nameof(objectType), objectType, null)
            };
        }
    }
}
