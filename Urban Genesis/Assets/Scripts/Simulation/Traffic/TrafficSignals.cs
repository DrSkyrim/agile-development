using System;
using System.Collections.Generic;

namespace UrbanGenesis.Simulation.Traffic
{
    public enum SignalState { Green, Amber, Red }

    /// <summary>
    /// Traffic light timing. Every lit junction cycles through its phases: the arms are sorted around the junction and an arm
    /// belongs to phase (index mod phases), so opposite arms of a four-way go together. Each phase is green, then amber, then
    /// a short all-red gap that lets the crossing clear. Pure function of time, so the game can draw the same lights.
    /// </summary>
    public sealed class TrafficSignals
    {
        public const double GreenSeconds = 7.0;
        public const double AmberSeconds = 1.5;
        public const double ClearSeconds = 1.5;
        public const double PhaseSeconds = GreenSeconds + AmberSeconds + ClearSeconds;

        private sealed class Plan
        {
            public List<RoadSegment> Arms;
            public int Phases;
            public double Offset;
        }

        private readonly CitySimulation city;
        private readonly Dictionary<int, Plan> plans = new Dictionary<int, Plan>();
        private int planRevision = -1;

        public TrafficSignals(CitySimulation city)
        {
            this.city = city;
        }

        /// <summary>The number of phases of a lit junction (2 for four-ways, otherwise one per arm).</summary>
        public static int PhaseCount(int arms)
        {
            return arms % 2 == 0 ? 2 : arms;
        }

        /// <summary>Arms of the junction in order around it (by direction leaving the junction).</summary>
        public static List<RoadSegment> SortedArms(RoadNode node)
        {
            var arms = new List<RoadSegment>(node.Segments);
            arms.Sort((x, y) => Angle(node, x).CompareTo(Angle(node, y)));
            return arms;
        }

        private static double Angle(RoadNode node, RoadSegment seg)
        {
            Vec2 away = seg.A == node ? seg.Curve.TangentAtLength(0.0) : seg.Curve.TangentAtLength(seg.Length) * -1.0;
            return Math.Atan2(away.Y, away.X);
        }

        /// <summary>Colour shown to traffic arriving along <paramref name="arm"/>. Green if the junction has no lights.</summary>
        public SignalState StateFor(RoadNode node, RoadSegment arm, double time)
        {
            if (!node.HasTrafficLight)
            {
                return SignalState.Green;
            }

            if (planRevision != city.Revision)
            {
                plans.Clear();
                planRevision = city.Revision;
            }

            Plan plan;
            if (!plans.TryGetValue(node.Id, out plan))
            {
                plan = new Plan();
                plan.Arms = SortedArms(node);
                plan.Phases = PhaseCount(plan.Arms.Count);
                plan.Offset = node.Id * 1.7;
                plans[node.Id] = plan;
            }

            int index = plan.Arms.IndexOf(arm);
            if (index < 0)
            {
                return SignalState.Red;
            }

            double t = (time + plan.Offset) % (PhaseSeconds * plan.Phases);
            int current = (int)(t / PhaseSeconds);
            if (current != index % plan.Phases)
            {
                return SignalState.Red;
            }

            double within = t - current * PhaseSeconds;
            return within < GreenSeconds ? SignalState.Green : within < GreenSeconds + AmberSeconds ? SignalState.Amber : SignalState.Red;
        }
    }
}
