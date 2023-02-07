using System.Numerics;

namespace Pal.Common
{
    public class PalaceMath
    {
        public static bool IsNearlySamePosition(Vector3 a, Vector3 b)
        {
            return (int)a.X == (int)b.X && (int)a.Y == (int)b.Y && (int)a.Z == (int)b.Z;
        }

        public static int GetHashCode(Vector3 v)
        {
            return HashCode.Combine((int)v.X, (int)v.Y, (int)v.Z);
        }
    }
}
