using NUnit.Framework;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Tests
{
    [TestFixture]
    public class UpgradeTests
    {
        private static RoadSegment At(CitySimulation city, double x, double y)
        {
            double s;
            return city.Roads.FindSegmentNear(new Vec2(x, y), 5.0, out s);
        }

        private static CitySimulation Street()
        {
            var city = new CitySimulation(5);
            city.Treasury.Enable(1e7);
            city.SpawnChance = 1.0;
            city.Economy.DemandDrivesGrowth = false;
            Assert.IsNotNull(city.PlaceRoad(city.PlanRoad(new Vec2(-300, 0), null, new Vec2(300, 0), RoadType.Street)));
            return city;
        }

        [Test]
        public void RoadsHaveHumanNames()
        {
            Assert.AreEqual("Single-lane road", RoadSpecs.Name(RoadType.Street));
            Assert.AreEqual("Two-lane road", RoadSpecs.Name(RoadType.Avenue));
            Assert.AreEqual("Single-lane highway", RoadSpecs.Name(RoadType.TwoLaneHighway));
            Assert.AreEqual("Two-lane highway", RoadSpecs.Name(RoadType.Highway));
            Assert.AreEqual("One-way highway", RoadSpecs.Name(RoadType.OneWayHighway));
        }

        [Test]
        public void UpgradingAStreetToAnAvenueWidensItAndRegrowsBuildings()
        {
            CitySimulation city = Street();
            city.PaintZone(new Vec2(0, 20), 400, ZoneType.Residential);
            city.PaintZone(new Vec2(0, -20), 400, ZoneType.Residential);
            for (int i = 0; i < 300; i++) { city.Step(); }
            int before = city.BuildingCount;
            Assert.Greater(before, 5);

            RoadSegment road = At(city, 0, 0);
            double balance = city.Treasury.Balance;
            var plan = city.PlanUpgrade(road, RoadType.Avenue);
            Assert.IsTrue(plan.IsValid, plan.Error);
            Assert.Greater(plan.Demolitions.Count, 0);
            Assert.IsNull(city.UpgradeRoad(road, RoadType.Avenue));
            Assert.AreEqual(RoadType.Avenue, road.Type);
            Assert.AreEqual(22.0, road.Width, 0.001);
            Assert.Less(city.Treasury.Balance, balance);
            Assert.Less(city.BuildingCount, before);

            for (int i = 0; i < 600; i++) { city.Step(); }
            Assert.Greater(city.BuildingCount, 5, "buildings grow back beside the wider road");
            foreach (Building b in city.Buildings)
            {
                foreach (Vec2 corner in b.Footprint)
                {
                    Assert.GreaterOrEqual(road.Curve.DistanceTo(corner), 10.5, "no building stands on the new road");
                }
            }
        }

        [Test]
        public void UpgradeRefusesWhatDoesNotFit()
        {
            CitySimulation city = Street();
            RoadSegment road = At(city, 0, 0);
            Assert.IsNotNull(city.UpgradeRoad(road, RoadType.Street), "same type");

            // A street joining the middle of the road cannot stay once the road becomes a highway.
            Assert.IsNotNull(city.PlaceRoad(city.PlanRoad(new Vec2(0, 0), null, new Vec2(0, 200), RoadType.Street)));
            RoadSegment west = At(city, -150, 0);
            Assert.IsNotNull(city.UpgradeRoad(west, RoadType.Highway));
            Assert.AreEqual(RoadType.Street, west.Type);

            // Streets and avenues mix freely.
            Assert.IsNull(city.UpgradeRoad(west, RoadType.Avenue));
        }

        [Test]
        public void UpgradeDropsLaneRulesAndCannotTouchTheOutsideRoad()
        {
            var city = new CitySimulation(2);
            city.Treasury.Enable(1e7);
            var oc = city.CreateOutsideConnection(new System.Random(1));
            Assert.IsNotNull(city.UpgradeRoad(oc.Segment, RoadType.OneWayHighway));
        }
    }
}
