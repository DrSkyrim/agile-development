using System.Collections.Generic;

namespace UrbanGenesis.Simulation
{
    /// <summary>
    /// Plain data for one building. A building can cover several cells; <see cref="Cell"/> is its
    /// lowest-X, lowest-Y corner. The Unity layer reads this to decide what to draw; nothing in here knows about Unity.
    /// </summary>
    public sealed class Building
    {
        public int Id { get; private set; }

        /// <summary>The footprint cell with the smallest X and Y.</summary>
        public GridCoord Cell { get; private set; }

        public ZoneType Zone { get; private set; }
        public BuildingTemplate Template { get; private set; }

        /// <summary>
        /// Quarter turns (0 to 3) the template was rotated by. At 0 the building's "front" faces towards
        /// lower Y (south); 1 faces lower X, 2 faces higher Y, 3 faces higher X.
        /// </summary>
        public int Rotation { get; private set; }

        public int Floors { get; private set; }

        /// <summary>A number the Unity layer uses to vary small details (roof direction, chimneys, ...) of otherwise identical buildings.</summary>
        public int Variant { get; private set; }

        public BuildingShape Shape { get { return Template.Shape; } }

        /// <summary>Footprint size in cells after rotation.</summary>
        public int Width { get { return Rotation % 2 == 0 ? Template.Width : Template.Depth; } }
        public int Depth { get { return Rotation % 2 == 0 ? Template.Depth : Template.Width; } }

        public Building(int id, GridCoord cell, BuildingTemplate template, int rotation, int floors, int variant)
        {
            Id = id;
            Cell = cell;
            Template = template;
            Zone = template.Zone;
            Rotation = ((rotation % 4) + 4) % 4;
            Floors = floors;
            Variant = variant;
        }

        /// <summary>Every cell the building covers.</summary>
        public IEnumerable<GridCoord> Cells()
        {
            for (int dy = 0; dy < Depth; dy++)
            {
                for (int dx = 0; dx < Width; dx++)
                {
                    yield return Cell.Offset(dx, dy);
                }
            }
        }
    }
}
