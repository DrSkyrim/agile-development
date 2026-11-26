using NUnit.Framework;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Tests
{
    [TestFixture]
    public class UndoTests
    {
        [Test]
        public void ASnapshotBroughtBackRestoresRoadsBuildingsAndMoney()
        {
            var city = new CitySimulation(5);
            city.Treasury.Enable(1e6);
            city.SpawnChance = 1.0;
            city.Economy.DemandDrivesGrowth = false;
            Assert.IsNotNull(city.PlaceRoad(city.PlanRoad(new Vec2(-300, 0), null, new Vec2(300, 0), RoadType.Street)));
            city.FillZone(new Vec2(0, 12), ZoneType.Residential);
            for (int i = 0; i < 150; i++) { city.Step(); }

            CitySnapshot before = city.Capture();
            int buildings = city.BuildingCount;
            int roads = city.Roads.SegmentCount;
            double money = city.Treasury.Balance;
            Assert.Greater(buildings, 3);

            city.Bulldoze(new Vec2(0, 12), 1000, true);
            city.PaintZone(new Vec2(0, 12), 400, ZoneType.None);
            Assert.Less(city.BuildingCount, buildings);

            int skipped;
            CitySimulation restored = CitySimulation.Restore(before, out skipped);
            Assert.AreEqual(0, skipped);
            Assert.AreEqual(buildings, restored.BuildingCount);
            Assert.AreEqual(roads, restored.Roads.SegmentCount);
            Assert.AreEqual(money, restored.Treasury.Balance, 0.01);
        }
    }
}
