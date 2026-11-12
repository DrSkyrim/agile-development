using System.Collections.Generic;
using NUnit.Framework;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Tests
{
    [TestFixture]
    public class SaveLoadTests
    {
        private static CitySimulation GrownCity()
        {
            var city = new CitySimulation(7);
            city.Treasury.Enable(50000.0);
            city.SpawnChance = 1.0;
            city.CreateOutsideConnection(new System.Random(5));
            city.PlaceRoad(city.PlanRoad(new Vec2(-400, 0), null, new Vec2(0, 0)));
            city.PlaceRoad(city.PlanRoad(new Vec2(0, 0), new Vec2(200, 80), new Vec2(400, 0)));
            city.PlaceRoad(city.PlanRoad(new Vec2(0, 0), null, new Vec2(0, -300)));
            city.PaintZone(new Vec2(-200, 20), 80, ZoneType.Residential);
            city.PaintZone(new Vec2(200, 40), 80, ZoneType.Commercial);
            city.PaintZone(new Vec2(10, -150), 80, ZoneType.Industrial);
            for (int i = 0; i < 400; i++) { city.Step(); }
            return city;
        }

        [Test]
        public void ARestoredCityMatchesTheOriginal()
        {
            CitySimulation a = GrownCity();
            Assert.Greater(a.BuildingCount, 5);

            int skipped;
            CitySimulation b = CitySimulation.Restore(a.Capture(), out skipped);

            Assert.AreEqual(0, skipped);
            Assert.AreEqual(a.Roads.SegmentCount, b.Roads.SegmentCount);
            Assert.AreEqual(a.Roads.NodeCount, b.Roads.NodeCount);
            Assert.AreEqual(a.BuildingCount, b.BuildingCount);
            Assert.AreEqual(a.TickCount, b.TickCount);
            Assert.AreEqual(a.Treasury.Balance, b.Treasury.Balance, 0.0001);
            Assert.AreEqual(a.Treasury.WeeklyUpkeep, b.Treasury.WeeklyUpkeep, 0.01);
            Assert.AreEqual(a.Economy.Snapshot.Residents, b.Economy.Snapshot.Residents, 0.0001);
            Assert.AreEqual(a.OutsideConnections.Count, b.OutsideConnections.Count);
            foreach (ZoneType z in new[] { ZoneType.Residential, ZoneType.Commercial, ZoneType.Industrial })
            {
                Assert.AreEqual(a.CountBuildings(z), b.CountBuildings(z), z.ToString());
                Assert.AreEqual(CountZoned(a, z), CountZoned(b, z), z + " lots");
            }
        }

        [Test]
        public void ARestoredCityKeepsGrowingAndBuilding()
        {
            int skipped;
            CitySimulation b = CitySimulation.Restore(GrownCity().Capture(), out skipped);
            int before = b.BuildingCount;
            for (int i = 0; i < 300; i++) { b.Step(); }
            Assert.GreaterOrEqual(b.BuildingCount, before);
            RoadPlan plan = b.PlanRoad(new Vec2(-400, 0), null, new Vec2(-400, 200));
            Assert.IsTrue(plan.IsValid, plan.Error);
            Assert.IsNotNull(b.PlaceRoad(plan));
        }

        [Test]
        public void TheOutsideConnectionStaysProtectedAfterLoading()
        {
            int skipped;
            CitySimulation b = CitySimulation.Restore(GrownCity().Capture(), out skipped);
            var oc = b.OutsideConnections[0];
            int segments = b.Roads.SegmentCount;
            b.Bulldoze(oc.Segment.Curve.Point(0.5), 10, true);
            Assert.AreEqual(segments, b.Roads.SegmentCount);
        }

        private static int CountZoned(CitySimulation city, ZoneType zone)
        {
            int n = 0;
            foreach (Lot lot in city.Lots.Lots) { if (lot.Zone == zone) { n++; } }
            return n;
        }
    }
}
