using System;
using System.Collections.Generic;

namespace UrbanGenesis.Simulation
{
    /// <summary>
    /// One stretch of road between two nodes, following a (possibly curved) centre line.
    /// Closures (roadworks, burst pipes, ...) will work by lowering <see cref="OpenFraction"/>,
    /// and traffic code will read <see cref="EffectiveCapacity"/> when it picks routes.
    /// </summary>
    public sealed class RoadSegment
    {
        /// <summary>How far apart the corners of the road-surface pieces are, in metres.</summary>
        public const double SurfaceSpacing = 4.0;

        private float openFraction = 1f;
        private List<Vec2[]> surface;

        public int Id { get; private set; }
        public RoadNode A { get; private set; }
        public RoadNode B { get; private set; }
        public RoadCurve Curve { get; private set; }

        /// <summary>How much traffic the segment carries when fully open (abstract units).</summary>
        public float BaseCapacity { get; set; }

        /// <summary>1 = fully open, 0 = fully closed. Values outside the range are clamped.</summary>
        public float OpenFraction
        {
            get { return openFraction; }
            set { openFraction = Math.Max(0f, Math.Min(1f, value)); }
        }

        public float EffectiveCapacity { get { return BaseCapacity * openFraction; } }
        public bool IsClosed { get { return openFraction <= 0f; } }
        public double Length { get { return Curve.Length; } }

        public RoadSegment(int id, RoadNode a, RoadNode b, RoadCurve curve, float baseCapacity)
        {
            Id = id;
            A = a;
            B = b;
            Curve = curve;
            BaseCapacity = baseCapacity;
        }

        public RoadNode OtherEnd(RoadNode node)
        {
            return node == A ? B : A;
        }

        /// <summary>
        /// The road surface as a strip of small convex quads (corners run anticlockwise), each
        /// <see cref="RoadNetwork.RoadWidth"/> wide. Calculated once and kept.
        /// </summary>
        public IList<Vec2[]> Surface
        {
            get
            {
                if (surface == null)
                {
                    surface = BuildSurface();
                }

                return surface;
            }
        }

        private List<Vec2[]> BuildSurface()
        {
            var result = new List<Vec2[]>();
            List<Vec2> centre = Curve.Sample(SurfaceSpacing);
            double half = RoadNetwork.HalfWidth;
            int pieces = centre.Count - 1;

            // Using the same sample positions as Sample() so the pieces share corners exactly.
            for (int i = 0; i < pieces; i++)
            {
                double s0 = Curve.Length * i / pieces;
                double s1 = Curve.Length * (i + 1) / pieces;
                result.Add(new[]
                {
                    Curve.OffsetPointAtLength(s0, -half),
                    Curve.OffsetPointAtLength(s1, -half),
                    Curve.OffsetPointAtLength(s1, half),
                    Curve.OffsetPointAtLength(s0, half)
                });
            }

            return result;
        }
    }
}
