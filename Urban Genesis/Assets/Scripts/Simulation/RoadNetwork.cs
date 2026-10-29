using System;
using System.Collections.Generic;

namespace UrbanGenesis.Simulation
{
    /// <summary>
    /// The road graph. Every road cell is a node, and every pair of orthogonally neighbouring
    /// road cells is joined by one <see cref="RoadSegment"/>.
    /// </summary>
    public sealed class RoadNetwork
    {
        public const float DefaultCapacity = 1f;

        private static readonly int[] DirX = { 1, -1, 0, 0 };
        private static readonly int[] DirY = { 0, 0, 1, -1 };

        private readonly HashSet<GridCoord> nodes = new HashSet<GridCoord>();
        private readonly Dictionary<SegmentKey, RoadSegment> segments = new Dictionary<SegmentKey, RoadSegment>();

        public event Action<RoadSegment> SegmentAdded;
        public event Action<RoadSegment> SegmentRemoved;

        public int NodeCount { get { return nodes.Count; } }
        public int SegmentCount { get { return segments.Count; } }

        public IEnumerable<GridCoord> Nodes { get { return nodes; } }
        public IEnumerable<RoadSegment> Segments { get { return segments.Values; } }

        public bool HasNode(GridCoord c)
        {
            return nodes.Contains(c);
        }

        /// <summary>Adds a road cell and connects it to any neighbouring road cells. Returns false if it already existed.</summary>
        public bool AddNode(GridCoord c)
        {
            if (!nodes.Add(c))
            {
                return false;
            }

            for (int i = 0; i < 4; i++)
            {
                GridCoord neighbour = c.Offset(DirX[i], DirY[i]);
                if (nodes.Contains(neighbour))
                {
                    var segment = new RoadSegment(c, neighbour, DefaultCapacity);
                    segments[new SegmentKey(c, neighbour)] = segment;
                    Raise(SegmentAdded, segment);
                }
            }

            return true;
        }

        /// <summary>Removes a road cell and every segment attached to it. Returns false if there was no road there.</summary>
        public bool RemoveNode(GridCoord c)
        {
            if (!nodes.Remove(c))
            {
                return false;
            }

            for (int i = 0; i < 4; i++)
            {
                GridCoord neighbour = c.Offset(DirX[i], DirY[i]);
                var key = new SegmentKey(c, neighbour);
                RoadSegment segment;
                if (segments.TryGetValue(key, out segment))
                {
                    segments.Remove(key);
                    Raise(SegmentRemoved, segment);
                }
            }

            return true;
        }

        public RoadSegment GetSegment(GridCoord a, GridCoord b)
        {
            RoadSegment segment;
            segments.TryGetValue(new SegmentKey(a, b), out segment);
            return segment;
        }

        /// <summary>Number of segments attached to a road cell (0 to 4).</summary>
        public int Degree(GridCoord c)
        {
            int degree = 0;
            for (int i = 0; i < 4; i++)
            {
                if (segments.ContainsKey(new SegmentKey(c, c.Offset(DirX[i], DirY[i]))))
                {
                    degree++;
                }
            }

            return degree;
        }

        /// <summary>
        /// Neighbouring road cells reachable from <paramref name="c"/>. With <paramref name="onlyOpen"/>
        /// set, segments that are fully closed are skipped.
        /// </summary>
        public List<GridCoord> Neighbours(GridCoord c, bool onlyOpen)
        {
            var result = new List<GridCoord>(4);
            for (int i = 0; i < 4; i++)
            {
                GridCoord neighbour = c.Offset(DirX[i], DirY[i]);
                RoadSegment segment;
                if (segments.TryGetValue(new SegmentKey(c, neighbour), out segment))
                {
                    if (!onlyOpen || !segment.IsClosed)
                    {
                        result.Add(neighbour);
                    }
                }
            }

            return result;
        }

        private static void Raise(Action<RoadSegment> handler, RoadSegment segment)
        {
            if (handler != null)
            {
                handler(segment);
            }
        }

        /// <summary>Order-independent key, so (a, b) and (b, a) point at the same segment.</summary>
        private struct SegmentKey : IEquatable<SegmentKey>
        {
            private readonly GridCoord first;
            private readonly GridCoord second;

            public SegmentKey(GridCoord a, GridCoord b)
            {
                bool aFirst = a.X < b.X || (a.X == b.X && a.Y <= b.Y);
                first = aFirst ? a : b;
                second = aFirst ? b : a;
            }

            public bool Equals(SegmentKey other)
            {
                return first == other.first && second == other.second;
            }

            public override bool Equals(object obj)
            {
                return obj is SegmentKey && Equals((SegmentKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (first.GetHashCode() * 397) ^ second.GetHashCode();
                }
            }
        }
    }
}
