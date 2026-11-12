using System;
using System.Collections.Generic;

namespace UrbanGenesis.Simulation
{
    /// <summary>
    /// What it costs to drive along a road segment, in metres of free-flowing road.
    /// The economy and (later) traffic both use this: closures and congestion only have to change the cost
    /// and every trip in the city reacts. Return <see cref="double.PositiveInfinity"/> for a segment that cannot be used.
    /// </summary>
    public interface IRoadCost
    {
        double Cost(RoadSegment segment);
    }

    /// <summary>Length divided by speed and open fraction: a closed road cannot be used, a half-open one counts double, a highway counts half.</summary>
    public sealed class FreeFlowRoadCost : IRoadCost
    {
        public const double MinOpenFraction = 0.2;

        public double Cost(RoadSegment segment)
        {
            if (segment.IsClosed)
            {
                return double.PositiveInfinity;
            }

            return segment.Length / (Math.Max(MinOpenFraction, segment.OpenFraction) * RoadSpecs.SpeedFactor(segment.Type));
        }
    }

    /// <summary>
    /// Shortest-route distances over the road network. Nodes are junctions, edges are road segments.
    /// Distances from a junction are computed on demand and cached until <see cref="Invalidate"/> is called
    /// (roads built or removed, a closure opened or closed, congestion changed).
    /// </summary>
    public sealed class RoadGraph
    {
        private readonly RoadNetwork roads;
        private readonly Dictionary<int, Dictionary<int, double>> cache = new Dictionary<int, Dictionary<int, double>>();

        public IRoadCost Cost { get; set; }

        /// <summary>How far (cost) a search from a junction goes. Anything further counts as unreachable.</summary>
        public double MaxSearch { get; set; }

        public RoadGraph(RoadNetwork roads)
        {
            this.roads = roads;
            Cost = new FreeFlowRoadCost();
            MaxSearch = 6000.0;
        }

        public void Invalidate()
        {
            cache.Clear();
        }

        /// <summary>Costs from one junction to every junction reachable within <see cref="MaxSearch"/>.</summary>
        public Dictionary<int, double> NodeDistances(int nodeId)
        {
            Dictionary<int, double> known;
            if (cache.TryGetValue(nodeId, out known))
            {
                return known;
            }

            var dist = new Dictionary<int, double>();
            RoadNode start = roads.GetNode(nodeId);
            if (start != null)
            {
                Search(start, dist);
            }

            cache[nodeId] = dist;
            return dist;
        }

        private void Search(RoadNode start, Dictionary<int, double> dist)
        {
            var heap = new MinHeap();
            dist[start.Id] = 0.0;
            heap.Push(0.0, start.Id);
            var settled = new HashSet<int>();

            while (heap.Count > 0)
            {
                double d;
                int id;
                heap.Pop(out d, out id);
                if (!settled.Add(id))
                {
                    continue;
                }

                RoadNode node = roads.GetNode(id);
                if (node == null)
                {
                    continue;
                }

                IList<RoadSegment> segments = node.Segments;
                for (int i = 0; i < segments.Count; i++)
                {
                    RoadSegment segment = segments[i];
                    double cost = Cost.Cost(segment);
                    if (double.IsInfinity(cost))
                    {
                        continue;
                    }

                    int other = segment.OtherEnd(node).Id;
                    double next = d + cost;
                    double known;
                    if (next > MaxSearch || (dist.TryGetValue(other, out known) && known <= next))
                    {
                        continue;
                    }

                    dist[other] = next;
                    heap.Push(next, other);
                }
            }
        }

        /// <summary>
        /// Travel cost from the middle of one segment to the middle of another, or infinity if there is no open route.
        /// Trips on the same segment cost a third of its length. A closed segment can only be used by trips that stay on it.
        /// </summary>
        public double SegmentDistance(RoadSegment from, RoadSegment to)
        {
            if (from == to)
            {
                return from.Length / 3.0;
            }

            double offsetFrom = Cost.Cost(from) * 0.5;
            double offsetTo = Cost.Cost(to) * 0.5;
            if (double.IsInfinity(offsetFrom) || double.IsInfinity(offsetTo))
            {
                return double.PositiveInfinity;
            }

            double best = double.PositiveInfinity;
            RoadNode[] fromEnds = { from.A, from.B };
            RoadNode[] toEnds = { to.A, to.B };
            for (int i = 0; i < 2; i++)
            {
                Dictionary<int, double> reach = NodeDistances(fromEnds[i].Id);
                for (int j = 0; j < 2; j++)
                {
                    double d;
                    if (reach.TryGetValue(toEnds[j].Id, out d))
                    {
                        best = Math.Min(best, offsetFrom + d + offsetTo);
                    }
                }
            }

            return best;
        }

        // A plain binary min-heap (Unity's runtime has no PriorityQueue).
        private sealed class MinHeap
        {
            private readonly List<double> keys = new List<double>();
            private readonly List<int> values = new List<int>();

            public int Count { get { return keys.Count; } }

            public void Push(double key, int value)
            {
                keys.Add(key);
                values.Add(value);
                int i = keys.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (keys[parent] <= keys[i])
                    {
                        break;
                    }

                    Swap(i, parent);
                    i = parent;
                }
            }

            public void Pop(out double key, out int value)
            {
                key = keys[0];
                value = values[0];
                int last = keys.Count - 1;
                keys[0] = keys[last];
                values[0] = values[last];
                keys.RemoveAt(last);
                values.RemoveAt(last);

                int i = 0;
                while (true)
                {
                    int left = i * 2 + 1;
                    int right = left + 1;
                    int smallest = i;
                    if (left < keys.Count && keys[left] < keys[smallest]) { smallest = left; }
                    if (right < keys.Count && keys[right] < keys[smallest]) { smallest = right; }
                    if (smallest == i)
                    {
                        break;
                    }

                    Swap(i, smallest);
                    i = smallest;
                }
            }

            private void Swap(int a, int b)
            {
                double k = keys[a]; keys[a] = keys[b]; keys[b] = k;
                int v = values[a]; values[a] = values[b]; values[b] = v;
            }
        }
    }
}
