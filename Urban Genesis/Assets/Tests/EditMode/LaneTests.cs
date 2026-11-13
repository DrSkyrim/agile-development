using NUnit.Framework;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Tests
{
    [TestFixture]
    public class LaneTests
    {
        private static CitySimulation Cross(RoadType arms, out RoadNode centre)
        {
            var city = new CitySimulation(1);
            city.PlaceRoad(city.PlanRoad(new Vec2(-200, 0), null, new Vec2(0, 0), arms));
            city.PlaceRoad(city.PlanRoad(new Vec2(0, 0), null, new Vec2(200, 0), arms));
            city.PlaceRoad(city.PlanRoad(new Vec2(0, 0), null, new Vec2(0, 200), arms));
            city.PlaceRoad(city.PlanRoad(new Vec2(0, 0), null, new Vec2(0, -200), arms));
            centre = city.Roads.FindIntersectionNear(new Vec2(0, 0), 5);
            return city;
        }

        private static RoadSegment Arm(RoadNode node, double dx, double dy)
        {
            foreach (RoadSegment s in node.Segments)
            {
                Vec2 other = s.A == node ? s.B.Position : s.A.Position;
                Vec2 d = other - node.Position;
                if (d.X * dx + d.Y * dy > 50.0) { return s; }
            }

            return null;
        }

        [Test]
        public void TheOneLaneHighwayIsKept()
        {
            var city = new CitySimulation(1);
            RoadSegment road = city.PlaceRoad(city.PlanRoad(new Vec2(-300, 0), null, new Vec2(300, 0), RoadType.TwoLaneHighway));
            Assert.IsNotNull(road);
            Assert.AreEqual("1+1", RoadSpecs.LaneLabel(RoadType.TwoLaneHighway));
            Assert.AreEqual(20.0, road.Width, 0.001);
            Assert.IsFalse(road.HasLots);
            Assert.IsFalse(city.PlanRoad(new Vec2(0, 200), null, new Vec2(0, 2)).IsValid, "ends only");
        }

        [Test]
        public void LaneOffsetsSitInsideTheRoadAndDifferPerLane()
        {
            foreach (RoadType t in RoadSpecs.All)
            {
                for (int lane = 0; lane < RoadSpecs.LanesPerDirection(t); lane++)
                {
                    double d = RoadSpecs.LaneOffset(t, lane);
                    Assert.LessOrEqual(d, RoadSpecs.HalfWidth(t), t.ToString());
                    Assert.GreaterOrEqual(d, 0.0, t.ToString());
                }
            }

            Assert.Greater(RoadSpecs.LaneOffset(RoadType.Avenue, 0), RoadSpecs.LaneOffset(RoadType.Avenue, 1));
            Assert.AreEqual(0.0, RoadSpecs.LaneOffset(RoadType.OneWayHighway, 0), 0.0001);
        }

        [Test]
        public void ArrivingLanesKeepToTheRight_AndLeavingSideIsTheOtherSide()
        {
            RoadNode centre;
            Cross(RoadType.Avenue, out centre);
            RoadSegment west = Arm(centre, -1, 0);
            // Arriving from the west heading east, "right" is south (negative y) with y pointing up.
            Vec2 lane = LaneGeometry.ArrivingLane(west, centre, 0, 30.0);
            Vec2 leave = LaneGeometry.LeavingSide(west, centre, 30.0);
            Assert.Less(lane.Y, 0.0);
            Assert.Greater(leave.Y, 0.0);
            Assert.AreEqual(-30.0, lane.X, 1.0);

            RoadSegment south = Arm(centre, 0, -1);
            Vec2 fromSouth = LaneGeometry.ArrivingLane(south, centre, 0, 30.0);
            Assert.Greater(fromSouth.X, 0.0, "heading north, right is east");
        }

        [Test]
        public void EveryTurnIsAllowedByDefault_ExceptUTurns()
        {
            RoadNode centre;
            Cross(RoadType.Avenue, out centre);
            RoadSegment west = Arm(centre, -1, 0);
            foreach (RoadSegment to in centre.Segments)
            {
                Assert.AreEqual(to != west, centre.IsTurnAllowed(west, 0, to));
                Assert.AreEqual(to != west, centre.IsTurnAllowed(west, 1, to));
            }

            Assert.AreEqual(3, centre.CountExits(west, 0));
            Assert.IsFalse(centre.IsTurnAllowed(west, 2, Arm(centre, 1, 0)), "no such lane");
        }

        [Test]
        public void ALaneCanBeLimitedToTurningOnly_ButNeverLeftWithNoExit()
        {
            RoadNode centre;
            CitySimulation city = Cross(RoadType.Avenue, out centre);
            RoadSegment west = Arm(centre, -1, 0), east = Arm(centre, 1, 0), north = Arm(centre, 0, 1), south = Arm(centre, 0, -1);
            string error;
            int changes = 0;
            city.LaneConnectionsChanged += n => changes++;

            Assert.IsTrue(city.SetLaneTurn(centre, west, 0, east, false, out error));
            Assert.IsTrue(city.SetLaneTurn(centre, west, 0, north, false, out error));
            Assert.IsFalse(centre.IsTurnAllowed(west, 0, east));
            Assert.IsTrue(centre.IsTurnAllowed(west, 1, east), "the other lane is untouched");
            Assert.AreEqual(1, centre.CountExits(west, 0));

            Assert.IsFalse(city.SetLaneTurn(centre, west, 0, south, false, out error));
            StringAssert.Contains("at least one exit", error);
            Assert.IsTrue(centre.IsTurnAllowed(west, 0, south));

            Assert.IsTrue(city.SetLaneTurn(centre, west, 0, east, true, out error));
            Assert.IsTrue(centre.IsTurnAllowed(west, 0, east));
            Assert.AreEqual(3, changes);

            city.ResetLaneTurns(centre);
            Assert.AreEqual(0, centre.BlockedTurns.Count);
        }

        [Test]
        public void ImpossibleTurnsAreRefused()
        {
            var city = new CitySimulation(1);
            city.PlaceRoad(city.PlanRoad(new Vec2(-300, 0), null, new Vec2(0, 0), RoadType.OneWayHighway));
            city.PlaceRoad(city.PlanRoad(new Vec2(0, 0), null, new Vec2(300, 0), RoadType.OneWayHighway));
            city.PlaceRoad(city.PlanRoad(new Vec2(0, 0), null, new Vec2(0, 200), RoadType.Street));
            city.PlaceRoad(city.PlanRoad(new Vec2(0, 0), null, new Vec2(0, -200), RoadType.Street));
            RoadNode centre = city.Roads.FindIntersectionNear(new Vec2(0, 0), 5);
            RoadSegment inbound = Arm(centre, -1, 0), outbound = Arm(centre, 1, 0), north = Arm(centre, 0, 1);

            Assert.AreEqual(1, centre.ArrivingLanes(inbound));
            Assert.AreEqual(0, centre.ArrivingLanes(outbound), "one-way leading away");
            Assert.IsFalse(centre.IsTurnPossible(north, inbound), "cannot leave along a one-way that leads in");
            string error;
            Assert.IsFalse(city.SetLaneTurn(centre, north, 0, inbound, false, out error));
            Assert.IsTrue(centre.IsTurnAllowed(north, 0, outbound));
        }

        [Test]
        public void LaneRulesAreSaved_AndSurviveSplitsAndRemovals()
        {
            RoadNode centre;
            CitySimulation city = Cross(RoadType.Avenue, out centre);
            RoadSegment west = Arm(centre, -1, 0), east = Arm(centre, 1, 0);
            string error;
            Assert.IsTrue(city.SetLaneTurn(centre, west, 1, east, false, out error));

            int skipped;
            CitySimulation loaded = CitySimulation.Restore(city.Capture(), out skipped);
            RoadNode again = loaded.Roads.FindIntersectionNear(new Vec2(0, 0), 5);
            Assert.IsFalse(again.IsTurnAllowed(Arm(again, -1, 0), 1, Arm(again, 1, 0)));
            Assert.IsTrue(again.IsTurnAllowed(Arm(again, -1, 0), 0, Arm(again, 1, 0)));

            // A street joining the west arm halfway splits it; the rule must follow the piece that still touches the junction.
            Assert.IsNotNull(city.PlaceRoad(city.PlanRoad(new Vec2(-100, 150), null, new Vec2(-100, 2), RoadType.Street)));
            RoadNode after = city.Roads.FindIntersectionNear(new Vec2(0, 0), 5);
            Assert.AreEqual(1, after.BlockedTurns.Count);
            Assert.IsFalse(after.IsTurnAllowed(Arm(after, -1, 0), 1, Arm(after, 1, 0)));

            city.Roads.RemoveSegment(Arm(after, 1, 0));
            Assert.AreEqual(0, after.BlockedTurns.Count, "the rule for a removed road is dropped");
        }

        [Test]
        public void AOneWayHighwayCanBranchOffAHighwayAnywhere_ButNothingElseCan()
        {
            var city = new CitySimulation(1);
            RoadSegment highway = city.PlaceRoad(city.PlanRoad(new Vec2(-400, 0), null, new Vec2(400, 0), RoadType.Highway));
            Assert.IsNotNull(highway);

            Assert.IsFalse(city.PlanRoad(new Vec2(0, 300), null, new Vec2(0, 2), RoadType.Street).IsValid, "a street cannot join");
            Assert.IsTrue(city.PlanRoad(new Vec2(0, 300), null, new Vec2(0, 2), RoadType.Highway).IsValid, "a highway can join another highway");

            RoadPlan exit = city.PlanRoad(new Vec2(0, 0), null, new Vec2(250, 250), RoadType.OneWayHighway);
            Assert.IsTrue(exit.IsValid, exit.Error);
            Assert.IsNotNull(city.PlaceRoad(exit));
            Assert.AreEqual(3, city.Roads.SegmentCount, "the highway was split in two, plus the ramp");

            RoadNode junction = city.Roads.FindIntersectionNear(new Vec2(0, 0), 10);
            Assert.IsNotNull(junction);
            Assert.IsFalse(junction.CanHaveTrafficLights, "a free-flowing highway junction has no lights");
            Assert.IsFalse(junction.HasTrafficLight);
            Assert.IsFalse(city.SetTrafficLights(junction, true));
        }

        [Test]
        public void ARampCanAlsoMergeBackOntoAHighway()
        {
            var city = new CitySimulation(1);
            city.PlaceRoad(city.PlanRoad(new Vec2(-400, 0), null, new Vec2(400, 0), RoadType.Highway));
            RoadPlan entry = city.PlanRoad(new Vec2(200, 300), null, new Vec2(120, 0), RoadType.OneWayHighway);
            Assert.IsTrue(entry.IsValid, entry.Error);
        }

        [Test]
        public void ARampCanEndOnTheMiddleOfAStreet()
        {
            var city = new CitySimulation(1);
            city.PlaceRoad(city.PlanRoad(new Vec2(-300, 0), null, new Vec2(300, 0)));
            RoadPlan plan = city.PlanRoad(new Vec2(0, 300), null, new Vec2(0, 2), RoadType.OneWayHighway);
            Assert.IsTrue(plan.IsValid, plan.Error);
        }
    }
}
