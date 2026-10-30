using System.Collections.Generic;

namespace UrbanGenesis.Simulation
{
    /// <summary>A junction or dead end where road segments meet.</summary>
    public sealed class RoadNode
    {
        private readonly List<RoadSegment> segments = new List<RoadSegment>(4);

        public int Id { get; private set; }
        public Vec2 Position { get; private set; }

        /// <summary>The segments that start or end here.</summary>
        public IList<RoadSegment> Segments { get { return segments; } }

        public int Degree { get { return segments.Count; } }

        public RoadNode(int id, Vec2 position)
        {
            Id = id;
            Position = position;
        }

        internal void Attach(RoadSegment segment) { segments.Add(segment); }
        internal void Detach(RoadSegment segment) { segments.Remove(segment); }
    }
}
