using System;
using System.Collections.Generic;

namespace UrbanGenesis.Simulation.Traffic
{
    /// <summary>
    /// Finds the quickest drivable route between two points on the road network. A point is a road segment plus a distance along
    /// it from end A. The search runs over directed roads (a road driven forwards or backwards), so one-way roads, banned turns
    /// and closed roads are all respected, and a vehicle may turn around at a dead end. Cost is travel time at free flow.
    /// </summary>
    public sealed class RoutePlanner
    {
        private struct Entry
        {
            public double Cost;
            public int Key;
        }

        private const int FinalFlag = 1 << 30;

        private readonly RoadNetwork roads;
        private readonly Dictionary<int, double> best = new Dictionary<int, double>();
        private readonly Dictionary<int, int> previous = new Dictionary<int, int>();
        private readonly List<Entry> heap = new List<Entry>();

        public RoutePlanner(RoadNetwork roads)
        {
            this.roads = roads;
        }

        private static int KeyOf(RoadSegment segment, bool forward) { return segment.Id * 2 + (forward ? 0 : 1); }

        private static double TimeCost(RoadSegment segment, double metres)
        {
            return metres / RoadSpecs.SpeedFactor(segment.Type);
        }

        private static bool Drivable(RoadSegment segment, bool forward)
        {
            return !segment.IsClosed && (forward || !RoadSpecs.IsOneWay(segment.Type));
        }

        /// <summary>The route, or null if the destination cannot be reached. The first step starts at <paramref name="fromS"/>, the last ends at <paramref name="toS"/>.</summary>
        public List<RouteStep> Plan(RoadSegment from, double fromS, RoadSegment to, double toS)
        {
            best.Clear();
            previous.Clear();
            heap.Clear();

            double bestTotal = double.MaxValue;
            int bestEnd = -1;
            bool direct = false;
            bool directForward = false;

            // Driving straight along the same road.
            if (from == to)
            {
                if (toS >= fromS && Drivable(from, true)) { bestTotal = TimeCost(from, toS - fromS); direct = true; directForward = true; }
                else if (toS < fromS && Drivable(from, false)) { bestTotal = TimeCost(from, fromS - toS); direct = true; directForward = false; }
            }

            for (int d = 0; d < 2; d++)
            {
                bool forward = d == 0;
                if (!Drivable(from, forward))
                {
                    continue;
                }

                double metres = forward ? from.Length - fromS : fromS;
                Push(KeyOf(from, forward), TimeCost(from, metres), -1);
            }

            while (heap.Count > 0)
            {
                Entry top = Pop();
                double known;
                if (!best.TryGetValue(top.Key, out known) || top.Cost > known)
                {
                    continue;
                }

                if (top.Cost >= bestTotal)
                {
                    break;
                }

                RoadSegment segment = roads.GetSegment(top.Key / 2);
                bool forward = top.Key % 2 == 0;
                if (segment == null)
                {
                    continue;
                }

                RoadNode node = forward ? segment.B : segment.A;
                for (int i = 0; i < node.Segments.Count; i++)
                {
                    RoadSegment next = node.Segments[i];
                    bool nextForward = next.A == node;
                    if (next == segment)
                    {
                        if (node.Degree != 1 || next.A == next.B) { continue; }
                        nextForward = !forward;
                    }
                    else if (!node.IsTurnPossible(segment, next))
                    {
                        continue;
                    }

                    if (!Drivable(next, nextForward))
                    {
                        continue;
                    }

                    int nextKey = KeyOf(next, nextForward);
                    if (next == to)
                    {
                        double enter = nextForward ? toS : next.Length - toS;
                        double total = top.Cost + TimeCost(next, enter);
                        if (total < bestTotal)
                        {
                            bestTotal = total;
                            bestEnd = nextKey | FinalFlag;
                            direct = false;
                            previous[bestEnd] = top.Key;
                        }

                        continue;
                    }

                    Push(nextKey, top.Cost + TimeCost(next, next.Length), top.Key);
                }
            }

            if (direct)
            {
                return new List<RouteStep> { new RouteStep { Segment = from, Forward = directForward } };
            }

            if (bestEnd < 0)
            {
                return null;
            }

            var keys = new List<int>();
            int at = bestEnd;
            while (at >= 0)
            {
                keys.Add(at & ~FinalFlag);
                int p;
                at = previous.TryGetValue(at, out p) ? p : -1;
            }

            keys.Reverse();
            var route = new List<RouteStep>(keys.Count);
            for (int i = 0; i < keys.Count; i++)
            {
                RoadSegment seg = roads.GetSegment(keys[i] / 2);
                if (seg == null)
                {
                    return null;
                }

                route.Add(new RouteStep { Segment = seg, Forward = keys[i] % 2 == 0 });
            }

            return route;
        }

        private void Push(int key, double cost, int from)
        {
            double known;
            if (best.TryGetValue(key, out known) && known <= cost)
            {
                return;
            }

            best[key] = cost;
            previous[key] = from;
            heap.Add(new Entry { Cost = cost, Key = key });
            int i = heap.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (heap[parent].Cost <= heap[i].Cost) { break; }
                Entry tmp = heap[parent]; heap[parent] = heap[i]; heap[i] = tmp;
                i = parent;
            }
        }

        private Entry Pop()
        {
            Entry top = heap[0];
            Entry last = heap[heap.Count - 1];
            heap.RemoveAt(heap.Count - 1);
            if (heap.Count > 0)
            {
                heap[0] = last;
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, m = i;
                    if (l < heap.Count && heap[l].Cost < heap[m].Cost) { m = l; }
                    if (r < heap.Count && heap[r].Cost < heap[m].Cost) { m = r; }
                    if (m == i) { break; }
                    Entry tmp = heap[m]; heap[m] = heap[i]; heap[i] = tmp;
                    i = m;
                }
            }

            return top;
        }
    }
}
