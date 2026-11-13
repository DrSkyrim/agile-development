namespace UrbanGenesis.Simulation
{
    /// <summary>The kinds of road. Each has its own width, lanes, rules and costs (see <see cref="RoadSpecs"/>). The numbers are saved, so only add to the end.</summary>
    public enum RoadType
    {
        /// <summary>The ordinary road: one lane each way (1+1), 16 m wide, buildings stand along it, junctions anywhere.</summary>
        Street = 0,

        /// <summary>A wide, fast road, two lanes each way (2+2) with a median, and no buildings beside it. It only connects at its ends.</summary>
        Highway = 1,

        /// <summary>A big city street, two lanes each way (2+2), 22 m wide. Buildings stand along it and junctions are allowed anywhere.</summary>
        Avenue = 2,

        /// <summary>A fast road with a single lane in one direction only (from where you started drawing to where you ended). No buildings, connects only at its ends.</summary>
        OneWayHighway = 3,

        /// <summary>A smaller highway: one lane each way (1+1), 20 m wide, a solid double centre line. No buildings, connects only at its ends.</summary>
        TwoLaneHighway = 4
    }

    /// <summary>The numbers and rules that differ between road types.</summary>
    public static class RoadSpecs
    {
        public static readonly RoadType[] All = { RoadType.Street, RoadType.Avenue, RoadType.TwoLaneHighway, RoadType.Highway, RoadType.OneWayHighway };

        /// <summary>The widest road, for searching around roads without knowing their type.</summary>
        public const double MaxWidth = 28.0;
        public const double MaxHalfWidth = MaxWidth / 2.0;

        public static string Name(RoadType type)
        {
            switch (type)
            {
                case RoadType.Avenue: return "Avenue";
                case RoadType.Highway:
                case RoadType.TwoLaneHighway: return "Highway";
                case RoadType.OneWayHighway: return "One-way highway";
                default: return "Street";
            }
        }

        /// <summary>The lane layout as players say it: "1+1", "2+2" or "1 way".</summary>
        public static string LaneLabel(RoadType type)
        {
            if (IsOneWay(type))
            {
                return "1 lane, one way";
            }

            int lanes = LanesPerDirection(type);
            return lanes + "+" + lanes;
        }

        public static int LanesPerDirection(RoadType type)
        {
            return type == RoadType.Avenue || type == RoadType.Highway ? 2 : 1;
        }

        /// <summary>One-way roads carry traffic only from segment end A to end B (the direction they were drawn in).</summary>
        public static bool IsOneWay(RoadType type)
        {
            return type == RoadType.OneWayHighway;
        }

        public static double Width(RoadType type)
        {
            switch (type)
            {
                case RoadType.Avenue: return 22.0;
                case RoadType.Highway: return 28.0;
                case RoadType.OneWayHighway: return 12.0;
                case RoadType.TwoLaneHighway: return 20.0;
                default: return 16.0;
            }
        }

        /// <summary>Half the width of the strip down the middle (median or double line) that separates the two directions.</summary>
        public static double MedianHalfWidth(RoadType type)
        {
            switch (type)
            {
                case RoadType.Highway: return 1.4;
                case RoadType.Avenue: return 0.6;
                case RoadType.TwoLaneHighway: return 0.7;
                case RoadType.OneWayHighway: return 0.0;
                default: return 0.35;
            }
        }

        /// <summary>
        /// How far from the road's centre line the middle of a lane is. Lane 0 is the outermost lane on its side (the one beside the kerb),
        /// higher numbers are closer to the middle. A one-way road's single lane is on the centre line.
        /// </summary>
        public static double LaneOffset(RoadType type, int lane)
        {
            if (IsOneWay(type))
            {
                return 0.0;
            }

            int lanes = LanesPerDirection(type);
            double median = MedianHalfWidth(type);
            double laneWidth = (HalfWidth(type) - median) / lanes;
            return HalfWidth(type) - (lane + 0.5) * laneWidth;
        }

        public static double HalfWidth(RoadType type)
        {
            return Width(type) * 0.5;
        }

        /// <summary>Whether lots (and so zones and buildings) exist beside this kind of road.</summary>
        public static bool HasLots(RoadType type)
        {
            return type == RoadType.Street || type == RoadType.Avenue;
        }

        /// <summary>Whether another road may end in the middle of this one (making a junction there).</summary>
        public static bool AllowsJunctionsAlong(RoadType type)
        {
            return HasLots(type);
        }

        /// <summary>The two-way highways (not the one-way kind, which is used for ramps).</summary>
        public static bool IsHighway(RoadType type)
        {
            return type == RoadType.Highway || type == RoadType.TwoLaneHighway;
        }

        /// <summary>
        /// Whether a road of type <paramref name="joining"/> may end in the middle of an existing road of type <paramref name="host"/>.
        /// Streets and avenues join each other anywhere. Any highway-class road (including one-way ramps) may end on the middle of a
        /// street, an avenue or a two-way highway. Streets and avenues cannot join a highway mid-way (they use its ends or a ramp),
        /// and nothing joins the middle of a one-way highway.
        /// </summary>
        public static bool CanJoinMidway(RoadType joining, RoadType host)
        {
            if (AllowsJunctionsAlong(joining) && AllowsJunctionsAlong(host))
            {
                return true;
            }

            return !HasLots(joining) && (IsHighway(host) || HasLots(host));
        }

        /// <summary>How fast traffic moves compared with a street. Used by route costs.</summary>
        public static double SpeedFactor(RoadType type)
        {
            switch (type)
            {
                case RoadType.Avenue: return 1.4;
                case RoadType.Highway: return 2.0;
                case RoadType.OneWayHighway: return 2.0;
                case RoadType.TwoLaneHighway: return 1.8;
                default: return 1.0;
            }
        }

        public static double MinLength(RoadType type)
        {
            if (type == RoadType.OneWayHighway)
            {
                return 50.0; // ramps and slip roads are short
            }

            return HasLots(type) ? RoadNetwork.MinRoadLength : 100.0;
        }

        public static double MinRadius(RoadType type)
        {
            return HasLots(type) ? (type == RoadType.Avenue ? 60.0 : RoadNetwork.MinCurveRadius) : 150.0;
        }

        public static float BaseCapacity(RoadType type)
        {
            switch (type)
            {
                case RoadType.Avenue: return 2f;
                case RoadType.Highway: return 3f;
                case RoadType.OneWayHighway: return 1.5f;
                case RoadType.TwoLaneHighway: return 2f;
                default: return 1f;
            }
        }
    }
}
