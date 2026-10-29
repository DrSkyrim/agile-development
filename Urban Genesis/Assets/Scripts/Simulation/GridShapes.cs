using System;
using System.Collections.Generic;

namespace UrbanGenesis.Simulation
{
    /// <summary>Helpers that turn a drag (start cell, end cell) into the list of cells a tool affects.</summary>
    public static class GridShapes
    {
        /// <summary>
        /// Straight, axis-aligned line from <paramref name="start"/> towards <paramref name="end"/>.
        /// The line follows whichever axis the drag moved furthest along, so roads stay on the grid.
        /// Ties go to the horizontal axis.
        /// </summary>
        public static List<GridCoord> StraightLine(GridCoord start, GridCoord end)
        {
            var cells = new List<GridCoord>();
            int dx = end.X - start.X;
            int dy = end.Y - start.Y;

            if (Math.Abs(dx) >= Math.Abs(dy))
            {
                int step = dx >= 0 ? 1 : -1;
                for (int x = start.X; x != end.X + step; x += step)
                {
                    cells.Add(new GridCoord(x, start.Y));
                }
            }
            else
            {
                int step = dy >= 0 ? 1 : -1;
                for (int y = start.Y; y != end.Y + step; y += step)
                {
                    cells.Add(new GridCoord(start.X, y));
                }
            }

            return cells;
        }
    }
}
