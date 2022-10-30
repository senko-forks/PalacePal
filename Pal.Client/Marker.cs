using ECommons.SplatoonAPI;
using Palace;
using System;
using System.Numerics;
using System.Text.Json.Serialization;

namespace Pal.Client
{
    internal class Marker
    {
        public EType Type { get; set; } = EType.Unknown;
        public Vector3 Position { get; set; }
        public bool Seen { get; set; } = false;

        [JsonIgnore]
        public bool RemoteSeen { get; set; } = false;

        [JsonIgnore]
        public Element? SplatoonElement { get; set; }

        public Marker(EType type, Vector3 position)
        {
            Type = type;
            Position = position;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Type, (int)Position.X, (int)Position.Y, (int)Position.Z);
        }

        public override bool Equals(object? obj)
        {
            return obj is Marker otherMarker && Type == otherMarker.Type && (int)Position.X == (int)otherMarker.Position.X && (int)Position.Y == (int)otherMarker.Position.Y && (int)Position.Z == (int)otherMarker.Position.Z;
        }

        public static bool operator ==(Marker? a, object? b)
        {
            return Equals(a, b);
        }

        public static bool operator !=(Marker? a, object? b)
        {
            return !Equals(a, b);
        }


        public bool IsPermanent() => Type == EType.Trap || Type == EType.Hoard;

        public enum EType
        {
            Unknown = ObjectType.Unknown,

            #region Permanent Markers
            Trap = ObjectType.Trap,
            Hoard = ObjectType.Hoard,
            #endregion

            # region Markers that only show up if they're currently visible
            SilverCoffer = 100,
            #endregion
        }
    }
}
