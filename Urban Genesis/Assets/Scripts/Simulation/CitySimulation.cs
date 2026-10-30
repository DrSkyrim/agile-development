using System;
using System.Collections.Generic;

namespace UrbanGenesis.Simulation
{
    /// <summary>
    /// The whole city model: grid, roads, zones and buildings. It has no idea Unity exists.
    /// The Unity layer sends it commands (PlaceRoad, PaintZone, Bulldoze), calls <see cref="Step"/>
    /// on a fixed timer, and listens to the events to update what is on screen.
    ///
    /// Rules the model keeps true at all times:
    ///  * A road cell never has a zone or a building on it.
    ///  * A zoned cell is always "served", meaning a road is within <see cref="ZoneDepth"/> cells.
    ///  * A building covers one or more cells, and every cell it covers is zoned for the building's type.
    ///  * Buildings never overlap.
    /// </summary>
    public sealed class CitySimulation
    {
        /// <summary>How far (in cells, counted along the grid) a zone can be from the nearest road.</summary>
        public const int ZoneDepth = 4;

        /// <summary>How many random templates growth tries on one spot before settling for a 1x1 building.</summary>
        private const int TemplateTries = 4;

        /// <summary>How far in front of a building (in cells) growth looks for a road when deciding which way it faces.</summary>
        private const int FacingScanDistance = 8;

        private static readonly int[] FrontDx = { 0, -1, 0, 1 };
        private static readonly int[] FrontDy = { -1, 0, 1, 0 };

        private readonly Dictionary<int, Building> buildings = new Dictionary<int, Building>();
        private readonly List<GridCoord> candidates = new List<GridCoord>();
        private readonly Random rng;
        private int nextBuildingId = 1;

        public CityGrid Grid { get; private set; }
        public RoadNetwork Roads { get; private set; }

        /// <summary>Number of simulation ticks run so far.</summary>
        public int TickCount { get; private set; }

        /// <summary>Chance (0 to 1) that each growth attempt on a tick actually places a building.</summary>
        public double SpawnChance { get; set; }

        /// <summary>How many growth attempts each zone type gets per tick.</summary>
        public int GrowthAttemptsPerZonePerTick { get; set; }

        /// <summary>Raised when anything about one cell changes (road, zone or building).</summary>
        public event Action<GridCoord> CellChanged;
        public event Action<Building> BuildingSpawned;
        public event Action<Building> BuildingRemoved;

        public CitySimulation(int width, int height, int seed)
        {
            Grid = new CityGrid(width, height);
            Roads = new RoadNetwork();
            rng = new Random(seed);
            SpawnChance = 0.5;
            GrowthAttemptsPerZonePerTick = 2;
        }

        public int BuildingCount { get { return buildings.Count; } }
        public IEnumerable<Building> Buildings { get { return buildings.Values; } }

        public int CountBuildings(ZoneType zone)
        {
            int count = 0;
            foreach (Building b in buildings.Values)
            {
                if (b.Zone == zone)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>Finds the building covering a cell. Works for any of the cells a larger building covers.</summary>
        public bool TryGetBuildingAt(GridCoord c, out Building building)
        {
            building = null;
            if (!Grid.InBounds(c))
            {
                return false;
            }

            int id = Grid.GetBuildingId(c);
            return id != CityGrid.NoBuilding && buildings.TryGetValue(id, out building);
        }

        // ---------------------------------------------------------------- queries

        public bool IsRoad(GridCoord c)
        {
            return Roads.HasNode(c);
        }

        /// <summary>True if a road lies within <see cref="ZoneDepth"/> cells of <paramref name="c"/>.</summary>
        public bool IsServed(GridCoord c)
        {
            for (int dy = -ZoneDepth; dy <= ZoneDepth; dy++)
            {
                int remaining = ZoneDepth - Math.Abs(dy);
                for (int dx = -remaining; dx <= remaining; dx++)
                {
                    if (Roads.HasNode(c.Offset(dx, dy)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool CanPlaceRoad(GridCoord c)
        {
            return Grid.InBounds(c)
                && !Roads.HasNode(c)
                && Grid.GetBuildingId(c) == CityGrid.NoBuilding;
        }

        public bool CanZone(GridCoord c)
        {
            return Grid.InBounds(c) && !Roads.HasNode(c) && IsServed(c);
        }

        // ---------------------------------------------------------------- commands

        /// <summary>Places a road on every valid cell in the list. Returns how many roads were actually placed.</summary>
        public int PlaceRoad(IEnumerable<GridCoord> cells)
        {
            int placed = 0;
            foreach (GridCoord c in cells)
            {
                if (!CanPlaceRoad(c))
                {
                    continue;
                }

                // Building a road over empty zoned land simply removes the zoning.
                if (Grid.GetZone(c) != ZoneType.None)
                {
                    Grid.SetZone(c, ZoneType.None);
                }

                Roads.AddNode(c);
                placed++;
                RaiseCellChanged(c);
            }

            return placed;
        }

        /// <summary>
        /// Sets the zone of every valid cell in the rectangle. Zoning over a different zone replaces it
        /// (and removes the building on it, even if that building also covers cells outside the rectangle).
        /// Passing <see cref="ZoneType.None"/> clears zoning. Returns how many cells changed.
        /// </summary>
        public int PaintZone(GridRect rect, ZoneType zone)
        {
            int changed = 0;
            foreach (GridCoord c in ClipToGrid(rect).Cells())
            {
                if (Roads.HasNode(c))
                {
                    continue;
                }

                if (zone != ZoneType.None && !IsServed(c))
                {
                    continue;
                }

                if (Grid.GetZone(c) == zone)
                {
                    continue;
                }

                RemoveBuildingAt(c);
                Grid.SetZone(c, zone);
                changed++;
                RaiseCellChanged(c);
            }

            return changed;
        }

        /// <summary>
        /// Clears everything (road, zone, building) in the rectangle. A building that is only partly inside
        /// the rectangle is removed completely, but only the cells inside the rectangle lose their zone.
        /// Removing roads can leave nearby zones without service; those zones are cleared too.
        /// Returns how many cells in the rectangle had something removed.
        /// </summary>
        public int Bulldoze(GridRect rect)
        {
            GridRect area = ClipToGrid(rect);
            int changed = 0;
            bool roadsRemoved = false;

            foreach (GridCoord c in area.Cells())
            {
                bool touched = RemoveBuildingAt(c);

                if (Grid.GetZone(c) != ZoneType.None)
                {
                    Grid.SetZone(c, ZoneType.None);
                    touched = true;
                }

                if (Roads.RemoveNode(c))
                {
                    touched = true;
                    roadsRemoved = true;
                }

                if (touched)
                {
                    changed++;
                    RaiseCellChanged(c);
                }
            }

            if (roadsRemoved)
            {
                ClearUnservedZones(area.Expanded(ZoneDepth));
            }

            return changed;
        }

        // ---------------------------------------------------------------- simulation

        /// <summary>Advances the city by one tick.</summary>
        public void Step()
        {
            TickCount++;
            GrowZone(ZoneType.Residential);
            GrowZone(ZoneType.Commercial);
            GrowZone(ZoneType.Industrial);
        }

        private void GrowZone(ZoneType zone)
        {
            candidates.Clear();
            for (int y = 0; y < Grid.Height; y++)
            {
                for (int x = 0; x < Grid.Width; x++)
                {
                    var c = new GridCoord(x, y);
                    if (Grid.GetZone(c) == zone && Grid.GetBuildingId(c) == CityGrid.NoBuilding)
                    {
                        candidates.Add(c);
                    }
                }
            }

            for (int attempt = 0; attempt < GrowthAttemptsPerZonePerTick && candidates.Count > 0; attempt++)
            {
                if (rng.NextDouble() >= SpawnChance)
                {
                    continue;
                }

                int index = rng.Next(candidates.Count);
                GridCoord cell = candidates[index];
                candidates[index] = candidates[candidates.Count - 1];
                candidates.RemoveAt(candidates.Count - 1);

                // A bigger building placed earlier this tick may already cover this cell.
                if (Grid.GetBuildingId(cell) != CityGrid.NoBuilding)
                {
                    continue;
                }

                GrowAt(cell, zone);
            }
        }

        /// <summary>
        /// Grows one building that covers <paramref name="cell"/>. It tries a few random templates and
        /// settles for a 1x1 building if none of them fit around the cell.
        /// </summary>
        private void GrowAt(GridCoord cell, ZoneType zone)
        {
            for (int tries = 0; tries < TemplateTries; tries++)
            {
                BuildingTemplate template = BuildingCatalog.Pick(zone, rng, false);
                if (TryPlace(template, cell))
                {
                    return;
                }
            }

            TryPlace(BuildingCatalog.Pick(zone, rng, true), cell);
        }

        /// <summary>
        /// Looks at every way of laying the template over <paramref name="seed"/> (four rotations, and every
        /// position that still covers the seed cell) and builds on the best one that fits. "Best" means the
        /// front of the building is closest to a road; ties are broken at random.
        /// </summary>
        private bool TryPlace(BuildingTemplate template, GridCoord seed)
        {
            bool found = false;
            GridCoord bestAnchor = seed;
            int bestRotation = 0;
            int bestScore = int.MaxValue;
            int ties = 0;

            for (int rotation = 0; rotation < 4; rotation++)
            {
                bool turned = rotation % 2 == 1;
                int width = turned ? template.Depth : template.Width;
                int depth = turned ? template.Width : template.Depth;

                for (int oy = 0; oy < depth; oy++)
                {
                    for (int ox = 0; ox < width; ox++)
                    {
                        GridCoord anchor = seed.Offset(-ox, -oy);
                        if (!FitsAt(anchor, width, depth, template.Zone))
                        {
                            continue;
                        }

                        int score = DistanceToRoadInFront(anchor, width, depth, rotation);
                        if (!found || score < bestScore)
                        {
                            found = true;
                            bestScore = score;
                            bestAnchor = anchor;
                            bestRotation = rotation;
                            ties = 1;
                        }
                        else if (score == bestScore)
                        {
                            ties++;
                            if (rng.Next(ties) == 0)
                            {
                                bestAnchor = anchor;
                                bestRotation = rotation;
                            }
                        }
                    }
                }
            }

            if (!found)
            {
                return false;
            }

            SpawnBuilding(template, bestAnchor, bestRotation);
            return true;
        }

        /// <summary>True if every cell of the rectangle is inside the grid, zoned for <paramref name="zone"/> and empty.</summary>
        private bool FitsAt(GridCoord anchor, int width, int depth, ZoneType zone)
        {
            for (int dy = 0; dy < depth; dy++)
            {
                for (int dx = 0; dx < width; dx++)
                {
                    GridCoord c = anchor.Offset(dx, dy);
                    if (!Grid.InBounds(c)
                        || Grid.GetZone(c) != zone
                        || Grid.GetBuildingId(c) != CityGrid.NoBuilding
                        || Roads.HasNode(c))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// How many cells you walk from the front edge of the footprint to reach a road, looking straight ahead
        /// across the whole width of the building. Returns a large number if there is none close by.
        /// </summary>
        private int DistanceToRoadInFront(GridCoord anchor, int width, int depth, int rotation)
        {
            int dx = FrontDx[rotation];
            int dy = FrontDy[rotation];

            for (int distance = 1; distance <= FacingScanDistance; distance++)
            {
                if (dy != 0)
                {
                    int y = dy < 0 ? anchor.Y - distance : anchor.Y + depth - 1 + distance;
                    for (int x = anchor.X; x < anchor.X + width; x++)
                    {
                        if (Roads.HasNode(new GridCoord(x, y)))
                        {
                            return distance;
                        }
                    }
                }
                else
                {
                    int x = dx < 0 ? anchor.X - distance : anchor.X + width - 1 + distance;
                    for (int y = anchor.Y; y < anchor.Y + depth; y++)
                    {
                        if (Roads.HasNode(new GridCoord(x, y)))
                        {
                            return distance;
                        }
                    }
                }
            }

            return FacingScanDistance + 1;
        }

        private void SpawnBuilding(BuildingTemplate template, GridCoord anchor, int rotation)
        {
            int floors = rng.Next(template.MinFloors, template.MaxFloors + 1);
            int variant = rng.Next();

            var building = new Building(nextBuildingId++, anchor, template, rotation, floors, variant);
            buildings[building.Id] = building;
            foreach (GridCoord c in building.Cells())
            {
                Grid.SetBuildingId(c, building.Id);
            }

            Action<Building> spawned = BuildingSpawned;
            if (spawned != null)
            {
                spawned(building);
            }

            foreach (GridCoord c in building.Cells())
            {
                RaiseCellChanged(c);
            }
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// Removes the building covering a cell, along with its hold on every other cell it covers.
        /// Does not touch any zone. Returns true if there was a building.
        /// </summary>
        private bool RemoveBuildingAt(GridCoord c)
        {
            int id = Grid.GetBuildingId(c);
            if (id == CityGrid.NoBuilding)
            {
                return false;
            }

            Building building;
            buildings.TryGetValue(id, out building);
            buildings.Remove(id);

            if (building == null)
            {
                Grid.SetBuildingId(c, CityGrid.NoBuilding);
                return true;
            }

            foreach (GridCoord covered in building.Cells())
            {
                Grid.SetBuildingId(covered, CityGrid.NoBuilding);
            }

            Action<Building> removed = BuildingRemoved;
            if (removed != null)
            {
                removed(building);
            }

            // The caller reports the cell it was working on; tell listeners about the others.
            foreach (GridCoord covered in building.Cells())
            {
                if (covered != c)
                {
                    RaiseCellChanged(covered);
                }
            }

            return true;
        }

        private void ClearUnservedZones(GridRect region)
        {
            foreach (GridCoord c in ClipToGrid(region).Cells())
            {
                if (Grid.GetZone(c) != ZoneType.None && !IsServed(c))
                {
                    RemoveBuildingAt(c);
                    Grid.SetZone(c, ZoneType.None);
                    RaiseCellChanged(c);
                }
            }
        }

        private GridRect ClipToGrid(GridRect rect)
        {
            return new GridRect(
                Math.Max(0, rect.MinX),
                Math.Max(0, rect.MinY),
                Math.Min(Grid.Width - 1, rect.MaxX),
                Math.Min(Grid.Height - 1, rect.MaxY));
        }

        private void RaiseCellChanged(GridCoord c)
        {
            Action<GridCoord> handler = CellChanged;
            if (handler != null)
            {
                handler(c);
            }
        }
    }
}
