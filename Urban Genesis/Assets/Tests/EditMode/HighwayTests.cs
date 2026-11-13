using NUnit.Framework;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Tests
{
    [TestFixture]
    public class HighwayTests
    {
        private static readonly Vec2 A = new Vec2(-300, 0);
        private static readonly Vec2 B = new Vec2(300, 0);

        [Test]
        public void AHighwayIsWiderAndHasNoLots()
        {
            var city = new CitySimulation(1);
            RoadSegment road = city.PlaceRoad(city.PlanRoad(A, null, B, RoadType.Highway));
            Assert.IsNotNull(road);
            Assert.AreEqual(RoadType.Highway, road.Type);
            Assert.AreEqual(28.0, road.Width, 0.001);
            Assert.IsFalse(road.HasLots);
            Assert.AreEqual(0, LotLayout.Cols(road));
        }

        [Test]
        public void AHighwayTooShortOrTooSharp_IsRefused()
        {
            var city = new CitySimulation(1);
            Assert.IsFalse(city.PlanRoad(new Vec2(0, 0), null, new Vec2(60, 0), RoadType.Highway).IsValid);
            Assert.IsFalse(city.PlanRoad(new Vec2(-100, 0), new Vec2(0, 300), new Vec2(100, 0), RoadType.Highway).IsValid);
        }

        [Test]
        public void AStreetCannotJoinTheMiddleOfAHighway_ButCanJoinItsEnd()
        {
            var city = new CitySimulation(1);
            city.PlaceRoad(city.PlanRoad(A, null, B, RoadType.Highway));
            Assert.IsFalse(city.PlanRoad(new Vec2(0, 200), null, new Vec2(0, 2), RoadType.Street).IsValid);
            Assert.IsTrue(city.PlanRoad(B, null, new Vec2(300, 200), RoadType.Street).IsValid);
        }

        [Test]
        public void AHighwayCannotJoinTheMiddleOfAStreet()
        {
            var city = new CitySimulation(1);
            city.PlaceRoad(city.PlanRoad(A, null, B));
            Assert.IsFalse(city.PlanRoad(new Vec2(0, 300), null, new Vec2(0, 2), RoadType.Highway).IsValid);
        }

        [Test]
        public void HighwaysCostAndUpkeepMore_AndAreFasterToTravel()
        {
            var city = new CitySimulation(1);
            city.Treasury.Enable(100000.0);
            RoadPlan street = city.PlanRoad(A, null, B);
            RoadPlan highway = city.PlanRoad(new Vec2(-300, 100), null, new Vec2(300, 100), RoadType.Highway);
            Assert.Greater(highway.Cost, street.Cost * 2.5);
            Assert.AreEqual(2.0, RoadSpecs.SpeedFactor(RoadType.Highway), 0.001);
            Assert.AreEqual(1.0, RoadSpecs.SpeedFactor(RoadType.Street), 0.001);
        }

        [Test]
        public void TheOutsideConnection_IsAValidFreeHighwayInsideTheMap()
        {
            for (int seed = 0; seed < 25; seed++)
            {
                var city = new CitySimulation(1);
                city.Treasury.Enable(1000.0);
                var oc = city.CreateOutsideConnection(new System.Random(seed));
                Assert.IsNotNull(oc, "seed " + seed);
                Assert.AreEqual(RoadType.Highway, oc.Segment.Type);
                Assert.IsTrue(oc.Segment.IsOutsideConnection);
                Assert.AreEqual(1000.0, city.Treasury.Balance, 0.0001);
                Assert.IsTrue(city.Roads.WorldBounds.Contains(oc.EdgeNode.Position));
                Assert.Less(oc.CityNode.Position.Length, 500.0);
            }
        }

        [Test]
        public void TheOutsideConnection_CannotBeBulldozed_AndHasNoUpkeep()
        {
            var city = new CitySimulation(1);
            city.Treasury.Enable(1000.0);
            var oc = city.CreateOutsideConnection(new System.Random(3));
            Assert.AreEqual(0.0, city.Treasury.WeeklyUpkeep, 0.0001);
            Vec2 mid = oc.Segment.Curve.Point(0.5);
            city.Bulldoze(mid, 10.0, true);
            Assert.AreEqual(1, city.Roads.SegmentCount);
            Assert.AreEqual(1000.0, city.Treasury.Balance, 0.0001);
        }
    }
}
