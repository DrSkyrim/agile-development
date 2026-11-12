namespace UrbanGenesis.Simulation
{
    /// <summary>The kinds of road. Each has its own width, rules and costs (see <see cref="RoadSpecs"/>).</summary>
    public enum RoadType
    {
        /// <summary>The ordinary road: 16 m wide, buildings stand along it, junctions anywhere.</summary>
        Street,

        /// <summary>A wide, fast road with no buildings beside it. It only connects at its ends (no junctions along it).</summary>
        Highway
    }

    /// <summary>The numbers and rules that differ between road types.</summary>
    public static class RoadSpecs
    {
        /// <summary>The widest road, for searching around roads without knowing their type.</summary>
        public const double MaxWidth = 24.0;
        public const double MaxHalfWidth = MaxWidth / 2.0;

        public static string Name(RoadType type)
        {
            return type == RoadType.Highway ? "Highway" : "Street";
        }

        public static double Width(RoadType type)
        {
            return type == RoadType.Highway ? 24.0 : 16.0;
        }

        public static double HalfWidth(RoadType type)
        {
            return Width(type) * 0.5;
        }

        /// <summary>Whether lots (and so zones and buildings) exist beside this kind of road.</summary>
        public static bool HasLots(RoadType type)
        {
            return type == RoadType.Street;
        }

        /// <summary>Whether another road may end in the middle of this one (making a junction there).</summary>
        public static bool AllowsJunctionsAlong(RoadType type)
        {
            return type == RoadType.Street;
        }

        /// <summary>How fast traffic moves compared with a street. Used by route costs.</summary>
        public static double SpeedFactor(RoadType type)
        {
            return type == RoadType.Highway ? 2.0 : 1.0;
        }

        public static double MinLength(RoadType type)
        {
            return type == RoadType.Highway ? 100.0 : RoadNetwork.MinRoadLength;
        }

        public static double MinRadius(RoadType type)
        {
            return type == RoadType.Highway ? 150.0 : RoadNetwork.MinCurveRadius;
        }

        public static float BaseCapacity(RoadType type)
        {
            return type == RoadType.Highway ? 3f : 1f;
        }
    }
}
