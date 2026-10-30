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

        private int CountCoveredCells()
        {
            int count = 0;
            for (int y = 0; y < sim.Grid.Height; y++)
            {
                for (int x = 0; x < sim.Grid.Width; x++)
                {
                    if (sim.Grid.GetBuildingId(new GridCoord(x, y)) != CityGrid.NoBuilding)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        // Every building must sit entirely on matching zoned, road-free cells that point back at it,
        // and no cell may point at a building that does not cover it.
        private void AssertBuildingsAreConsistent()
        {
            int covered = 0;
            foreach (Building b in sim.Buildings)
            {
                foreach (GridCoord c in b.Cells())
                {
                    Assert.IsTrue(sim.Grid.InBounds(c), "building " + b.Id + " sticks out of the grid at " + c);
                    Assert.AreEqual(b.Id, sim.Grid.GetBuildingId(c), "cell " + c + " does not point at building " + b.Id);
                    Assert.AreEqual(b.Zone, sim.Grid.GetZone(c), "zone does not match building " + b.Id + " at " + c);
                    Assert.IsFalse(sim.IsRoad(c), "building " + b.Id + " is on a road at " + c);
                    covered++;
                }
            }

            Assert.AreEqual(CountCoveredCells(), covered, "some cells point at a building that does not cover them");
        }

        // Grows a city until a building that covers more than one cell appears, and returns it.
        private Building MakeABigBuilding()
        {
            BuildMainRoad();
            sim.PaintZone(new GridRect(2, 2, 17, 18), ZoneType.Commercial);

            for (int i = 0; i < 300; i++)
            {
                sim.Step();
                foreach (Building b in sim.Buildings)
                {
                    if (b.Width * b.Depth > 1)
                    {
                        return b;
                    }
                }
            }

            Assert.IsTrue(false, "no multi-cell building appeared in 300 ticks");
            return null;
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

            RunTicks(300);

            Assert.AreEqual(zoned, CountCoveredCells(), "every zoned cell should end up under some building");
            AssertBuildingsAreConsistent();
        }

        [Test]
        public void Step_BuildingsOnlyAppearOnMatchingZonedNonRoadCells()
        {
            BuildMainRoad();
            sim.PaintZone(new GridRect(2, 11, 17, 12), ZoneType.Commercial);
            sim.PaintZone(new GridRect(2, 8, 17, 9), ZoneType.Industrial);

            RunTicks(200);

            Assert.Greater(sim.BuildingCount, 0);
            AssertBuildingsAreConsistent();
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

            Assert.Greater(sim.BuildingCount, 0);
            foreach (Building b in sim.Buildings)
            {
                Assert.GreaterOrEqual(b.Floors, b.Template.MinFloors, b.Shape + " is too short");
                Assert.LessOrEqual(b.Floors, b.Template.MaxFloors, b.Shape + " is too tall");
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
                parts.Add(b.Id + "@" + b.Cell + " " + b.Shape + " " + b.Width + "x" + b.Depth
                    + " turn " + b.Rotation + " floors " + b.Floors + " variant " + b.Variant);
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

                AssertBuildingsAreConsistent();
            }
        }

        // ------------------------------------------------------------ multi-cell buildings

        [Test]
        public void Growth_MakesBuildingsOfSeveralSizesAndShapes()
        {
            BuildMainRoad();
            sim.PaintZone(new GridRect(2, 2, 17, 18), ZoneType.Residential);

            RunTicks(300);

            int big = 0;
            var shapes = new HashSet<BuildingShape>();
            foreach (Building b in sim.Buildings)
            {
                if (b.Width * b.Depth > 1)
                {
                    big++;
                }

                shapes.Add(b.Shape);
            }

            Assert.Greater(big, 0);
            Assert.Greater(shapes.Count, 3);
            AssertBuildingsAreConsistent();
        }

        [Test]
        public void Growth_InAOneCellWideStrip_OnlyBuildsThingsThatFit()
        {
            BuildMainRoad();
            sim.PaintZone(new GridRect(2, 11, 17, 11), ZoneType.Commercial);

            RunTicks(200);

            Assert.Greater(sim.BuildingCount, 0);
            foreach (Building b in sim.Buildings)
            {
                Assert.AreEqual(1, b.Depth, b.Shape + " does not fit a strip one cell deep");
            }

            AssertBuildingsAreConsistent();
        }

        [Test]
        public void Growth_TurnsBuildingsToFaceTheRoad()
        {
            BuildMainRoad(); // runs along y = 10
            sim.PaintZone(new GridRect(2, 11, 17, 14), ZoneType.Residential); // north side: front must face south (turn 0)
            sim.PaintZone(new GridRect(2, 6, 17, 9), ZoneType.Residential);   // south side: front must face north (turn 2)

            RunTicks(300);

            int northTotal = 0, northFacing = 0, southTotal = 0, southFacing = 0;
            foreach (Building b in sim.Buildings)
            {
                if (b.Cell.Y > 10)
                {
                    northTotal++;
                    if (b.Rotation == 0) { northFacing++; }
                }
                else
                {
                    southTotal++;
                    if (b.Rotation == 2) { southFacing++; }
                }
            }

            Assert.Greater(northTotal, 0);
            Assert.Greater(southTotal, 0);
            Assert.GreaterOrEqual(northFacing * 10, northTotal * 8, "most buildings north of the road should face south");
            Assert.GreaterOrEqual(southFacing * 10, southTotal * 8, "most buildings south of the road should face north");
        }

        [Test]
        public void BigBuilding_CanBeFoundFromEveryCellItCovers()
        {
            Building big = MakeABigBuilding();

            foreach (GridCoord c in big.Cells())
            {
                Building found;
                Assert.IsTrue(sim.TryGetBuildingAt(c, out found), "nothing found at " + c);
                Assert.AreEqual(big.Id, found.Id);
            }
        }

        [Test]
        public void BigBuilding_BlocksRoadsOnAnyOfItsCells()
        {
            Building big = MakeABigBuilding();

            foreach (GridCoord c in big.Cells())
            {
                Assert.IsFalse(sim.CanPlaceRoad(c), "a road could be built on " + c);
            }
        }

        [Test]
        public void Bulldoze_OneCellOfABigBuilding_RemovesTheWholeBuildingButOnlyThatCellsZone()
        {
            Building big = MakeABigBuilding();
            GridCoord target = big.Cell;
            var others = new List<GridCoord>();
            foreach (GridCoord c in big.Cells())
            {
                if (c != target)
                {
                    others.Add(c);
                }
            }

            int removedEvents = 0;
            sim.BuildingRemoved += b => removedEvents++;

            sim.Bulldoze(new GridRect(target.X, target.Y, target.X, target.Y));

            Building found;
            Assert.IsFalse(sim.TryGetBuildingAt(target, out found));
            Assert.AreEqual(ZoneType.None, sim.Grid.GetZone(target));
            Assert.AreEqual(1, removedEvents, "the building should be reported removed exactly once");
            foreach (GridCoord c in others)
            {
                Assert.AreEqual(CityGrid.NoBuilding, sim.Grid.GetBuildingId(c), "building still holds " + c);
                Assert.AreEqual(big.Zone, sim.Grid.GetZone(c), "zone should be kept at " + c);
            }

            AssertBuildingsAreConsistent();
        }

        [Test]
        public void PaintZone_DifferentZoneOnOneCellOfABigBuilding_RemovesTheWholeBuilding()
        {
            Building big = MakeABigBuilding();
            GridCoord target = big.Cell;

            sim.PaintZone(new GridRect(target.X, target.Y, target.X, target.Y), ZoneType.Industrial);

            foreach (GridCoord c in big.Cells())
            {
                Assert.AreEqual(CityGrid.NoBuilding, sim.Grid.GetBuildingId(c), "building still holds " + c);
            }

            Assert.AreEqual(ZoneType.Industrial, sim.Grid.GetZone(target));
            AssertBuildingsAreConsistent();
        }

        [Test]
        public void PaintZone_SameZoneOverABigBuilding_LeavesItAlone()
        {
            Building big = MakeABigBuilding();

            sim.PaintZone(new GridRect(0, 0, 19, 19), ZoneType.Commercial);

            Building found;
            Assert.IsTrue(sim.TryGetBuildingAt(big.Cell, out found));
            Assert.AreEqual(big.Id, found.Id);
        }
    }

    [TestFixture]
    public class BuildingTests
    {
        private static BuildingTemplate Template(int width, int depth)
        {
            return new BuildingTemplate(BuildingShape.Warehouse, ZoneType.Industrial, width, depth, 1, 2, 1.0);
        }

        [Test]
        public void Cells_CoverTheWholeFootprintStartingAtTheAnchor()
        {
            var b = new Building(1, new GridCoord(5, 7), Template(3, 2), 0, 1, 0);

            var cells = new List<GridCoord>(b.Cells());

            Assert.AreEqual(6, cells.Count);
            Assert.AreEqual(3, b.Width);
            Assert.AreEqual(2, b.Depth);
            Assert.IsTrue(cells.Contains(new GridCoord(5, 7)));
            Assert.IsTrue(cells.Contains(new GridCoord(7, 8)));
            Assert.IsFalse(cells.Contains(new GridCoord(8, 7)));
        }

        [Test]
        public void ARotationByAQuarterTurn_SwapsWidthAndDepth()
        {
            var b = new Building(1, new GridCoord(5, 7), Template(3, 2), 1, 1, 0);

            var cells = new List<GridCoord>(b.Cells());

            Assert.AreEqual(2, b.Width);
            Assert.AreEqual(3, b.Depth);
            Assert.AreEqual(6, cells.Count);
            Assert.IsTrue(cells.Contains(new GridCoord(6, 9)));
            Assert.IsFalse(cells.Contains(new GridCoord(7, 7)));
        }

        [Test]
        public void Rotation_IsWrappedIntoZeroToThree()
        {
            Assert.AreEqual(1, new Building(1, new GridCoord(0, 0), Template(1, 1), 5, 1, 0).Rotation);
            Assert.AreEqual(3, new Building(1, new GridCoord(0, 0), Template(1, 1), -1, 1, 0).Rotation);
        }
    }

    [TestFixture]
    public class BuildingCatalogTests
    {
        private static readonly ZoneType[] Zones = { ZoneType.Residential, ZoneType.Commercial, ZoneType.Industrial };

        [Test]
        public void EveryZone_HasAtLeastOneSingleCellTemplate()
        {
            foreach (ZoneType zone in Zones)
            {
                bool found = false;
                foreach (BuildingTemplate t in BuildingCatalog.ForZone(zone))
                {
                    if (t.CellCount == 1)
                    {
                        found = true;
                    }
                }

                Assert.IsTrue(found, zone + " needs a 1x1 template to fill gaps");
            }
        }

        [Test]
        public void EveryTemplate_HasSensibleNumbersAndBelongsToItsZone()
        {
            foreach (ZoneType zone in Zones)
            {
                foreach (BuildingTemplate t in BuildingCatalog.ForZone(zone))
                {
                    Assert.AreEqual(zone, t.Zone, t.Shape + " is listed under the wrong zone");
                    Assert.GreaterOrEqual(t.Width, 1);
                    Assert.GreaterOrEqual(t.Depth, 1);
                    Assert.GreaterOrEqual(t.MinFloors, 1);
                    Assert.GreaterOrEqual(t.MaxFloors, t.MinFloors);
                    Assert.IsTrue(t.Weight > 0.0, t.Shape + " can never be picked");
                }
            }
        }

        [Test]
        public void EveryShape_HasAtLeastOneTemplate()
        {
            var used = new HashSet<BuildingShape>();
            foreach (BuildingTemplate t in BuildingCatalog.All())
            {
                used.Add(t.Shape);
            }

            foreach (BuildingShape shape in System.Enum.GetValues(typeof(BuildingShape)))
            {
                Assert.IsTrue(used.Contains(shape), shape + " is never used by any template");
            }
        }

        [Test]
        public void Pick_SingleCellOnly_NeverReturnsABiggerTemplate()
        {
            var rng = new System.Random(3);
            foreach (ZoneType zone in Zones)
            {
                for (int i = 0; i < 200; i++)
                {
                    Assert.AreEqual(1, BuildingCatalog.Pick(zone, rng, true).CellCount);
                }
            }
        }

        [Test]
        public void Pick_EventuallyReturnsEveryTemplateOfAZone()
        {
            var rng = new System.Random(4);
            foreach (ZoneType zone in Zones)
            {
                var seen = new HashSet<BuildingTemplate>();
                for (int i = 0; i < 3000; i++)
                {
                    seen.Add(BuildingCatalog.Pick(zone, rng, false));
                }

                Assert.AreEqual(BuildingCatalog.ForZone(zone).Count, seen.Count, "some " + zone + " templates are never picked");
            }
        }
    }

    [TestFixture]
    public class BuildingDesignerTests
    {
        private const float Tolerance = 0.001f;

        private static string Signature(List<BuildingPart> parts)
        {
            var text = new List<string>();
            foreach (BuildingPart p in parts)
            {
                text.Add(string.Format("{0} {1:0.00},{2:0.00} {3:0.00}x{4:0.00} h{5:0.00} e{6:0.00} {7} {8}",
                    p.Kind, p.CenterX, p.CenterY, p.SizeX, p.SizeY, p.Height, p.Elevation, p.Tone, p.RidgeAlongX));
            }

            return string.Join("|", text.ToArray());
        }

        [Test]
        public void EveryTemplate_GetsPartsThatStayInsideItsFootprint_ForManyVariantsAndFloorCounts()
        {
            foreach (BuildingTemplate t in BuildingCatalog.All())
            {
                for (int floors = t.MinFloors; floors <= t.MaxFloors; floors++)
                {
                    for (int variant = 0; variant < 60; variant++)
                    {
                        var b = new Building(1, new GridCoord(0, 0), t, variant % 4, floors, variant);
                        List<BuildingPart> parts = BuildingDesigner.Design(b);
                        string what = t.Shape + " " + t.Width + "x" + t.Depth + " floors " + floors + " variant " + variant;

                        Assert.Greater(parts.Count, 0, what + " has no parts");
                        foreach (BuildingPart p in parts)
                        {
                            Assert.IsTrue(p.SizeX > 0f && p.SizeY > 0f, what + ": a part has no size");
                            Assert.IsTrue(p.Height > 0f, what + ": a part has no height");
                            Assert.IsTrue(p.Elevation >= 0f, what + ": a part is below the ground");
                            Assert.IsTrue(p.CenterX - p.SizeX * 0.5f >= -t.Width * 0.5f - Tolerance, what + ": a part sticks out on the left");
                            Assert.IsTrue(p.CenterX + p.SizeX * 0.5f <= t.Width * 0.5f + Tolerance, what + ": a part sticks out on the right");
                            Assert.IsTrue(p.CenterY - p.SizeY * 0.5f >= -t.Depth * 0.5f - Tolerance, what + ": a part sticks out at the front");
                            Assert.IsTrue(p.CenterY + p.SizeY * 0.5f <= t.Depth * 0.5f + Tolerance, what + ": a part sticks out at the back");
                        }
                    }
                }
            }
        }

        [Test]
        public void EveryBuilding_IsAtLeastAsTallAsItsFloorCount()
        {
            foreach (BuildingTemplate t in BuildingCatalog.All())
            {
                for (int floors = t.MinFloors; floors <= t.MaxFloors; floors++)
                {
                    for (int variant = 0; variant < 30; variant++)
                    {
                        var b = new Building(1, new GridCoord(0, 0), t, 0, floors, variant);
                        float top = 0f;
                        foreach (BuildingPart p in BuildingDesigner.Design(b))
                        {
                            top = System.Math.Max(top, p.Elevation + p.Height);
                        }

                        Assert.IsTrue(top >= floors - Tolerance, t.Shape + " with " + floors + " floors only reaches " + top);
                    }
                }
            }
        }

        [Test]
        public void SameBuilding_AlwaysGetsTheSameDesign()
        {
            foreach (BuildingTemplate t in BuildingCatalog.All())
            {
                var b = new Building(1, new GridCoord(0, 0), t, 0, t.MinFloors, 12345);

                Assert.AreEqual(Signature(BuildingDesigner.Design(b)), Signature(BuildingDesigner.Design(b)), t.Shape + " is not deterministic");
            }
        }

        [Test]
        public void EveryTemplate_HasMoreThanOneLook_AcrossVariants()
        {
            foreach (BuildingTemplate t in BuildingCatalog.All())
            {
                var looks = new HashSet<string>();
                for (int variant = 0; variant < 60; variant++)
                {
                    looks.Add(Signature(BuildingDesigner.Design(new Building(1, new GridCoord(0, 0), t, 0, t.MinFloors, variant))));
                }

                Assert.Greater(looks.Count, 1, t.Shape + " " + t.Width + "x" + t.Depth + " always looks the same");
            }
        }

        [Test]
        public void DifferentShapes_LookDifferent()
        {
            var looks = new HashSet<string>();
            int shapes = 0;
            var seen = new HashSet<BuildingShape>();
            foreach (BuildingTemplate t in BuildingCatalog.All())
            {
                if (seen.Add(t.Shape))
                {
                    shapes++;
                    looks.Add(Signature(BuildingDesigner.Design(new Building(1, new GridCoord(0, 0), t, 0, t.MinFloors, 0))));
                }
            }

            Assert.AreEqual(shapes, looks.Count);
        }
    }
}
