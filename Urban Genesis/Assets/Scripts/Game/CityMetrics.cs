using UnityEngine;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Game
{
    /// <summary>
    /// How grid cells map to the 3D world. One cell is <see cref="CellSize"/> world units wide
    /// (1 unit = 1 metre, matching Blender's default). The grid's corner (0, 0) sits at the world origin,
    /// grid X runs along world X and grid Y runs along world Z.
    /// </summary>
    public static class CityMetrics
    {
        public const float CellSize = 8f;

        /// <summary>World position of the centre of a cell, at ground level.</summary>
        public static Vector3 CellCenter(GridCoord c)
        {
            return new Vector3((c.X + 0.5f) * CellSize, 0f, (c.Y + 0.5f) * CellSize);
        }

        /// <summary>World position of the lower-left (minimum X/Z) corner of a cell.</summary>
        public static Vector3 CellCorner(GridCoord c)
        {
            return new Vector3(c.X * CellSize, 0f, c.Y * CellSize);
        }

        /// <summary>The cell that contains a world position. May be outside the grid; check with CityGrid.InBounds.</summary>
        public static GridCoord WorldToCell(Vector3 world)
        {
            return new GridCoord(Mathf.FloorToInt(world.x / CellSize), Mathf.FloorToInt(world.z / CellSize));
        }
    }
}
