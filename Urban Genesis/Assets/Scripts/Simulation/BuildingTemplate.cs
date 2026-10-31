namespace UrbanGenesis.Simulation
{
    /// <summary>
    /// A blueprint the growth system can pick: what shape, how many cells it covers (before rotation),
    /// how tall it can be and how often it is chosen.
    /// </summary>
    public sealed class BuildingTemplate
    {
        public BuildingShape Shape { get; private set; }
        public ZoneType Zone { get; private set; }

        /// <summary>Footprint in cells when the building is not rotated.</summary>
        public int Width { get; private set; }
        public int Depth { get; private set; }

        public int MinFloors { get; private set; }
        public int MaxFloors { get; private set; }

        /// <summary>Relative chance of being picked. Only compared with other templates of the same zone.</summary>
        public double Weight { get; private set; }

        /// <summary>How many separate homes (houses or apartments) the building holds. Zero for non-residential buildings.</summary>
        public int Homes { get; private set; }

        public int CellCount { get { return Width * Depth; } }

        public BuildingTemplate(BuildingShape shape, ZoneType zone, int width, int depth, int minFloors, int maxFloors, double weight, int homes = 0)
        {
            Shape = shape;
            Zone = zone;
            Width = width;
            Depth = depth;
            MinFloors = minFloors;
            MaxFloors = maxFloors;
            Weight = weight;
            Homes = homes;
        }
    }
}
