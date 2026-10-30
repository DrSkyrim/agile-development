using System;
using System.Collections.Generic;
using NUnit.Framework;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Tests
{
    /// <summary>The rules that must hold for any city, however it was built.</summary>
    public static class CityChecks
    {
        public static void AssertAllRulesHold(CitySimulation city)
        {
            var seen = new HashSet<Building>();
            foreach (Building b in city.Buildings)
            {
                seen.Add(b);
                string what = b.Template.Shape + " " + b.Width + "x" + b.Depth + " #" + b.Id;

                // It stands on exactly the lots it should, and they know about it.
                Assert.AreEqual(b.Width * b.Depth, b.Lots.Count, what + " lot count");
                foreach (Lot lot in b.Lots)
                {
                    Assert.AreSame(b, lot.Building, what + " lot does not point back");
                    Assert.AreEqual(b.Zone, lot.Zone, what + " stands on a lot zoned for something else");
                    Assert.AreSame(lot, city.Lots.Get(lot.Key), what + " stands on a lot that no longer exists");
                }

                // Direct connection to a road: one row of its lots is on the road, and its front edge touches the road.
                bool roadside = false;
                foreach (Lot lot in b.Lots)
                {
                    roadside |= lot.Key.Row == 0;
                }

                Assert.IsTrue(roadside, what + " has no lot on the road");
                double gap = FrontGapToRoad(city, b);
                Assert.GreaterOrEqual(gap, -(CitySimulation.BuildingRoadTolerance + 0.1), what + " sticks into the road");
                Assert.LessOrEqual(gap, CitySimulation.RoadContactTolerance + 0.1, what + " does not touch the road (gap " + gap + ")");

                // Never on a road.
                foreach (RoadSegment s in city.Roads.SegmentsNear(b.Bounds, 0.0))
                {
                    foreach (Vec2[] piece in s.Surface)
                    {
                        Assert.IsFalse(Geometry.ConvexOverlap(b.Footprint, piece, CitySimulation.BuildingRoadTolerance + 0.1), what + " is on a road");
                    }
                }
            }

            // Never on top of each other.
            var list = new List<Building>(city.Buildings);
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    if (list[i].Bounds.Overlaps(list[j].Bounds))
                    {
                        Assert.IsFalse(Geometry.ConvexOverlap(list[i].Footprint, list[j].Footprint, CitySimulation.BuildingOverlapTolerance + 0.1),
                            "buildings #" + list[i].Id + " and #" + list[j].Id + " overlap");
                    }
                }
            }

            // Lots: not under any road, and every building a lot names is real.
            foreach (Lot lot in city.Lots.Lots)
            {
                Assert.IsNotNull(city.Roads.GetSegment(lot.Key.SegmentId), lot.Key + " belongs to a road that is gone");
                foreach (RoadSegment s in city.Roads.SegmentsNear(lot.Bounds, 0.0))
                {
                    if (s.Id == lot.Key.SegmentId)
                    {
                        continue;
                    }

                    foreach (Vec2[] piece in s.Surface)
                    {
                        Assert.IsFalse(Geometry.ConvexOverlap(lot.Corners, piece, LotLayout.RoadOverlapTolerance + 0.1), lot.Key + " is under a road");
                    }
                }

                if (lot.Building != null)
                {
                    Assert.IsTrue(seen.Contains(lot.Building), lot.Key + " names a building that does not exist");
                }
            }
        }

        /// <summary>Distance from the building's front edge to the nearest road edge (0 = touching).</summary>
        public static double FrontGapToRoad(CitySimulation city, Building b)
        {
            double nearest = double.MaxValue;
            for (int k = 0; k <= 8; k++)
            {
                Vec2 p = Vec2.Lerp(b.Footprint[0], b.Footprint[1], k / 8.0);
                foreach (RoadSegment s in city.Roads.SegmentsNear(new Aabb(p.X, p.Y, p.X, p.Y), 20.0))
                {
                    nearest = Math.Min(nearest, s.Curve.DistanceTo(p));
                }
            }

            return nearest - RoadNetwork.HalfWidth;
        }
    }

    [TestFixture]
    public class CitySimulationTests
    {
        private static readonly Vec2 West = new Vec2(-200, 0);
        private static readonly Vec2 East = new Vec2(200, 0);

        private static CitySimulation CityWithRoad(int seed)
        {
            var city = new CitySimulation(seed);
            Assert.IsNotNull(city.PlaceRoad(city.PlanRoad(West, null, East)));
            return city;
        }

        private static void Run(CitySimulation city, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                city.Step();
            }
        }

        /// <summary>Zones every lot of the road (all four rows, both sides).</summary>
        private static void ZoneEverything(CitySimulation city, ZoneType zone)
        {
            for (double x = -190; x <= 190; x += 16)
            {
                city.PaintZone(new Vec2(x, 0), 48, zone);
            }
        }

        // ---------------------------------------------------------------- zoning

        [Test]
        public void PaintZone_OnlyZonesLotsInTheBrush()
        {
            CitySimulation city = CityWithRoad(1);

            int changed = city.PaintZone(new Vec2(0, 12), 9, ZoneType.Residential);

            Assert.Greater(changed, 0);
            foreach (Lot lot in city.Lots.Lots)
            {
                bool inside = Vec2.Distance(lot.Center, new Vec2(0, 12)) <= 9;
                Assert.AreEqual(inside ? ZoneType.Residential : ZoneType.None, lot.Zone, lot.Key.ToString());
            }
        }

        [Test]
        public void PaintZone_OnEmptyGroundFarFromRoads_DoesNothing()
        {
            CitySimulation city = CityWithRoad(1);
            Assert.AreEqual(0, city.PaintZone(new Vec2(0, 300), 30, ZoneType.Residential));
        }

        [Test]
        public void PaintZone_None_ClearsZoning()
        {
            CitySimulation city = CityWithRoad(1);
            city.PaintZone(new Vec2(0, 12), 20, ZoneType.Commercial);

            city.PaintZone(new Vec2(0, 12), 30, ZoneType.None);

            foreach (Lot lot in city.Lots.Lots)
            {
                Assert.AreEqual(ZoneType.None, lot.Zone);
            }
        }

        [Test]
        public void ZoneReachesFourRowsFromTheRoad_AndNoFurther()
        {
            CitySimulation city = CityWithRoad(1);
            city.PaintZone(new Vec2(0, 0), 200, ZoneType.Residential);

            double farthest = 0.0;
            foreach (Lot lot in city.Lots.Lots)
            {
                if (lot.Zone == ZoneType.Residential)
                {
                    farthest = Math.Max(farthest, Math.Abs(lot.Center.Y));
                }
            }

            Assert.AreEqual(RoadNetwork.HalfWidth + LotLayout.Rows * RoadNetwork.LotSize - 4.0, farthest, 1e-6);
        }

        [Test]
        public void PaintingAnotherZoneOverABuilding_RemovesIt()
        {
            CitySimulation city = CityWithRoad(2);
            city.SpawnChance = 1.0;
            ZoneEverything(city, ZoneType.Residential);
            Run(city, 40);
            Assert.Greater(city.BuildingCount, 0);

            Building b = null;
            foreach (Building x in city.Buildings) { b = x; break; }
            int before = city.BuildingCount;
            city.PaintZone(b.Lots[0].Center, 1, ZoneType.Commercial);

            Assert.AreEqual(before - 1, city.BuildingCount);
            CityChecks.AssertAllRulesHold(city);
        }

        [Test]
        public void FillZone_ZonesTheWholeSideOfTheRoad_AllFourRows_AndNothingElse()
        {
            CitySimulation city = CityWithRoad(20);

            int changed = city.FillZone(new Vec2(-50, 12), ZoneType.Residential);

            Assert.AreEqual(50 * LotLayout.Rows, changed);
            foreach (Lot lot in city.Lots.Lots)
            {
                bool leftSide = lot.Key.Side == 0;
                Assert.AreEqual(leftSide ? ZoneType.Residential : ZoneType.None, lot.Zone, lot.Key.ToString());
            }
        }

        [Test]
        public void FillZone_StopsAtJunctions()
        {
            CitySimulation city = CityWithRoad(21);
            Assert.IsNotNull(city.PlaceRoad(city.PlanRoad(new Vec2(0, -150), null, new Vec2(0, 3))));

            // The main road is now two pieces. Filling the north side of the west piece must not touch the east piece.
            city.FillZone(new Vec2(-100, 12), ZoneType.Commercial);

            bool west = false;
            foreach (Lot lot in city.Lots.Lots)
            {
                if (lot.Zone == ZoneType.Commercial)
                {
                    west = true;
                    Assert.Less(lot.Center.X, 0.0, "the fill ran past the junction");
                }
            }

            Assert.IsTrue(west);
        }

        [Test]
        public void FillZone_ReplacesZoningAndBuildings_AndDoesNothingOffALot()
        {
            CitySimulation city = CityWithRoad(22);
            city.SpawnChance = 1.0;
            ZoneEverything(city, ZoneType.Residential);
            Run(city, 100);
            Assert.Greater(city.BuildingCount, 0);

            Assert.AreEqual(0, city.FillZone(new Vec2(0, 300), ZoneType.Industrial));
            Assert.Greater(city.FillZone(new Vec2(0, 12), ZoneType.Industrial), 0);

            foreach (Building b in city.Buildings)
            {
                foreach (Lot lot in b.Lots)
                {
                    Assert.AreEqual(b.Zone, lot.Zone);
                }
            }

            CityChecks.AssertAllRulesHold(city);
        }

        // ---------------------------------------------------------------- growth

        [Test]
        public void Growth_NeedsZoning()
        {
            CitySimulation city = CityWithRoad(3);
            city.SpawnChance = 1.0;
            Run(city, 100);
            Assert.AreEqual(0, city.BuildingCount);
        }

        [Test]
        public void Growth_FillsAZonedStrip_WithBuildingsOfTheRightType()
        {
            CitySimulation city = CityWithRoad(4);
            city.SpawnChance = 1.0;
            ZoneEverything(city, ZoneType.Commercial);

            Run(city, 400);

            Assert.Greater(city.BuildingCount, 20);
            foreach (Building b in city.Buildings)
            {
                Assert.AreEqual(ZoneType.Commercial, b.Zone);
            }

            CityChecks.AssertAllRulesHold(city);
        }

        [Test]
        public void Buildings_OnlySpawn_WhereTheyHaveADirectConnectionToTheRoad()
        {
            // Zone only the fourth row, 28 m from the road: nothing there touches the road, so nothing may be built.
            CitySimulation city = CityWithRoad(5);
            city.SpawnChance = 1.0;
            for (double x = -190; x <= 190; x += 6)
            {
                city.PaintZone(new Vec2(x, 36), 3.9, ZoneType.Residential);
            }

            int zoned = 0;
            foreach (Lot lot in city.Lots.Lots)
            {
                if (lot.Zone == ZoneType.Residential)
                {
                    Assert.AreEqual(3, lot.Key.Row);
                    zoned++;
                }
            }

            Assert.Greater(zoned, 20);
            Run(city, 300);
            Assert.AreEqual(0, city.BuildingCount, "a building appeared with no road connection");
        }

        [Test]
        public void EveryBuilding_TouchesTheRoad_AndFacesIt()
        {
            CitySimulation city = CityWithRoad(6);
            city.SpawnChance = 1.0;
            ZoneEverything(city, ZoneType.Residential);
            Run(city, 300);

            Assert.Greater(city.BuildingCount, 10);
            foreach (Building b in city.Buildings)
            {
                // The back of the building points away from the road, the front towards it.
                Vec2 front = (b.Footprint[0] + b.Footprint[1]) * 0.5;
                Vec2 back = (b.Footprint[2] + b.Footprint[3]) * 0.5;
                Assert.Greater(Math.Abs(back.Y), Math.Abs(front.Y), "building #" + b.Id + " faces away from the road");
                Assert.AreEqual(0.0, CityChecks.FrontGapToRoad(city, b), CitySimulation.RoadContactTolerance + 0.1);
            }

            CityChecks.AssertAllRulesHold(city);
        }

        [Test]
        public void BigBuildings_TakePriority_OnAFreshStrip()
        {
            CitySimulation city = CityWithRoad(7);
            city.SpawnChance = 1.0;
            city.BigBuildingPriority = 1.0;
            city.GrowthAttemptsPerZonePerTick = 1;
            ZoneEverything(city, ZoneType.Residential);

            int biggest = 0;
            foreach (BuildingTemplate t in BuildingCatalog.ForZone(ZoneType.Residential))
            {
                if (t.Depth <= LotLayout.Rows)
                {
                    biggest = Math.Max(biggest, t.CellCount);
                }
            }

            city.Step();

            Assert.AreEqual(1, city.BuildingCount);
            foreach (Building b in city.Buildings)
            {
                Assert.AreEqual(biggest, b.Template.CellCount, "the first building on an empty strip should be the biggest one");
            }
        }

        [Test]
        public void BigBuildings_TakePriority_AmongTheOnesThatFit()
        {
            // Only the first row is zoned, so nothing deeper than one lot can fit. The biggest one-row building must win.
            CitySimulation city = CityWithRoad(8);
            city.SpawnChance = 1.0;
            city.BigBuildingPriority = 1.0;
            city.GrowthAttemptsPerZonePerTick = 1;
            for (double x = -190; x <= 190; x += 6)
            {
                city.PaintZone(new Vec2(x, 12), 3.9, ZoneType.Residential);
            }

            int biggestOneRow = 0;
            foreach (BuildingTemplate t in BuildingCatalog.ForZone(ZoneType.Residential))
            {
                if (t.Depth == 1)
                {
                    biggestOneRow = Math.Max(biggestOneRow, t.CellCount);
                }
            }

            city.Step();

            Assert.AreEqual(1, city.BuildingCount);
            foreach (Building b in city.Buildings)
            {
                Assert.AreEqual(1, b.Depth);
                Assert.AreEqual(biggestOneRow, b.Template.CellCount);
            }
        }

        [Test]
        public void WithTheBiggestFirstRule_BigBuildingsAreCommon_AndSmallOnesFillTheGaps()
        {
            CitySimulation city = CityWithRoad(9);
            city.SpawnChance = 1.0;
            city.BigBuildingPriority = 1.0;
            ZoneEverything(city, ZoneType.Residential);
            Run(city, 600);

            int big = 0;
            int small = 0;
            foreach (Building b in city.Buildings)
            {
                if (b.Template.CellCount >= 6) { big++; } else { small++; }
            }

            Assert.Greater(big, 0, "no big buildings grew");
            Assert.Greater(small, 0, "nothing filled the gaps");
            CityChecks.AssertAllRulesHold(city);
        }

        [Test]
        public void Growth_FollowsACurvedRoad_AndKeepsTheRules()
        {
            var city = new CitySimulation(10);
            Vec2 a = new Vec2(-180, -40);
            Vec2 b = new Vec2(180, -40);
            RoadPlan plan = city.PlanRoad(a, RoadNetwork.ControlThrough(a, new Vec2(0, 90), b), b);
            Assert.IsTrue(plan.IsValid, plan.Error);
            city.PlaceRoad(plan);
            city.SpawnChance = 1.0;
            for (double x = -170; x <= 170; x += 20)
            {
                city.PaintZone(new Vec2(x, 20), 120, ZoneType.Commercial);
            }

            Run(city, 400);

            Assert.Greater(city.BuildingCount, 20);
            CityChecks.AssertAllRulesHold(city);
        }

        private static string Snapshot(int seed)
        {
            CitySimulation city = CityWithRoad(seed);
            city.SpawnChance = 1.0;
            ZoneEverything(city, ZoneType.Residential);
            Run(city, 150);
            var parts = new List<string>();
            foreach (Building b in city.Buildings)
            {
                parts.Add(b.Template.Shape + ":" + b.Width + "x" + b.Depth + "@" + b.Center + "f" + b.Floors);
            }

            parts.Sort();
            return string.Join("|", parts.ToArray());
        }

        [Test]
        public void Growth_IsRepeatable_ForTheSameSeed()
        {
            Assert.AreEqual(Snapshot(11), Snapshot(11));
            Assert.AreNotEqual(Snapshot(11), Snapshot(12));
        }

        // ---------------------------------------------------------------- roads and buildings

        [Test]
        public void ARoadThroughBuildings_IsRefused_UntilTheyAreBulldozed()
        {
            CitySimulation city = CityWithRoad(13);
            city.SpawnChance = 1.0;
            ZoneEverything(city, ZoneType.Residential);
            Run(city, 300);
            Assert.Greater(city.BuildingCount, 10);

            // A stub road joining the main road from the south runs straight through the buildings in front of it.
            RoadPlan through = city.PlanRoad(new Vec2(0, -120), null, new Vec2(0, 3));
            Assert.IsFalse(through.IsValid);
            StringAssert.Contains("buildings", through.Error);
            Assert.IsNull(city.PlaceRoad(through));

            for (int i = 0; i < 8; i++)
            {
                city.Bulldoze(new Vec2(0, -10 - i * 12), 14, false);
            }

            RoadPlan after = city.PlanRoad(new Vec2(0, -120), null, new Vec2(0, 3));
            Assert.IsTrue(after.IsValid, after.Error);
            Assert.IsNotNull(city.PlaceRoad(after));
            CityChecks.AssertAllRulesHold(city);
        }

        [Test]
        public void ATJunctionBuiltAwayFromBuildings_KeepsEveryBuilding()
        {
            CitySimulation city = CityWithRoad(14);
            city.SpawnChance = 1.0;
            for (double x = -190; x <= -100; x += 16)
            {
                city.PaintZone(new Vec2(x, 0), 48, ZoneType.Residential);
            }

            Run(city, 300);
            int before = city.BuildingCount;
            Assert.Greater(before, 5);

            RoadPlan plan = city.PlanRoad(new Vec2(100, -150), null, new Vec2(100, 3));
            Assert.IsTrue(plan.IsValid, plan.Error);
            city.PlaceRoad(plan);

            Assert.AreEqual(before, city.BuildingCount);
            CityChecks.AssertAllRulesHold(city);
        }

        [Test]
        public void GrowthContinues_AlongBothPiecesOfASplitRoad()
        {
            CitySimulation city = CityWithRoad(15);
            Assert.IsNotNull(city.PlaceRoad(city.PlanRoad(new Vec2(104, -150), null, new Vec2(104, 3))));
            city.SpawnChance = 1.0;
            ZoneEverything(city, ZoneType.Industrial);

            Run(city, 500);

            Assert.Greater(city.BuildingCount, 20);
            CityChecks.AssertAllRulesHold(city);
        }

        [Test]
        public void BulldozingARoad_RemovesTheBuildingsBesideIt_AndItsZones()
        {
            CitySimulation city = CityWithRoad(16);
            city.SpawnChance = 1.0;
            ZoneEverything(city, ZoneType.Residential);
            Run(city, 200);
            Assert.Greater(city.BuildingCount, 5);

            Assert.Greater(city.Bulldoze(new Vec2(0, 0), 3, true), 0);

            Assert.AreEqual(0, city.BuildingCount);
            Assert.AreEqual(0, city.Lots.Count);
        }

        [Test]
        public void Bulldoze_WithoutIncludeRoad_LeavesTheRoadAlone()
        {
            CitySimulation city = CityWithRoad(17);
            city.PaintZone(new Vec2(0, 12), 10, ZoneType.Residential);

            city.Bulldoze(new Vec2(0, 0), 30, false);

            Assert.AreEqual(1, city.Roads.SegmentCount);
            foreach (Lot lot in city.Lots.Lots)
            {
                Assert.AreEqual(ZoneType.None, lot.Zone);
            }
        }

        [Test]
        public void Bulldoze_RemovesABuilding_WhenThePointIsOnIt()
        {
            CitySimulation city = CityWithRoad(18);
            city.SpawnChance = 1.0;
            ZoneEverything(city, ZoneType.Commercial);
            Run(city, 100);
            Building b = null;
            foreach (Building x in city.Buildings) { b = x; break; }
            Assert.IsNotNull(b);

            Building found;
            Assert.IsTrue(city.TryGetBuildingAt(b.Center, out found));
            Assert.AreSame(b, found);

            city.Bulldoze(b.Center, 1, false);

            Assert.IsFalse(city.TryGetBuildingAt(b.Center, out found));
            CityChecks.AssertAllRulesHold(city);
        }

        [Test]
        public void Events_AreRaisedForBuildings_AndRevisionGoesUp()
        {
            CitySimulation city = CityWithRoad(19);
            int spawned = 0;
            int removed = 0;
            city.BuildingSpawned += b => spawned++;
            city.BuildingRemoved += b => removed++;
            int revision = city.Revision;
            city.SpawnChance = 1.0;
            ZoneEverything(city, ZoneType.Residential);
            Run(city, 100);

            Assert.AreEqual(city.BuildingCount, spawned);
            Assert.Greater(city.Revision, revision);

            city.Bulldoze(new Vec2(0, 0), 3, true);
            Assert.AreEqual(spawned, removed);
        }

        [Test]
        public void EveryZoneTemplate_FitsInTheLotRows()
        {
            foreach (BuildingTemplate t in BuildingCatalog.All())
            {
                Assert.LessOrEqual(t.Depth, LotLayout.Rows, t.Shape + " is deeper than the lots");
            }
        }

        // ---------------------------------------------------------------- random stress

        [Test]
        public void RandomEdits_NeverBreakTheRules()
        {
            for (int seed = 1; seed <= 4; seed++)
            {
                var rng = new Random(seed * 101);
                var city = new CitySimulation(seed);
                city.SpawnChance = 0.9;

                for (int op = 0; op < 140; op++)
                {
                    int what = rng.Next(10);
                    Vec2 p = new Vec2(rng.Next(-420, 420), rng.Next(-420, 420));
                    if (what < 3)
                    {
                        Vec2 q = new Vec2(p.X + rng.Next(-250, 250), p.Y + rng.Next(-250, 250));
                        Vec2? control = rng.Next(2) == 0 ? (Vec2?)null : new Vec2((p.X + q.X) / 2 + rng.Next(-90, 90), (p.Y + q.Y) / 2 + rng.Next(-90, 90));
                        RoadPlan plan = city.PlanRoad(p, control, q);
                        if (plan.IsValid)
                        {
                            Assert.IsNotNull(city.PlaceRoad(plan));
                        }
                    }
                    else if (what < 6)
                    {
                        city.PaintZone(p, rng.Next(8, 50), (ZoneType)rng.Next(0, 4));
                    }
                    else if (what < 7)
                    {
                        city.Bulldoze(p, rng.Next(2, 25), rng.Next(3) == 0);
                    }
                    else
                    {
                        Run(city, rng.Next(5, 40));
                    }

                    if (op % 10 == 0)
                    {
                        CityChecks.AssertAllRulesHold(city);
                    }
                }

                CityChecks.AssertAllRulesHold(city);
            }
        }
    }
}
