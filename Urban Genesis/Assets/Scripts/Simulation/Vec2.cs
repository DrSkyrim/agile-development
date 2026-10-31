using System;

namespace UrbanGenesis.Simulation
{
    /// <summary>
    /// A point or direction on the ground, in metres. X runs east and Y runs north; the Unity layer
    /// maps Y to the world's Z axis. Doubles keep the road geometry accurate over a whole city.
    /// </summary>
    public struct Vec2 : IEquatable<Vec2>
    {
        public static readonly Vec2 Zero = new Vec2(0.0, 0.0);

        public readonly double X;
        public readonly double Y;

        public Vec2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double LengthSquared { get { return X * X + Y * Y; } }
        public double Length { get { return Math.Sqrt(X * X + Y * Y); } }

        /// <summary>The same direction with length 1, or <see cref="Zero"/> if this has no length.</summary>
        public Vec2 Normalized
        {
            get
            {
                double length = Length;
                return length < 1e-12 ? Zero : new Vec2(X / length, Y / length);
            }
        }

        /// <summary>This turned a quarter turn anticlockwise (to the left when facing along it).</summary>
        public Vec2 PerpLeft { get { return new Vec2(-Y, X); } }

        public static Vec2 operator +(Vec2 a, Vec2 b) { return new Vec2(a.X + b.X, a.Y + b.Y); }
        public static Vec2 operator -(Vec2 a, Vec2 b) { return new Vec2(a.X - b.X, a.Y - b.Y); }
        public static Vec2 operator -(Vec2 a) { return new Vec2(-a.X, -a.Y); }
        public static Vec2 operator *(Vec2 a, double s) { return new Vec2(a.X * s, a.Y * s); }
        public static Vec2 operator *(double s, Vec2 a) { return new Vec2(a.X * s, a.Y * s); }
        public static Vec2 operator /(Vec2 a, double s) { return new Vec2(a.X / s, a.Y / s); }

        public static bool operator ==(Vec2 a, Vec2 b) { return a.X == b.X && a.Y == b.Y; }
        public static bool operator !=(Vec2 a, Vec2 b) { return !(a == b); }

        public static double Dot(Vec2 a, Vec2 b) { return a.X * b.X + a.Y * b.Y; }

        /// <summary>Positive if <paramref name="b"/> is anticlockwise from <paramref name="a"/>.</summary>
        public static double Cross(Vec2 a, Vec2 b) { return a.X * b.Y - a.Y * b.X; }

        public static double Distance(Vec2 a, Vec2 b) { return (a - b).Length; }

        public static Vec2 Lerp(Vec2 a, Vec2 b, double t) { return new Vec2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t); }

        public bool Equals(Vec2 other) { return X == other.X && Y == other.Y; }
        public override bool Equals(object obj) { return obj is Vec2 && Equals((Vec2)obj); }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        public override string ToString()
        {
            return "(" + X.ToString("0.0") + ", " + Y.ToString("0.0") + ")";
        }
    }
}
