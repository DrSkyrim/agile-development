using System;
using System.Collections.Generic;

namespace UrbanGenesis.Simulation
{
    /// <summary>
    /// Road cost that grows with traffic: the free-flow cost times a slowdown that depends on how full the road is
    /// (vehicles on it divided by <see cref="Inspector.Capacity"/>). The economy routes every trip with this cost,
    /// so a jammed road makes jobs, shops and deliveries behind it count as further away, and the city earns and grows less.
    /// The fullness is smoothed over several updates so that single vehicles passing do not flicker the whole network.
    /// </summary>
    public sealed class CongestionRoadCost : IRoadCost
    {
        /// <summary>The slowdown never goes above this (a jammed road is slow, but never closed).</summary>
        public const double MaxSlowdown = 4.0;

        /// <summary>Extra cost at a completely full road: slowdown = 1 + Strength x fill squared (before the cap).</summary>
        public double Strength = 3.0;

        /// <summary>How much of the new reading counts per update (0 to 1). Lower is steadier.</summary>
        public double Smoothing = 0.35;

        private readonly FreeFlowRoadCost free = new FreeFlowRoadCost();
        private readonly Dictionary<int, double> fill = new Dictionary<int, double>();
        private readonly Dictionary<int, int> steps = new Dictionary<int, int>();

        /// <summary>Goes up whenever some road's slowdown moved by a visible step, so cached routes must be recomputed.</summary>
        public int Revision { get; private set; }

        /// <summary>What the average vehicle on the road experiences: 1 = free flow, 2 = trips take twice as long.</summary>
        public double AverageSlowdown { get; private set; }

        public CongestionRoadCost()
        {
            AverageSlowdown = 1.0;
        }

        /// <summary>The smoothed fullness of a road (0 = empty, 1 = packed, can go above 1 in a queue).</summary>
        public double Fill(int segmentId)
        {
            double f;
            return fill.TryGetValue(segmentId, out f) ? f : 0.0;
        }

        public double Slowdown(int segmentId)
        {
            double f = Fill(segmentId);
            return Math.Min(MaxSlowdown, 1.0 + Strength * f * f);
        }

        public double Cost(RoadSegment segment)
        {
            double cost = free.Cost(segment);
            return double.IsInfinity(cost) ? cost : cost * Slowdown(segment.Id);
        }

        public void Clear()
        {
            fill.Clear();
            steps.Clear();
            AverageSlowdown = 1.0;
            Revision++;
        }

        /// <summary>Takes a new reading: the number of vehicles on every road (roads missing from <paramref name="load"/> are empty).</summary>
        public void Update(IEnumerable<RoadSegment> segments, IDictionary<int, int> load)
        {
            bool changed = false;
            double weighted = 0.0;
            double vehicles = 0.0;
            var seen = new HashSet<int>();
            foreach (RoadSegment segment in segments)
            {
                seen.Add(segment.Id);
                int count;
                load.TryGetValue(segment.Id, out count);
                double reading = count / Inspector.Capacity(segment);
                double old = Fill(segment.Id);
                double now = old + (reading - old) * Smoothing;
                if (now < 0.005) { now = 0.0; }
                if (now == 0.0) { fill.Remove(segment.Id); } else { fill[segment.Id] = now; }

                // Four steps per whole unit of slowdown: smaller moves do not count as a change.
                int step = (int)Math.Round(Slowdown(segment.Id) * 4.0);
                int before;
                steps.TryGetValue(segment.Id, out before);
                if (before == 0) { before = 4; }
                if (step != before) { changed = true; }
                steps[segment.Id] = step;

                weighted += count * Slowdown(segment.Id);
                vehicles += count;
            }

            // Forget roads that are gone.
            var gone = new List<int>();
            foreach (int id in fill.Keys) { if (!seen.Contains(id)) { gone.Add(id); } }
            foreach (int id in gone) { fill.Remove(id); steps.Remove(id); changed = true; }

            AverageSlowdown = vehicles > 0.0 ? weighted / vehicles : 1.0;
            if (changed) { Revision++; }
        }
    }
}
