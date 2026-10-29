using System.Collections.Generic;
using NUnit.Framework;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Tests
{
    [TestFixture]
    public class GridShapesTests
    {
        [Test]
        public void StraightLine_Horizontal_ReturnsCellsLeftToRight()
        {
            List<GridCoord> line = GridShapes.StraightLine(new GridCoord(2, 4), new GridCoord(5, 4));

            Assert.AreEqual(4, line.Count);
            Assert.AreEqual(new GridCoord(2, 4), line[0]);
            Assert.AreEqual(new GridCoord(5, 4), line[3]);
        }

        [Test]
        public void StraightLine_DraggedBackwards_ReturnsCellsInDragOrder()
        {
            List<GridCoord> line = GridShapes.StraightLine(new GridCoord(5, 1), new GridCoord(2, 1));

            Assert.AreEqual(4, line.Count);
            Assert.AreEqual(new GridCoord(5, 1), line[0]);
            Assert.AreEqual(new GridCoord(2, 1), line[3]);
        }

        [Test]
        public void StraightLine_DiagonalDrag_FollowsTheLongerAxis()
        {
            List<GridCoord> line = GridShapes.StraightLine(new GridCoord(0, 0), new GridCoord(2, 5));

            Assert.AreEqual(6, line.Count);
            foreach (GridCoord c in line)
            {
                Assert.AreEqual(0, c.X);
            }
        }

        [Test]
        public void StraightLine_SameCell_ReturnsThatCellOnce()
        {
            List<GridCoord> line = GridShapes.StraightLine(new GridCoord(3, 3), new GridCoord(3, 3));

            Assert.AreEqual(1, line.Count);
            Assert.AreEqual(new GridCoord(3, 3), line[0]);
        }
    }

    [TestFixture]
    public class TickClockTests
    {
        [Test]
        public void Advance_AtNormalSpeed_ReturnsOneTickPerTickSeconds()
        {
            var clock = new TickClock(0.5, 10);

            Assert.AreEqual(0, clock.Advance(0.3));
            Assert.AreEqual(1, clock.Advance(0.3), "0.6 seconds in total, so one tick is due");
            Assert.AreEqual(2, clock.Advance(1.0));
        }

        [Test]
        public void Advance_AtDoubleSpeed_ReturnsTwiceAsManyTicks()
        {
            var clock = new TickClock(0.5, 10);
            clock.SetSpeedLevel(2);

            Assert.AreEqual(4, clock.Advance(1.0));
        }

        [Test]
        public void Advance_WhilePaused_ReturnsZeroAndKeepsNoBacklog()
        {
            var clock = new TickClock(0.5, 10);
            clock.SetSpeedLevel(0);

            Assert.AreEqual(0, clock.Advance(100.0));

            clock.SetSpeedLevel(1);
            Assert.AreEqual(0, clock.Advance(0.1));
        }

        [Test]
        public void Advance_ALongFrame_IsCappedAndTheBacklogDropped()
        {
            var clock = new TickClock(0.5, 5);

            Assert.AreEqual(5, clock.Advance(60.0));
            Assert.AreEqual(0, clock.Advance(0.1));
        }

        [Test]
        public void TogglePause_ResumesAtThePreviousSpeed()
        {
            var clock = new TickClock(0.5, 10);
            clock.SetSpeedLevel(3);

            clock.TogglePause();
            Assert.IsTrue(clock.IsPaused);

            clock.TogglePause();
            Assert.AreEqual(3, clock.SpeedLevel);
        }

        [Test]
        public void SetSpeedLevel_IsClampedToTheValidRange()
        {
            var clock = new TickClock(0.5, 10);

            clock.SetSpeedLevel(99);
            Assert.AreEqual(TickClock.MaxSpeedLevel, clock.SpeedLevel);

            clock.SetSpeedLevel(-4);
            Assert.AreEqual(0, clock.SpeedLevel);
        }
    }

    [TestFixture]
    public class RoadNetworkTests
    {
        [Test]
        public void AddNode_NextToExistingRoad_CreatesASegment()
        {
            var roads = new RoadNetwork();
            roads.AddNode(new GridCoord(1, 1));
            roads.AddNode(new GridCoord(2, 1));

            Assert.AreEqual(2, roads.NodeCount);
            Assert.AreEqual(1, roads.SegmentCount);
            Assert.IsNotNull(roads.GetSegment(new GridCoord(1, 1), new GridCoord(2, 1)));
            Assert.IsNotNull(roads.GetSegment(new GridCoord(2, 1), new GridCoord(1, 1)), "segment lookup should not care about order");
        }

        [Test]
        public void AddNode_Diagonal_DoesNotConnect()
        {
            var roads = new RoadNetwork();
            roads.AddNode(new GridCoord(1, 1));
            roads.AddNode(new GridCoord(2, 2));

            Assert.AreEqual(0, roads.SegmentCount);
        }

        [Test]
        public void AddNode_Crossroads_HasDegreeFour()
        {
            var roads = new RoadNetwork();
            var centre = new GridCoord(5, 5);
            roads.AddNode(centre);
            roads.AddNode(new GridCoord(4, 5));
            roads.AddNode(new GridCoord(6, 5));
            roads.AddNode(new GridCoord(5, 4));
            roads.AddNode(new GridCoord(5, 6));

            Assert.AreEqual(4, roads.Degree(centre));
            Assert.AreEqual(1, roads.Degree(new GridCoord(4, 5)));
        }

        [Test]
        public void AddNode_Twice_ReturnsFalseTheSecondTime()
        {
            var roads = new RoadNetwork();

            Assert.IsTrue(roads.AddNode(new GridCoord(0, 0)));
            Assert.IsFalse(roads.AddNode(new GridCoord(0, 0)));
            Assert.AreEqual(1, roads.NodeCount);
        }

        [Test]
        public void RemoveNode_RemovesAttachedSegments()
        {
            var roads = new RoadNetwork();
            roads.AddNode(new GridCoord(0, 0));
            roads.AddNode(new GridCoord(1, 0));
            roads.AddNode(new GridCoord(2, 0));

            Assert.IsTrue(roads.RemoveNode(new GridCoord(1, 0)));

            Assert.AreEqual(2, roads.NodeCount);
            Assert.AreEqual(0, roads.SegmentCount);
        }

        [Test]
        public void RemoveNode_WithNoRoad_ReturnsFalse()
        {
            var roads = new RoadNetwork();

            Assert.IsFalse(roads.RemoveNode(new GridCoord(3, 3)));
        }

        [Test]
        public void ClosedSegment_IsSkippedWhenOnlyOpenNeighboursAreRequested()
        {
            var roads = new RoadNetwork();
            var a = new GridCoord(0, 0);
            var b = new GridCoord(1, 0);
            roads.AddNode(a);
            roads.AddNode(b);

            roads.GetSegment(a, b).OpenFraction = 0f;

            Assert.IsTrue(roads.GetSegment(a, b).IsClosed);
            Assert.AreEqual(1, roads.Neighbours(a, false).Count);
            Assert.AreEqual(0, roads.Neighbours(a, true).Count);
        }

        [Test]
        public void OpenFraction_IsClampedAndScalesCapacity()
        {
            var segment = new RoadSegment(new GridCoord(0, 0), new GridCoord(1, 0), 10f);

            segment.OpenFraction = 0.5f;
            Assert.AreEqual(5f, segment.EffectiveCapacity, 0.0001f);

            segment.OpenFraction = 7f;
            Assert.AreEqual(1f, segment.OpenFraction, 0.0001f);

            segment.OpenFraction = -3f;
            Assert.AreEqual(0f, segment.OpenFraction, 0.0001f);
        }
    }

    [TestFixture]
    public class CitySimulationTests
    {
        private CitySimulation sim;

        [SetUp]
        public void SetUp()
        {
            sim = new CitySimulation(20, 20, 1234);
            sim.SpawnChance = 1.0;
        }

        // A road along y = 10 from x = 2 to x = 17.
        private void BuildMainRoad()
        {
            sim.PlaceRoad(GridShapes.StraightLine(new GridCoord(2, 10), new GridCoord(17, 10)));
        }

        private int CountZonedCells()
        {
            int count = 0;
            for (int y = 0; y < sim.Grid.Height; y++)
            {
                for (int x = 0; x < sim.Grid.Width; x++)
                {
                    if (sim.Grid.GetZone(new GridCoord(x, y)) != ZoneType.None)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private void RunTicks(int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                sim.Step();
            }
        }

        // ------------------------------------------------------------ roads

        [Test]
        public void PlaceRoad_StraightLine_BuildsConnectedRoad()
        {
            BuildMainRoad();

            Assert.AreEqual(16, sim.Roads.NodeCount);
            Assert.AreEqual(15, sim.Roads.SegmentCount);
        }

        [Test]
        public void PlaceRoad_OutsideTheGrid_IsIgnored()
        {
            int placed = sim.PlaceRoad(new[] { new GridCoord(-1, 0), new GridCoord(0, 20), new GridCoord(100, 100) });

            Assert.AreEqual(0, placed);
            Assert.AreEqual(0, sim.Roads.NodeCount);
        }

        [Test]
        public void PlaceRoad_OnExistingRoad_DoesNotCountTwice()
        {
            BuildMainRoad();

            int placedAgain = sim.PlaceRoad(GridShapes.StraightLine(new GridCoord(2, 10), new GridCoord(5, 10)));

            Assert.AreEqual(0, placedAgain);
            Assert.AreEqual(16, sim.Roads.NodeCount);
        }

        [Test]
        public void PlaceRoad_RaisesCellChangedOncePerNewRoadCell()
        {
            var changed = new List<GridCoord>();
            sim.CellChanged += changed.Add;

            sim.PlaceRoad(GridShapes.StraightLine(new GridCoord(0, 0), new GridCoord(3, 0)));

            Assert.AreEqual(4, changed.Count);
        }

        [Test]
        public void PlaceRoad_OverEmptyZone_RemovesTheZone()
        {
            BuildMainRoad();
            sim.PaintZone(new GridRect(5, 9, 5, 9), ZoneType.Residential);
            Assert.AreEqual(ZoneType.Residential, sim.Grid.GetZone(new GridCoord(5, 9)));

            sim.PlaceRoad(new[] { new GridCoord(5, 9) });

            Assert.IsTrue(sim.IsRoad(new GridCoord(5, 9)));
            Assert.AreEqual(ZoneType.None, sim.Grid.GetZone(new GridCoord(5, 9)));
        }

        [Test]
        public void PlaceRoad_OverABuilding_IsRejected()
        {
            BuildMainRoad();
            sim.PaintZone(new GridRect(5, 9, 5, 9), ZoneType.Residential);
            RunTicks(10);
            Building building;
            Assert.IsTrue(sim.TryGetBuildingAt(new GridCoord(5, 9), out building));

            int placed = sim.PlaceRoad(new[] { new GridCoord(5, 9) });

            Assert.AreEqual(0, placed);
            Assert.IsFalse(sim.IsRoad(new GridCoord(5, 9)));
        }

        // ------------------------------------------------------------ zoning

        [Test]
        public void IsServed_UsesZoneDepthAlongTheGrid()
        {
            sim.PlaceRoad(new[] { new GridCoord(10, 10) });

            Assert.AreEqual(4, CitySimulation.ZoneDepth);
            Assert.IsTrue(sim.IsServed(new GridCoord(10, 14)), "4 cells straight away");
            Assert.IsFalse(sim.IsServed(new GridCoord(10, 15)), "5 cells straight away");
            Assert.IsTrue(sim.IsServed(new GridCoord(12, 12)), "diagonal, 4 steps");
            Assert.IsFalse(sim.IsServed(new GridCoord(13, 12)), "5 steps");
        }

        [Test]
        public void PaintZone_FarFromAnyRoad_ChangesNothing()
        {
            BuildMainRoad();

            int changed = sim.PaintZone(new GridRect(0, 0, 19, 4), ZoneType.Commercial);

            Assert.AreEqual(0, changed);
            Assert.AreEqual(0, CountZonedCells());
        }

        [Test]
        public void PaintZone_NextToRoad_ZonesOnlyServedNonRoadCells()
        {
            BuildMainRoad();

            sim.PaintZone(new GridRect(2, 2, 17, 18), ZoneType.Residential);

            // Rows 6 to 9 and 11 to 14 are within 4 cells of the road row (y = 10); row 10 is the road itself.
            Assert.AreEqual(8 * 16, CountZonedCells());
            Assert.AreEqual(ZoneType.None, sim.Grid.GetZone(new GridCoord(5, 10)), "the road itself");
            Assert.AreEqual(ZoneType.Residential, sim.Grid.GetZone(new GridCoord(5, 6)), "4 cells below the road");
            Assert.AreEqual(ZoneType.Residential, sim.Grid.GetZone(new GridCoord(5, 14)), "4 cells above the road");
            Assert.AreEqual(ZoneType.None, sim.Grid.GetZone(new GridCoord(5, 5)), "5 cells below the road");
            Assert.AreEqual(ZoneType.None, sim.Grid.GetZone(new GridCoord(5, 15)), "5 cells above the road");
        }

        [Test]
        public void PaintZone_RectangleHangingOffTheGrid_IsClipped()
        {
            BuildMainRoad();
            sim.PaintZone(new GridRect(-50, -50, 500, 500), ZoneType.Industrial);
            int zonedAfterOversizedPaint = CountZonedCells();

            // The same paint, but exactly the size of the grid, must give the same result.
            var reference = new CitySimulation(20, 20, 1234);
            reference.PlaceRoad(GridShapes.StraightLine(new GridCoord(2, 10), new GridCoord(17, 10)));
            reference.PaintZone(new GridRect(0, 0, 19, 19), ZoneType.Industrial);
            int zonedReference = 0;
            for (int y = 0; y < 20; y++)
            {
                for (int x = 0; x < 20; x++)
                {
                    if (reference.Grid.GetZone(new GridCoord(x, y)) != ZoneType.None)
                    {
                        zonedReference++;
                    }
                }
            }

            Assert.Greater(zonedAfterOversizedPaint, 0);
            Assert.AreEqual(zonedReference, zonedAfterOversizedPaint);
        }

        [Test]
        public void PaintZone_OverDifferentZone_ReplacesItAndRemovesTheBuilding()
        {
            BuildMainRoad();
            sim.PaintZone(new GridRect(2, 11, 17, 12), ZoneType.Residential);
            RunTicks(50);
            Assert.Greater(sim.CountBuildings(ZoneType.Residential), 0);

            sim.PaintZone(new GridRect(2, 11, 17, 12), ZoneType.Commercial);

            Assert.AreEqual(0, sim.CountBuildings(ZoneType.Residential));
            Assert.AreEqual(ZoneType.Commercial, sim.Grid.GetZone(new GridCoord(7, 11)));
        }

        [Test]
        public void PaintZone_None_ClearsZoning()
        {
            BuildMainRoad();
            sim.PaintZone(new GridRect(2, 11, 17, 12), ZoneType.Residential);

            sim.PaintZone(new GridRect(2, 11, 17, 12), ZoneType.None);

            Assert.AreEqual(0, CountZonedCells());
        }

        // ------------------------------------------------------------ growth

        [Test]
        public void Step_WithNoZones_SpawnsNothing()
        {
            BuildMainRoad();

            RunTicks(50);

            Assert.AreEqual(0, sim.BuildingCount);
        }

        [Test]
        public void Step_WithZones_EventuallyFillsEveryZonedCell()
        {
            BuildMainRoad();
            sim.PaintZone(new GridRect(2, 6, 17, 14), ZoneType.Residential);
            int zoned = CountZonedCells();

            RunTicks(200);

            Assert.AreEqual(zoned, sim.BuildingCount);
        }

        [Test]
        public void Step_BuildingsOnlyAppearOnMatchingZonedNonRoadCells()
        {
            BuildMainRoad();
            sim.PaintZone(new GridRect(2, 11, 17, 12), ZoneType.Commercial);
            sim.PaintZone(new GridRect(2, 8, 17, 9), ZoneType.Industrial);

            RunTicks(200);

            Assert.Greater(sim.BuildingCount, 0);
            foreach (Building b in sim.Buildings)
            {
                Assert.AreEqual(b.Zone, sim.Grid.GetZone(b.Cell));
                Assert.IsFalse(sim.IsRoad(b.Cell));
                Assert.AreEqual(b.Id, sim.Grid.GetBuildingId(b.Cell));
            }
        }

        [Test]
        public void Step_SpawnChanceZero_NeverSpawns()
        {
            sim.SpawnChance = 0.0;
            BuildMainRoad();
            sim.PaintZone(new GridRect(2, 11, 17, 12), ZoneType.Residential);

            RunTicks(100);

            Assert.AreEqual(0, sim.BuildingCount);
        }

        [Test]
        public void Step_RaisesBuildingSpawnedForEachBuilding()
        {
            int spawned = 0;
            sim.BuildingSpawned += b => spawned++;
            BuildMainRoad();
            sim.PaintZone(new GridRect(2, 11, 17, 12), ZoneType.Residential);

            RunTicks(100);

            Assert.AreEqual(sim.BuildingCount, spawned);
            Assert.Greater(spawned, 0);
        }

        [Test]
        public void Step_FloorCountsStayWithinTheRangeForEachZone()
        {
            BuildMainRoad();
            sim.PaintZone(new GridRect(2, 11, 17, 12), ZoneType.Residential);
            sim.PaintZone(new GridRect(2, 8, 9, 9), ZoneType.Commercial);
            sim.PaintZone(new GridRect(10, 8, 17, 9), ZoneType.Industrial);

            RunTicks(200);

            foreach (Building b in sim.Buildings)
            {
                switch (b.Zone)
                {
                    case ZoneType.Residential:
                        Assert.GreaterOrEqual(b.Floors, 1);
                        Assert.LessOrEqual(b.Floors, 3);
                        break;
                    case ZoneType.Commercial:
                        Assert.GreaterOrEqual(b.Floors, 2);
                        Assert.LessOrEqual(b.Floors, 4);
                        break;
                    case ZoneType.Industrial:
                        Assert.GreaterOrEqual(b.Floors, 1);
                        Assert.LessOrEqual(b.Floors, 2);
                        break;
                }
            }
        }

        [Test]
        public void Step_SameSeed_GivesTheSameCity()
        {
            Assert.AreEqual(RunScenario(77), RunScenario(77));
        }

        private string RunScenario(int seed)
        {
            var other = new CitySimulation(20, 20, seed);
            other.PlaceRoad(GridShapes.StraightLine(new GridCoord(2, 10), new GridCoord(17, 10)));
            other.PaintZone(new GridRect(2, 6, 17, 14), ZoneType.Residential);
            for (int i = 0; i < 30; i++)
            {
                other.Step();
            }

            var parts = new List<string>();
            foreach (Building b in other.Buildings)
            {
                parts.Add(b.Id + "@" + b.Cell + "x" + b.Floors);
            }

            return string.Join(";", parts.ToArray());
        }

        // ------------------------------------------------------------ bulldozing

        [Test]
        public void Bulldoze_ABuilding_RemovesBuildingAndZone()
        {
            BuildMainRoad();
            sim.PaintZone(new GridRect(5, 11, 5, 11), ZoneType.Residential);
            RunTicks(10);
            Assert.AreEqual(1, sim.BuildingCount);

            int changed = sim.Bulldoze(new GridRect(5, 11, 5, 11));

            Assert.AreEqual(1, changed);
            Assert.AreEqual(0, sim.BuildingCount);
            Assert.AreEqual(ZoneType.None, sim.Grid.GetZone(new GridCoord(5, 11)));
        }

        [Test]
        public void Bulldoze_AllRoad_RemovesEveryUnservedZoneAndBuilding()
        {
            BuildMainRoad();
            sim.PaintZone(new GridRect(2, 6, 17, 14), ZoneType.Residential);
            RunTicks(200);
            int buildingsBefore = sim.BuildingCount;
            Assert.Greater(buildingsBefore, 0);

            int removedEvents = 0;
            sim.BuildingRemoved += b => removedEvents++;

            sim.Bulldoze(new GridRect(0, 10, 19, 10));

            Assert.AreEqual(0, sim.Roads.NodeCount);
            Assert.AreEqual(0, sim.BuildingCount);
            Assert.AreEqual(0, CountZonedCells());
            Assert.AreEqual(buildingsBefore, removedEvents);
        }

        [Test]
        public void Bulldoze_PartOfARoad_OnlyClearsZonesThatLostService()
        {
            BuildMainRoad();
            sim.PaintZone(new GridRect(2, 8, 17, 9), ZoneType.Residential);

            sim.Bulldoze(new GridRect(2, 10, 5, 10));

            // (3, 8) was only served by road cells that are now gone; (10, 8) still has road beside it.
            Assert.AreEqual(ZoneType.None, sim.Grid.GetZone(new GridCoord(3, 8)));
            Assert.AreEqual(ZoneType.Residential, sim.Grid.GetZone(new GridCoord(10, 8)));
        }

        [Test]
        public void Bulldoze_RectangleHangingOffTheGrid_IsClipped()
        {
            BuildMainRoad();

            sim.Bulldoze(new GridRect(-100, -100, 100, 100));

            Assert.AreEqual(0, sim.Roads.NodeCount);
        }

        [Test]
        public void Invariant_ZonedCellsAreAlwaysServed_AfterRandomEdits()
        {
            var edits = new System.Random(5);
            for (int i = 0; i < 300; i++)
            {
                int x0 = edits.Next(0, 20);
                int y0 = edits.Next(0, 20);
                int x1 = edits.Next(0, 20);
                int y1 = edits.Next(0, 20);
                var a = new GridCoord(x0, y0);
                var b = new GridCoord(x1, y1);

                switch (edits.Next(4))
                {
                    case 0:
                        sim.PlaceRoad(GridShapes.StraightLine(a, b));
                        break;
                    case 1:
                        sim.PaintZone(GridRect.FromCorners(a, b), (ZoneType)edits.Next(1, 4));
                        break;
                    case 2:
                        sim.Bulldoze(GridRect.FromCorners(a, b));
                        break;
                    default:
                        RunTicks(3);
                        break;
                }

                for (int y = 0; y < 20; y++)
                {
                    for (int x = 0; x < 20; x++)
                    {
                        var c = new GridCoord(x, y);
                        ZoneType zone = sim.Grid.GetZone(c);
                        if (zone != ZoneType.None)
                        {
                            Assert.IsFalse(sim.IsRoad(c), "zone on a road at " + c);
                            Assert.IsTrue(sim.IsServed(c), "unserved zone at " + c);
                        }

                        Building building;
                        if (sim.TryGetBuildingAt(c, out building))
                        {
                            Assert.AreEqual(zone, building.Zone, "building does not match zone at " + c);
                        }
                    }
                }
            }
        }
    }
}
