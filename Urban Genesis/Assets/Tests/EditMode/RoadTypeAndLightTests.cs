using NUnit.Framework;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Tests
{
    [TestFixture]
    public class RoadTypeAndLightTests
    {
        private static readonly Vec2 W = new Vec2(-300, 0);
        private static readonly Vec2 E = new Vec2(300, 0);

        [Test]
        public void TheFourTypesHaveTheirLanesAndWidths()
        {
            Assert.AreEqual("1+1", RoadSpecs.LaneLabel(RoadType.Street));
            Assert.AreEqual("2+2", RoadSpecs.LaneLabel(RoadType.Avenue));
            Assert.AreEqual("2+2", RoadSpecs.LaneLabel(RoadType.Highway));
            Assert.AreEqual("1 lane, one way", RoadSpecs.LaneLabel(RoadType.OneWayHighway));
            Assert.Less(RoadSpecs.Width(RoadType.Street), RoadSpecs.Width(RoadType.Avenue));
            Assert.Less(RoadSpecs.Width(RoadType.Avenue), RoadSpecs.Width(RoadType.Highway));
            foreach (RoadType t in RoadSpecs.All)
            {
                Assert.LessOrEqual(RoadSpecs.Width(t), RoadSpecs.MaxWidth);
            }
        }

        [Test]
        public void AnAvenueHasLots_AndStreetsCanJoinItsMiddle()
        {
            var city = new CitySimulation(1);
            RoadSegment avenue = city.PlaceRoad(city.PlanRoad(W, null, E, RoadType.Avenue));
            Assert.IsTrue(avenue.HasLots);
            Assert.Greater(LotLayout.Cols(avenue), 0);
            RoadPlan side = city.PlanRoad(new Vec2(0, 200), null, new Vec2(0, 2), RoadType.Street);
            Assert.IsTrue(side.IsValid, side.Error);
        }

        [Test]
        public void AOneWayHighwayHasNoLots_ConnectsOnlyAtItsEnds_AndIsOneWay()
        {
            var city = new CitySimulation(1);
            RoadSegment road = city.PlaceRoad(city.PlanRoad(W, null, E, RoadType.OneWayHighway));
            Assert.IsFalse(road.HasLots);
            Assert.IsTrue(RoadSpecs.IsOneWay(road.Type));
            Assert.AreEqual(W.X, road.A.Position.X, 0.5, "runs from where it was started");
            Assert.IsFalse(city.PlanRoad(new Vec2(0, 200), null, new Vec2(0, 2), RoadType.Street).IsValid);
        }

        [Test]
        public void EveryTypeHasItsOwnPrice()
        {
            var city = new CitySimulation(1);
            city.Treasury.Enable(1000.0);
            double street = city.Treasury.RoadCost(100, RoadType.Street);
            double avenue = city.Treasury.RoadCost(100, RoadType.Avenue);
            double highway = city.Treasury.RoadCost(100, RoadType.Highway);
            Assert.Less(street, avenue);
            Assert.Less(avenue, highway);
        }

        private static CitySimulation Crossing(out RoadNode centre)
        {
            var city = new CitySimulation(1);
            city.PlaceRoad(city.PlanRoad(new Vec2(-200, 0), null, new Vec2(0, 0)));
            city.PlaceRoad(city.PlanRoad(new Vec2(0, 0), null, new Vec2(200, 0)));
            city.PlaceRoad(city.PlanRoad(new Vec2(0, 0), null, new Vec2(0, 200)));
            city.PlaceRoad(city.PlanRoad(new Vec2(0, 0), null, new Vec2(0, -200)));
            centre = city.Roads.FindIntersectionNear(new Vec2(0, 0), 5);
            return city;
        }

        [Test]
        public void ATwoRoadBendHasNoLights_ButTJunctionsAndFourWayCrossingsDo()
        {
            var city = new CitySimulation(1);
            city.PlaceRoad(city.PlanRoad(new Vec2(-200, 0), null, new Vec2(0, 0)));
            city.PlaceRoad(city.PlanRoad(new Vec2(0, 0), null, new Vec2(0, 200)));
            RoadNode bend = city.Roads.FindNodeNear(new Vec2(0, 0), 5);
            Assert.IsFalse(bend.IsIntersection);
            Assert.IsFalse(bend.HasTrafficLight);
            Assert.IsNull(city.Roads.FindIntersectionNear(new Vec2(0, 0), 5));

            city.PlaceRoad(city.PlanRoad(new Vec2(0, 0), null, new Vec2(200, 0)));
            Assert.AreEqual(3, bend.Degree);
            Assert.IsTrue(bend.HasTrafficLight, "T-junction");

            city.PlaceRoad(city.PlanRoad(new Vec2(0, 0), null, new Vec2(0, -200)));
            Assert.AreEqual(4, bend.Degree);
            Assert.IsTrue(bend.HasTrafficLight, "four-way");
        }

        [Test]
        public void LightsCanBeSwitchedOffAndOn_AndTheChangeIsAnnounced()
        {
            RoadNode centre;
            CitySimulation city = Crossing(out centre);
            Assert.AreEqual(4, centre.Degree);
            int changes = 0;
            city.TrafficLightsChanged += n => changes++;

            Assert.IsTrue(city.SetTrafficLights(centre, false));
            Assert.IsFalse(centre.HasTrafficLight);
            Assert.IsFalse(city.SetTrafficLights(centre, false), "already off");
            Assert.IsTrue(city.SetTrafficLights(centre, true));
            Assert.IsTrue(centre.HasTrafficLight);
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void LightsCannotBeSwitchedOnAtADeadEnd()
        {
            var city = new CitySimulation(1);
            city.PlaceRoad(city.PlanRoad(W, null, E));
            RoadNode end = city.Roads.FindNodeNear(E, 5);
            Assert.IsFalse(city.SetTrafficLights(end, false));
        }

        [Test]
        public void SwitchedOffLightsAreSaved()
        {
            RoadNode centre;
            CitySimulation city = Crossing(out centre);
            city.SetTrafficLights(centre, false);
            int skipped;
            CitySimulation loaded = CitySimulation.Restore(city.Capture(), out skipped);
            RoadNode again = loaded.Roads.FindIntersectionNear(new Vec2(0, 0), 5);
            Assert.IsNotNull(again);
            Assert.IsFalse(again.HasTrafficLight);
        }

        [Test]
        public void NewTypesSurviveSaving()
        {
            var city = new CitySimulation(1);
            city.PlaceRoad(city.PlanRoad(W, null, E, RoadType.Avenue));
            city.PlaceRoad(city.PlanRoad(new Vec2(-300, 300), null, new Vec2(300, 300), RoadType.OneWayHighway));
            int skipped;
            CitySimulation loaded = CitySimulation.Restore(city.Capture(), out skipped);
            var types = new System.Collections.Generic.List<RoadType>();
            foreach (RoadSegment s in loaded.Roads.Segments) { types.Add(s.Type); }
            Assert.AreEqual(2, types.Count);
            Assert.IsTrue(types.Contains(RoadType.Avenue));
            Assert.IsTrue(types.Contains(RoadType.OneWayHighway));
        }

        private static int GrowBesideRoad(RoadType type, ZoneType zone)
        {
            var city = new CitySimulation(3);
            city.SpawnChance = 1.0;
            city.Economy.DemandDrivesGrowth = false;
            city.PlaceRoad(city.PlanRoad(new Vec2(-250, -200), null, new Vec2(250, 200), type));
            city.PaintZone(new Vec2(0, 0), 500, zone);
            for (int i = 0; i < 600; i++) { city.Step(); }
            return city.BuildingCount;
        }

        [Test]
        public void BuildingsGrowBesideAvenuesJustAsOnStreets()
        {
            foreach (ZoneType zone in new[] { ZoneType.Residential, ZoneType.Commercial, ZoneType.Industrial })
            {
                int onStreet = GrowBesideRoad(RoadType.Street, zone);
                int onAvenue = GrowBesideRoad(RoadType.Avenue, zone);
                Assert.Greater(onStreet, 10, zone + " on a street");
                Assert.GreaterOrEqual(onAvenue, onStreet * 0.7, zone + " on an avenue: " + onAvenue + " vs " + onStreet);
            }
        }
    }
}
