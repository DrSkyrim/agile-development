using System;
using System.Collections.Generic;

namespace UrbanGenesis.Simulation
{
    /// <summary>An axis-aligned box, used to skip far-away shapes quickly.</summary>
    public struct Aabb
    {
        public readonly double MinX;
        public readonly double MinY;
        public readonly double MaxX;
        public readonly double MaxY;

        public Aabb(double minX, double minY, double maxX, double maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public static Aabb Of(IList<Vec2> points)
        {
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            for (int i = 0; i < points.Count; i++)
            {
                minX = Math.Min(minX, points[i].X);
                minY = Math.Min(minY, points[i].Y);
                maxX = Math.Max(maxX, points[i].X);
                maxY = Math.Max(maxY, points[i].Y);
            }

            return new Aabb(minX, minY, maxX, maxY);
        }

        public Aabb Expanded(double amount)
        {
            return new Aabb(MinX - amount, MinY - amount, MaxX + amount, MaxY + amount);
        }

        public bool Contains(Vec2 p)
        {
            return p.X >= MinX && p.X <= MaxX && p.Y >= MinY && p.Y <= MaxY;
        }

        public bool Overlaps(Aabb other)
        {
            return MinX <= other.MaxX && MaxX >= other.MinX && MinY <= other.MaxY && MaxY >= other.MinY;
        }
    }

    /// <summary>Flat-ground geometry helpers. Polygons are lists of corners in order.</summary>
    public static class Geometry
    {
        /// <summary>Area of a polygon; positive when its corners run anticlockwise, negative when clockwise.</summary>
        public static double SignedArea(IList<Vec2> polygon)
        {
            double sum = 0.0;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vec2 a = polygon[i];
                Vec2 b = polygon[(i + 1) % polygon.Count];
                sum += a.X * b.Y - b.X * a.Y;
            }

            return sum * 0.5;
        }

        /// <summary>The same corners, reversed if needed so they run anticlockwise.</summary>
        public static Vec2[] Anticlockwise(IList<Vec2> polygon)
        {
            var result = new Vec2[polygon.Count];
            bool reverse = SignedArea(polygon) < 0.0;
            for (int i = 0; i < polygon.Count; i++)
            {
                result[i] = reverse ? polygon[polygon.Count - 1 - i] : polygon[i];
            }

            return result;
        }

        /// <summary>True if a polygon with anticlockwise corners turns left at every corner (no dents, no twists).</summary>
        public static bool IsConvexAnticlockwise(IList<Vec2> polygon)
        {
            for (int i = 0; i < polygon.Count; i++)
            {
                Vec2 a = polygon[i];
                Vec2 b = polygon[(i + 1) % polygon.Count];
                Vec2 c = polygon[(i + 2) % polygon.Count];
                if (Vec2.Cross(b - a, c - b) <= 1e-9)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>True if the point is inside (or on the edge of) a convex polygon whose corners run anticlockwise.</summary>
        public static bool PointInConvex(Vec2 p, IList<Vec2> polygon)
        {
            for (int i = 0; i < polygon.Count; i++)
            {
                Vec2 a = polygon[i];
                Vec2 b = polygon[(i + 1) % polygon.Count];
                if (Vec2.Cross(b - a, p - a) < -1e-9)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// True if two convex polygons overlap by more than <paramref name="tolerance"/> metres.
        /// Shapes that only touch, or overlap by a sliver, do not count. Corner order does not matter.
        /// </summary>
        public static bool ConvexOverlap(IList<Vec2> a, IList<Vec2> b, double tolerance)
        {
            return !SeparatedOnAnEdgeOf(a, a, b, tolerance) && !SeparatedOnAnEdgeOf(b, a, b, tolerance);
        }

        private static bool SeparatedOnAnEdgeOf(IList<Vec2> owner, IList<Vec2> a, IList<Vec2> b, double tolerance)
        {
            for (int i = 0; i < owner.Count; i++)
            {
                Vec2 edge = owner[(i + 1) % owner.Count] - owner[i];
                Vec2 axis = edge.PerpLeft.Normalized;
                if (axis == Vec2.Zero)
                {
                    continue;
                }

                double minA, maxA, minB, maxB;
                Project(a, axis, out minA, out maxA);
                Project(b, axis, out minB, out maxB);

                double overlap = Math.Min(maxA, maxB) - Math.Max(minA, minB);
                if (overlap <= tolerance)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Project(IList<Vec2> polygon, Vec2 axis, out double min, out double max)
        {
            min = double.MaxValue;
            max = double.MinValue;
            for (int i = 0; i < polygon.Count; i++)
            {
                double d = Vec2.Dot(polygon[i], axis);
                min = Math.Min(min, d);
                max = Math.Max(max, d);
            }
        }

        /// <summary>Distance from a point to the line segment a-b.</summary>
        public static double DistanceToSegment(Vec2 p, Vec2 a, Vec2 b)
        {
            double u;
            return DistanceToSegment(p, a, b, out u);
        }

        /// <summary>Distance from a point to the segment a-b; <paramref name="u"/> is how far along it (0 to 1) the nearest point is.</summary>
        public static double DistanceToSegment(Vec2 p, Vec2 a, Vec2 b, out double u)
        {
            Vec2 ab = b - a;
            double lengthSquared = ab.LengthSquared;
            u = lengthSquared < 1e-12 ? 0.0 : Math.Max(0.0, Math.Min(1.0, Vec2.Dot(p - a, ab) / lengthSquared));
            return Vec2.Distance(p, a + ab * u);
        }

        /// <summary>Points along the edges of a polygon (its corners, and extra points along each edge) for checking clearance.</summary>
        public static List<Vec2> BoundarySamples(IList<Vec2> polygon, int extraPerEdge)
        {
            var samples = new List<Vec2>();
            for (int i = 0; i < polygon.Count; i++)
            {
                Vec2 a = polygon[i];
                Vec2 b = polygon[(i + 1) % polygon.Count];
                for (int k = 0; k <= extraPerEdge; k++)
                {
                    samples.Add(Vec2.Lerp(a, b, k / (double)(extraPerEdge + 1)));
                }
            }

            return samples;
        }
    }
}
