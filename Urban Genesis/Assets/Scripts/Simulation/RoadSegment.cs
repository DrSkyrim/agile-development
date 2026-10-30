using System;

namespace UrbanGenesis.Simulation
{
    /// <summary>
    /// One piece of road between two neighbouring road cells.
    /// Closures (roadworks, burst pipes, ...) will work by lowering <see cref="OpenFraction"/>,
    /// and traffic code will read <see cref="EffectiveCapacity"/> when it picks routes.
    /// </summary>
    public sealed class RoadSegment
    {
        private float openFraction = 1f;

        public GridCoord A { get; private set; }
        public GridCoord B { get; private set; }

        /// <summary>How much traffic the segment carries when fully open (abstract units).</summary>
        public float BaseCapacity { get; set; }

        /// <summary>1 = fully open, 0 = fully closed. Values outside the range are clamped.</summary>
        public float OpenFraction
        {
            get { return openFraction; }
            set { openFraction = Math.Max(0f, Math.Min(1f, value)); }
        }

        public float EffectiveCapacity
        {
            get { return BaseCapacity * openFraction; }
        }

        public bool IsClosed
        {
            get { return openFraction <= 0f; }
        }

        public RoadSegment(GridCoord a, GridCoord b, float baseCapacity)
        {
            A = a;
            B = b;
            BaseCapacity = baseCapacity;
        }
    }
}
