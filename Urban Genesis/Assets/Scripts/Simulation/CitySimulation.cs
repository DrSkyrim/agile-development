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
    ///  * A building only exists on a zoned cell of the same type.
    /// </summary>
    public sealed class CitySimulation
    {
        /// <summary>How far (in cells, counted along the grid) a zone can be from the nearest road.</summary>
        public const int ZoneDepth = 4;

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
        /// (and removes its building). Passing <see cref="ZoneType.None"/> clears zoning.
        /// Returns how many cells changed.
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
        /// Clears everything (road, zone, building) in the rectangle. Removing roads can leave nearby
        /// zones without service; those zones are cleared too. Returns how many cells in the rectangle had something removed.
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

                SpawnBuilding(cell, zone);
            }
        }

        private void SpawnBuilding(GridCoord cell, ZoneType zone)
        {
            var building = new Building(nextBuildingId++, cell, zone, PickFloors(zone));
            buildings[building.Id] = building;
            Grid.SetBuildingId(cell, building.Id);

            Action<Building> spawned = BuildingSpawned;
            if (spawned != null)
            {
                spawned(building);
            }

            RaiseCellChanged(cell);
        }

        private int PickFloors(ZoneType zone)
        {
            switch (zone)
            {
                case ZoneType.Residential: return rng.Next(1, 4); // 1 to 3 floors
                case ZoneType.Commercial: return rng.Next(2, 5);  // 2 to 4 floors
                case ZoneType.Industrial: return rng.Next(1, 3);  // 1 to 2 floors
                default: return 1;
            }
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>Removes the building on a cell if there is one. Does not touch the zone. Returns true if one was removed.</summary>
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
            Grid.SetBuildingId(c, CityGrid.NoBuilding);

            Action<Building> removed = BuildingRemoved;
            if (removed != null && building != null)
            {
                removed(building);
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
