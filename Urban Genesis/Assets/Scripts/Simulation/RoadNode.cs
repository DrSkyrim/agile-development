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

        /// <summary>Three or more roads meet here (a T-junction, a four-way crossing, ...).</summary>
        public bool IsIntersection { get { return segments.Count >= 3; } }

        /// <summary>The player has switched the traffic lights off here. They are on by default.</summary>
        public bool TrafficLightsOff { get; internal set; }

        /// <summary>Traffic lights stand here: it is an intersection and they have not been switched off.</summary>
        public bool HasTrafficLight { get { return IsIntersection && !TrafficLightsOff; } }

        public RoadNode(int id, Vec2 position)
        {
            Id = id;
            Position = position;
        }

        internal void Attach(RoadSegment segment) { segments.Add(segment); }
        internal void Detach(RoadSegment segment) { segments.Remove(segment); }
    }
}
