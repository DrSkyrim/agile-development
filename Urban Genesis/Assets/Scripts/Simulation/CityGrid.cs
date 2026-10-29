using System;

namespace UrbanGenesis.Simulation
{
    /// <summary>
    /// Fixed-size grid that remembers which zone and which building (if any) each cell has.
    /// Roads are not stored here; they live in <see cref="RoadNetwork"/>.
    /// </summary>
    public sealed class CityGrid
    {
        public const int NoBuilding = 0;

        private readonly ZoneType[] zones;
        private readonly int[] buildingIds;

        public int Width { get; private set; }
        public int Height { get; private set; }

        public CityGrid(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException("Grid size must be positive.");
            }

            Width = width;
            Height = height;
            zones = new ZoneType[width * height];
            buildingIds = new int[width * height];
        }

        public bool InBounds(GridCoord c)
        {
            return c.X >= 0 && c.Y >= 0 && c.X < Width && c.Y < Height;
        }

        public ZoneType GetZone(GridCoord c)
        {
            return zones[Index(c)];
        }

        public void SetZone(GridCoord c, ZoneType zone)
        {
            zones[Index(c)] = zone;
        }

        public int GetBuildingId(GridCoord c)
        {
            return buildingIds[Index(c)];
        }

        public void SetBuildingId(GridCoord c, int buildingId)
        {
            buildingIds[Index(c)] = buildingId;
        }

        private int Index(GridCoord c)
        {
            if (!InBounds(c))
            {
                throw new ArgumentOutOfRangeException("c", "Cell " + c + " is outside the grid.");
            }

            return c.Y * Width + c.X;
        }
    }
}
