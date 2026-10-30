namespace UrbanGenesis.Simulation
{
    /// <summary>What kind of solid a <see cref="BuildingPart"/> is.</summary>
    public enum PartKind
    {
        Box,
        Cylinder,
        Pyramid,    // square base, point on top (spires, silo caps)
        GableRoof   // triangular prism, ridge along X or Y (pitched roofs)
    }

    /// <summary>Which shade of the zone's gray a part is drawn in.</summary>
    public enum PartTone
    {
        Wall,   // the main colour
        Light,  // roofs and accents
        Dark    // plinths, doors, chimneys
    }

    /// <summary>
    /// One solid piece of a building model. Positions and sizes are in cells and floors, in the building's
    /// own frame: the origin is the middle of the footprint as it is before rotation, X runs across the
    /// width, Y runs from the front (negative) to the back (positive) and the front faces negative Y.
    /// The Unity layer turns these into meshes; nothing here knows about Unity.
    /// </summary>
    public sealed class BuildingPart
    {
        public PartKind Kind { get; private set; }

        /// <summary>Middle of the part, in cells from the middle of the footprint.</summary>
        public float CenterX { get; private set; }
        public float CenterY { get; private set; }

        /// <summary>Size in cells. For a cylinder both are the diameter.</summary>
        public float SizeX { get; private set; }
        public float SizeY { get; private set; }

        /// <summary>Height in floors.</summary>
        public float Height { get; private set; }

        /// <summary>How many floors above the ground the part's bottom is.</summary>
        public float Elevation { get; private set; }

        public PartTone Tone { get; private set; }

        /// <summary>Only used by <see cref="PartKind.GableRoof"/>: true if the ridge runs along X, false if along Y.</summary>
        public bool RidgeAlongX { get; private set; }

        public BuildingPart(PartKind kind, float centerX, float centerY, float sizeX, float sizeY,
            float height, float elevation, PartTone tone, bool ridgeAlongX)
        {
            Kind = kind;
            CenterX = centerX;
            CenterY = centerY;
            SizeX = sizeX;
            SizeY = sizeY;
            Height = height;
            Elevation = elevation;
            Tone = tone;
            RidgeAlongX = ridgeAlongX;
        }
    }
}
