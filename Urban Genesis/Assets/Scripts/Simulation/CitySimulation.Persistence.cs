using System;
using System.Collections.Generic;

namespace UrbanGenesis.Simulation
{
    public sealed partial class CitySimulation
    {
        /// <summary>How close (metres) a saved lot centre must be to a lot of the rebuilt city to count as the same lot.</summary>
        private const double SavedLotMatch = 1.0;

        /// <summary>Takes a copy of the city's state that <see cref="Restore"/> can rebuild.</summary>
        public CitySnapshot Capture()
        {
            var s = new CitySnapshot();
            s.WorldSize = Roads.WorldBounds.MaxX * 2.0;
            s.Seed = rng.Next();
            s.Tick = TickCount;
            s.SpawnChance = SpawnChance;
            s.GrowthAttempts = GrowthAttemptsPerZonePerTick;
            s.BigBuildingPriority = BigBuildingPriority;
            s.DemandDrivesGrowth = Economy.DemandDrivesGrowth;
            s.MoneyEnabled = Treasury.IsEnabled;
            s.Balance = Treasury.Balance;
            s.TicksIntoDay = Treasury.TicksIntoDay;
            s.NextNodeId = Roads.NextNodeId;
            s.NextSegmentId = Roads.NextSegmentId;
            s.NextBuildingId = nextBuildingId;

            foreach (RoadNode node in Roads.Nodes)
            {
                s.Nodes.Add(new CitySnapshot.NodeData { Id = node.Id, X = node.Position.X, Y = node.Position.Y });
            }

            foreach (RoadSegment seg in Roads.Segments)
            {
                s.Segments.Add(new CitySnapshot.SegmentData
                {
                    Id = seg.Id,
                    A = seg.A.Id,
                    B = seg.B.Id,
                    P0X = seg.Curve.P0.X, P0Y = seg.Curve.P0.Y,
                    P1X = seg.Curve.P1.X, P1Y = seg.Curve.P1.Y,
                    P2X = seg.Curve.P2.X, P2Y = seg.Curve.P2.Y,
                    Type = (int)seg.Type,
                    Open = seg.OpenFraction,
                    Outside = seg.IsOutsideConnection
                });
            }

            foreach (Lot lot in Lots.Lots)
            {
                if (lot.Zone != ZoneType.None)
                {
                    s.Zones.Add(new CitySnapshot.ZoneData { X = lot.Center.X, Y = lot.Center.Y, Zone = (int)lot.Zone });
                }
            }

            foreach (Building b in buildings.Values)
            {
                var data = new CitySnapshot.BuildingData
                {
                    Id = b.Id,
                    Shape = (int)b.Shape,
                    Width = b.Template.Width,
                    Depth = b.Template.Depth,
                    Floors = b.Floors,
                    Variant = b.Variant,
                    CX = b.Center.X, CY = b.Center.Y,
                    BX = b.Back.X, BY = b.Back.Y
                };
                for (int i = 0; i < b.Lots.Count; i++)
                {
                    data.LotCentres.Add(b.Lots[i].Center.X);
                    data.LotCentres.Add(b.Lots[i].Center.Y);
                }

                s.Buildings.Add(data);
            }

            return s;
        }

        /// <summary>
        /// Rebuilds a city from a snapshot. Zones or buildings that no longer fit (for example if the lot rules changed
        /// since the save) are dropped; <paramref name="skipped"/> says how many.
        /// </summary>
        public static CitySimulation Restore(CitySnapshot s, out int skipped)
        {
            if (s == null)
            {
                throw new ArgumentNullException("s");
            }

            if (s.Version > CitySnapshot.CurrentVersion)
            {
                throw new InvalidOperationException("This save was made by a newer version of the game.");
            }

            skipped = 0;
            var city = new CitySimulation(s.WorldSize, s.Seed);
            city.SpawnChance = s.SpawnChance;
            city.GrowthAttemptsPerZonePerTick = s.GrowthAttempts;
            city.BigBuildingPriority = s.BigBuildingPriority;
            city.Economy.DemandDrivesGrowth = s.DemandDrivesGrowth;
            city.TickCount = s.Tick;

            // Roads first: their lots appear as each segment is added.
            foreach (CitySnapshot.NodeData n in s.Nodes)
            {
                city.Roads.RestoreNode(n.Id, new Vec2(n.X, n.Y));
            }

            var segments = new List<CitySnapshot.SegmentData>(s.Segments);
            segments.Sort((a, b) => a.Id.CompareTo(b.Id));
            foreach (CitySnapshot.SegmentData d in segments)
            {
                var curve = new RoadCurve(new Vec2(d.P0X, d.P0Y), new Vec2(d.P1X, d.P1Y), new Vec2(d.P2X, d.P2Y));
                RoadSegment seg = city.Roads.RestoreSegment(d.Id, d.A, d.B, curve, (RoadType)d.Type, d.Open, d.Outside);
                if (d.Outside)
                {
                    city.outsideConnections.Add(new OutsideConnection { Segment = seg });
                }
            }

            city.Roads.NextNodeId = s.NextNodeId;
            city.Roads.NextSegmentId = s.NextSegmentId;

            foreach (CitySnapshot.ZoneData z in s.Zones)
            {
                Lot lot = city.SavedLotAt(z.X, z.Y);
                if (lot == null)
                {
                    skipped++;
                    continue;
                }

                city.SetZone(new List<Lot> { lot }, (ZoneType)z.Zone);
            }

            foreach (CitySnapshot.BuildingData d in s.Buildings)
            {
                if (!city.RestoreBuilding(d))
                {
                    skipped++;
                }
            }

            city.nextBuildingId = Math.Max(city.nextBuildingId, s.NextBuildingId);

            if (s.MoneyEnabled)
            {
                city.Treasury.Enable(s.Balance);
                city.Treasury.TicksIntoDay = s.TicksIntoDay;
            }

            city.Economy.Update();
            city.Treasury.Recompute();
            city.Revision++;
            return city;
        }

        private Lot SavedLotAt(double x, double y)
        {
            var p = new Vec2(x, y);
            Lot best = null;
            double bestDistance = SavedLotMatch * SavedLotMatch;
            foreach (Lot lot in Lots.LotsInCircle(p, SavedLotMatch))
            {
                double d = (lot.Center - p).LengthSquared;
                if (d <= bestDistance)
                {
                    bestDistance = d;
                    best = lot;
                }
            }

            return best;
        }

        private bool RestoreBuilding(CitySnapshot.BuildingData d)
        {
            BuildingTemplate template = null;
            foreach (BuildingTemplate t in BuildingCatalog.All())
            {
                if ((int)t.Shape == d.Shape && t.Width == d.Width && t.Depth == d.Depth)
                {
                    template = t;
                    break;
                }
            }

            if (template == null)
            {
                return false;
            }

            var lots = new List<Lot>();
            for (int i = 0; i + 1 < d.LotCentres.Count; i += 2)
            {
                Lot lot = SavedLotAt(d.LotCentres[i], d.LotCentres[i + 1]);
                if (lot == null || lot.Building != null)
                {
                    return false;
                }

                lots.Add(lot);
            }

            if (lots.Count == 0)
            {
                return false;
            }

            AddBuilding(new Building(d.Id, template, d.Floors, d.Variant, new Vec2(d.CX, d.CY), new Vec2(d.BX, d.BY), lots));
            return true;
        }
    }
}
