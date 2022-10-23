using ECommons.SplatoonAPI;
using Palace;
using System;
using System.Numerics;
using System.Text.Json.Serialization;

namespace Pal.Client
{
    internal class Marker
    {
        public ObjectType Type { get; set; } = ObjectType.Unknown;
        public Vector3 Position { get; set; }
        public bool Seen { get; set; } = false;

        [JsonIgnore]
        public bool RemoteSeen { get; set; } = false;

        [JsonIgnore]
        public Element SplatoonElement { get; set; }

        public Marker(ObjectType type, Vector3 position)
        {
            Type = type;
            Position = position;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Type, (int)Position.X, (int)Position.Y, (int)Position.Z);
        }

        public override bool Equals(object obj)
        {
            return obj is Marker otherMarker && Type == otherMarker.Type && Position == otherMarker.Position;
        }
    }
}
