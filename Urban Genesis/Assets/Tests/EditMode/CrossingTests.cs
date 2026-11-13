using NUnit.Framework;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Tests
{
    [TestFixture]
    public class CrossingTests
    {
        private static CitySimulation WithEastWestHighway(RoadType type = RoadType.Highway)
        {
            var city = new CitySimulation(1);
            city.Treasury.Enable(100000.0);
            Assert.IsNotNull(city.PlaceRoad(city.PlanRoad(new Vec2(-500, 0), null, new Vec2(500, 0), type)));
            return city;
        }

        [Test]
        public void AHighwayCanCrossAnotherHighway_MakingAFourWayJunction()
        {
            CitySimulation city = WithEastWestHighway();
            RoadPlan plan = city.PlanRoad(new Vec2(0, -400), null, new Vec2(0, 400), RoadType.Highway);
            Assert.IsTrue(plan.IsValid, plan.Error);
            Assert.IsNotNull(city.PlaceRoad(plan));

            Assert.AreEqual(4, city.Roads.SegmentCount, "both highways were split");
            RoadNode junction = city.Roads.FindIntersectionNear(new Vec2(0, 0), 3);
            Assert.IsNotNull(junction);
            Assert.AreEqual(4, junction.Degree);
            Assert.IsFalse(junction.HasTrafficLight);
            foreach (RoadSegment s in junction.Segments)
            {
                Assert.AreEqual(RoadType.Highway, s.Type);
            }
        }

        [Test]
        public void AHighwayCrossingIsPaidForByItsWholeLength()
        {
            CitySimulation city = WithEastWestHighway();
            double before = city.Treasury.Balance;
            RoadPlan plan = city.PlanRoad(new Vec2(0, -400), null, new Vec2(0, 400), RoadType.Highway);
            city.PlaceRoad(plan);
            Assert.AreEqual(before - plan.Cost, city.Treasury.Balance, 0.01);
            Assert.AreEqual(800.0 * city.Treasury.HighwayBuildCostPerMetre, plan.Cost, 5.0);
        }

        [Test]
        public void ACrossingMustBeSteepEnoughAndAwayFromEnds()
        {
            CitySimulation city = WithEastWestHighway();
            Assert.IsFalse(city.PlanRoad(new Vec2(-400, -150), null, new Vec2(400, 150), RoadType.Highway).IsValid, "about 20 degrees is too shallow");
            Assert.IsFalse(city.PlanRoad(new Vec2(-480, -300), null, new Vec2(-480, 300), RoadType.Highway).IsValid, "too close to the highway's end");
        }

        [Test]
        public void AStreetStillCannotCrossAHighway()
        {
            CitySimulation city = WithEastWestHighway();
            Assert.IsFalse(city.PlanRoad(new Vec2(0, -300), null, new Vec2(0, 300), RoadType.Street).IsValid);
        }

        [Test]
        public void ACrossingSurvivesSaving_AndAllowsLaneRules()
        {
            CitySimulation city = WithEastWestHighway();
            city.PlaceRoad(city.PlanRoad(new Vec2(0, -400), null, new Vec2(0, 400), RoadType.Highway));
            int skipped;
            CitySimulation loaded = CitySimulation.Restore(city.Capture(), out skipped);
            Assert.AreEqual(4, loaded.Roads.SegmentCount);
            RoadNode junction = loaded.Roads.FindIntersectionNear(new Vec2(0, 0), 3);
            Assert.AreEqual(4, junction.Degree);
            RoadSegment from = junction.Segments[0];
            Assert.AreEqual(3, junction.CountExits(from, 0));
        }

        [Test]
        public void AHighwayCanJoinTheMiddleOfAnotherHighway()
        {
            CitySimulation city = WithEastWestHighway();
            RoadPlan plan = city.PlanRoad(new Vec2(0, 300), null, new Vec2(0, 0), RoadType.TwoLaneHighway);
            Assert.IsTrue(plan.IsValid, plan.Error);
            city.PlaceRoad(plan);
            Assert.AreEqual(3, city.Roads.SegmentCount);
        }

        [Test]
        public void OnlyOneWayHighwaysMaySkipTheHundredMetreMinimum()
        {
            var city = new CitySimulation(1);
            Assert.IsTrue(city.PlanRoad(new Vec2(0, 0), null, new Vec2(60, 0), RoadType.OneWayHighway).IsValid);
            Assert.IsFalse(city.PlanRoad(new Vec2(0, 0), null, new Vec2(45, 0), RoadType.OneWayHighway).IsValid, "under 50 m");
            Assert.IsFalse(city.PlanRoad(new Vec2(0, 0), null, new Vec2(60, 0), RoadType.Highway).IsValid, "highways still need 100 m");
            Assert.IsFalse(city.PlanRoad(new Vec2(0, 0), null, new Vec2(60, 0), RoadType.TwoLaneHighway).IsValid);
            Assert.AreEqual(50.0, RoadSpecs.MinLength(RoadType.OneWayHighway), 0.001);
            Assert.AreEqual(100.0, RoadSpecs.MinLength(RoadType.Highway), 0.001);
        }
    }
}
