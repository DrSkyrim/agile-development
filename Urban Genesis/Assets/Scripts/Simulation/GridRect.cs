using System;
using System.Collections.Generic;

namespace UrbanGenesis.Simulation
{
    /// <summary>Axis-aligned rectangle of cells. Both corners are inclusive.</summary>
    public struct GridRect
    {
        public readonly int MinX;
        public readonly int MinY;
        public readonly int MaxX;
        public readonly int MaxY;

        public GridRect(int minX, int minY, int maxX, int maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        /// <summary>Builds the smallest rectangle containing both cells, whatever order they come in.</summary>
        public static GridRect FromCorners(GridCoord a, GridCoord b)
        {
            return new GridRect(
                Math.Min(a.X, b.X), Math.Min(a.Y, b.Y),
                Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
        }

        public int Width { get { return MaxX - MinX + 1; } }
        public int Height { get { return MaxY - MinY + 1; } }
        public int CellCount { get { return Width * Height; } }

        public bool Contains(GridCoord c)
        {
            return c.X >= MinX && c.X <= MaxX && c.Y >= MinY && c.Y <= MaxY;
        }

        public GridRect Expanded(int amount)
        {
            return new GridRect(MinX - amount, MinY - amount, MaxX + amount, MaxY + amount);
        }

        public IEnumerable<GridCoord> Cells()
        {
            for (int y = MinY; y <= MaxY; y++)
            {
                for (int x = MinX; x <= MaxX; x++)
                {
                    yield return new GridCoord(x, y);
                }
            }
        }
    }
}
