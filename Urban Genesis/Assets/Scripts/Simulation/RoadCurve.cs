using System;
using System.Collections.Generic;

namespace UrbanGenesis.Simulation
{
    /// <summary>
    /// The centre line of a road: a quadratic Bezier curve from <see cref="P0"/> to <see cref="P2"/> that bends
    /// towards the control point <see cref="P1"/>. With the control point halfway between the ends it is a straight line.
    /// Positions on the curve are measured as arc length (metres travelled from the start), which is what roads,
    /// lots and junctions care about. Instances never change.
    /// </summary>
    public sealed class RoadCurve
    {
        /// <summary>How many straight pieces the curve is cut into for length and distance maths.</summary>
        public const int TableSize = 64;

        private readonly Vec2[] points = new Vec2[TableSize + 1];
        private readonly double[] cumulative = new double[TableSize + 1];

        public Vec2 P0 { get; private set; }
        public Vec2 P1 { get; private set; }
        public Vec2 P2 { get; private set; }

        public double Length { get; private set; }
        public Aabb Bounds { get; private set; }

        public RoadCurve(Vec2 start, Vec2 control, Vec2 end)
        {
            P0 = start;
            P1 = control;
            P2 = end;

            double total = 0.0;
            for (int i = 0; i <= TableSize; i++)
            {
                points[i] = Point(i / (double)TableSize);
                if (i > 0)
                {
                    total += Vec2.Distance(points[i - 1], points[i]);
                }

                cumulative[i] = total;
            }

            Length = total;
            Bounds = Aabb.Of(points);
        }

        /// <summary>A straight road: the control point sits halfway between the ends.</summary>
        public static RoadCurve Straight(Vec2 start, Vec2 end)
        {
            return new RoadCurve(start, Vec2.Lerp(start, end, 0.5), end);
        }

        public bool IsStraight
        {
            get { return Math.Abs(Vec2.Cross(P1 - P0, P2 - P0)) < 1e-6 && Vec2.Dot(P1 - P0, P2 - P1) >= 0.0; }
        }

        // ---------------------------------------------------------------- positions by parameter t (0 to 1)

        public Vec2 Point(double t)
        {
            double u = 1.0 - t;
            return P0 * (u * u) + P1 * (2.0 * u * t) + P2 * (t * t);
        }

        /// <summary>Which way the curve is heading at t, with length 1 (see <see cref="Derivative"/> for the raw direction).</summary>
        public Vec2 Tangent(double t)
        {
            Vec2 d = Derivative(t).Normalized;
            return d == Vec2.Zero ? (P2 - P0).Normalized : d;
        }

        public Vec2 Derivative(double t)
        {
            return (P1 - P0) * (2.0 * (1.0 - t)) + (P2 - P1) * (2.0 * t);
        }

        // ---------------------------------------------------------------- positions by arc length

        /// <summary>The parameter t reached after travelling <paramref name="s"/> metres from the start.</summary>
        public double TAtLength(double s)
        {
            if (s <= 0.0)
            {
                return 0.0;
            }

            if (s >= Length)
            {
                return 1.0;
            }

            int lo = 0;
            int hi = TableSize;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (cumulative[mid] <= s)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }

            double span = cumulative[hi] - cumulative[lo];
            double fraction = span < 1e-12 ? 0.0 : (s - cumulative[lo]) / span;
            return (lo + fraction) / TableSize;
        }

        public Vec2 PointAtLength(double s) { return Point(TAtLength(s)); }
        public Vec2 TangentAtLength(double s) { return Tangent(TAtLength(s)); }

        /// <summary>The direction pointing to the left of the road at that distance, with length 1.</summary>
        public Vec2 NormalAtLength(double s) { return TangentAtLength(s).PerpLeft; }

        /// <summary>A point on the curve pushed sideways: positive offsets go left of the direction of travel, negative go right.</summary>
        public Vec2 OffsetPointAtLength(double s, double offset)
        {
            double t = TAtLength(s);
            return Point(t) + Tangent(t).PerpLeft * offset;
        }

        // ---------------------------------------------------------------- nearest point

        /// <summary>Distance from a point to the nearest part of the curve.</summary>
        public double DistanceTo(Vec2 p)
        {
            double distance;
            ClosestLength(p, out distance);
            return distance;
        }

        /// <summary>The arc length of the nearest point on the curve, and how far away that point is.</summary>
        public double ClosestLength(Vec2 p, out double distance)
        {
            double bestDistance = double.MaxValue;
            double bestLength = 0.0;

            for (int i = 0; i < TableSize; i++)
            {
                double u;
                double d = Geometry.DistanceToSegment(p, points[i], points[i + 1], out u);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    bestLength = cumulative[i] + (cumulative[i + 1] - cumulative[i]) * u;
                }
            }

            distance = bestDistance;
            return bestLength;
        }

        // ---------------------------------------------------------------- shape

        /// <summary>
        /// Radius of the tightest bend. A straight road has an enormous radius; a curve that folds back on itself
        /// (a cusp) has radius 0.
        /// </summary>
        public double MinRadius()
        {
            Vec2 second = (P2 - P1 * 2.0 + P0) * 2.0;
            double maxCurvature = 0.0;

            for (int i = 0; i <= 32; i++)
            {
                Vec2 d = Derivative(i / 32.0);
                double speed = d.Length;
                if (speed < 1e-6)
                {
                    return 0.0;
                }

                double curvature = Math.Abs(Vec2.Cross(d, second)) / (speed * speed * speed);
                maxCurvature = Math.Max(maxCurvature, curvature);
            }

            return maxCurvature < 1e-9 ? double.MaxValue : 1.0 / maxCurvature;
        }

        /// <summary>Cuts the curve in two at parameter t. The pieces together trace exactly the same path.</summary>
        public void Split(double t, out RoadCurve first, out RoadCurve second)
        {
            Vec2 a = Vec2.Lerp(P0, P1, t);
            Vec2 b = Vec2.Lerp(P1, P2, t);
            Vec2 middle = Vec2.Lerp(a, b, t);
            first = new RoadCurve(P0, a, middle);
            second = new RoadCurve(middle, b, P2);
        }

        public void SplitAtLength(double s, out RoadCurve first, out RoadCurve second)
        {
            Split(TAtLength(s), out first, out second);
        }

        /// <summary>The curve's centre line as points about <paramref name="spacing"/> metres apart, including both ends.</summary>
        public List<Vec2> Sample(double spacing)
        {
            int pieces = Math.Max(1, (int)Math.Ceiling(Length / Math.Max(0.1, spacing)));
            var result = new List<Vec2>(pieces + 1);
            for (int i = 0; i <= pieces; i++)
            {
                result.Add(PointAtLength(Length * i / pieces));
            }

            return result;
        }
    }
}
