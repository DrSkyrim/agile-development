using System.Collections.Generic;
using NUnit.Framework;
using UrbanGenesis.Simulation;
using UrbanGenesis.Simulation.Traffic;

namespace UrbanGenesis.Tests
{
    [TestFixture]
    public class TrafficTests
    {
        private static CitySimulation Cross(RoadType type = RoadType.Street)
        {
            var city = new CitySimulation(1);
            city.Treasury.Enable(1e7);
            city.PlaceRoad(city.PlanRoad(new Vec2(-300, 0), null, new Vec2(300, 0), type));
            city.PlaceRoad(city.PlanRoad(new Vec2(0, 0), null, new Vec2(0, 300), RoadType.Street));
            return city;
        }

        private static RoadSegment Find(CitySimulation city, Vec2 p)
        {
            double s;
            return city.Roads.FindSegmentNear(p, 5.0, out s);
        }

        [Test]
        public void RoutePlannerFindsTurnsAndRespectsOneWayRoads()
        {
            CitySimulation city = Cross();
            var planner = new RoutePlanner(city.Roads);
            RoadSegment west = Find(city, new Vec2(-150, 0));
            RoadSegment north = Find(city, new Vec2(0, 150));
            List<RouteStep> route = planner.Plan(west, 50, north, 100);
            Assert.IsNotNull(route);
            Assert.AreEqual(2, route.Count);
            Assert.AreSame(north, route[1].Segment);

            var oneWay = new CitySimulation(1);
            oneWay.Treasury.Enable(1e7);
            oneWay.PlaceRoad(oneWay.PlanRoad(new Vec2(-300, 0), null, new Vec2(300, 0), RoadType.OneWayHighway));
            RoadSegment seg = Find(oneWay, new Vec2(0, 0));
            var p2 = new RoutePlanner(oneWay.Roads);
            Assert.IsNotNull(p2.Plan(seg, 100, seg, 400));
            Assert.IsNull(p2.Plan(seg, 400, seg, 100), "cannot drive a one-way road backwards");
        }

        [Test]
        public void VehiclesDriveToTheirDestinationAndStayApart()
        {
            CitySimulation city = Cross();
            var traffic = new CityTraffic(city, 1);
            traffic.TripsPerVolume = 0.0;
            RoadSegment west = Find(city, new Vec2(-150, 0));
            RoadSegment east = Find(city, new Vec2(150, 0));
            RoadSegment north = Find(city, new Vec2(0, 150));
            Assert.IsTrue(traffic.TestSpawn(VehicleType.Car, west, 20, east, 120));
            Assert.IsTrue(traffic.TestSpawn(VehicleType.LongTruck, west, 60, north, 100));
            for (int i = 0; i < 4000 && traffic.Count > 0; i++)
            {
                traffic.Advance(0.1);
                for (int a = 0; a < traffic.Vehicles.Count; a++)
                {
                    for (int b = a + 1; b < traffic.Vehicles.Count; b++)
                    {
                        Vehicle va = traffic.Vehicles[a], vb = traffic.Vehicles[b];
                        if (va.Step.Segment == vb.Step.Segment && va.Step.Forward == vb.Step.Forward && va.Lane == vb.Lane)
                        {
                            Assert.Greater(System.Math.Abs(va.S - vb.S), 1.0);
                        }
                    }
                }
            }

            Assert.AreEqual(0, traffic.Count, "everyone should have arrived");
            Assert.AreEqual(2, traffic.Arrivals);
        }

        [Test]
        public void VehiclesTurnSmoothlyOnCurvedRoadsAndTJunctions()
        {
            var city = new CitySimulation(1);
            city.Treasury.Enable(1e7);
            city.PlaceRoad(city.PlanRoad(new Vec2(-300, 0), null, new Vec2(300, 0), RoadType.Avenue));
            // A curved street that leaves the avenue at an angle, plus a curved one the other way.
            Assert.IsNotNull(city.PlaceRoad(city.PlanRoad(new Vec2(0, 0), new Vec2(60, 150), new Vec2(250, 260), RoadType.Street)));
            Assert.IsNotNull(city.PlaceRoad(city.PlanRoad(new Vec2(-150, 0), new Vec2(-150, -120), new Vec2(-300, -200), RoadType.Street)));
            var traffic = new CityTraffic(city, 1);
            RoadSegment curvedA = Find(city, new Vec2(250, 260));
            RoadSegment curvedB = Find(city, new Vec2(-300, -200));
            RoadSegment east = Find(city, new Vec2(200, 0));
            for (int trip = 0; trip < 4; trip++)
            {
                RoadSegment from = trip % 2 == 0 ? curvedA : curvedB;
                RoadSegment to = trip % 2 == 0 ? curvedB : east;
                Assert.IsTrue(traffic.TestSpawn(trip == 1 ? VehicleType.LongTruck : VehicleType.Car, from, from.Length - 30, to, to.Length * 0.5), "trip " + trip);
                Vehicle v = traffic.Vehicles[traffic.Vehicles.Count - 1];
                Vec2 lastPos = v.PositionAt(v.S, 0.0);
                Vec2 lastHeading = v.Heading;
                double worstJump = 0.0, worstTurn = 0.0;
                for (int i = 0; i < 6000 && !v.Arrived && traffic.Vehicles.Contains(v); i++)
                {
                    traffic.Advance(0.1);
                    if (v.Arrived || !traffic.Vehicles.Contains(v)) { break; }
                    Vec2 pos = v.PositionAt(v.S, 0.0);
                    Vec2 mid = v.PositionAt(v.S, v.Length * 0.5);
                    Vec2 rear = v.PositionAt(v.S, v.Length);
                    Vec2 chord = (pos - rear).Normalized;
                    worstJump = System.Math.Max(worstJump, Vec2.Distance(pos, lastPos) - v.Speed * 0.1);
                    double turn = System.Math.Acos(System.Math.Max(-1.0, System.Math.Min(1.0, Vec2.Dot(chord, lastHeading))));
                    worstTurn = System.Math.Max(worstTurn, turn);
                    lastPos = pos;
                    lastHeading = chord;
                    Assert.Less(Vec2.Distance(pos, mid), v.Length, "body stays together");
                }

                Assert.Less(worstJump, 1.0, "front does not jump (trip " + trip + ")");
                Assert.Less(worstTurn, 0.35, "heading does not spin (trip " + trip + "): " + worstTurn);
                traffic.Advance(0.1);
            }
        }

        [Test]
        public void VehiclesKeepToTheLaneConnectionsThePlayerSet()
        {
            var city = new CitySimulation(1);
            city.Treasury.Enable(1e7);
            city.PlaceRoad(city.PlanRoad(new Vec2(-300, 0), null, new Vec2(0, 0), RoadType.Avenue));
            city.PlaceRoad(city.PlanRoad(new Vec2(0, 0), null, new Vec2(300, 0), RoadType.Avenue));
            city.PlaceRoad(city.PlanRoad(new Vec2(0, 0), null, new Vec2(0, 200), RoadType.Street));
            RoadNode centre = city.Roads.FindIntersectionNear(new Vec2(0, 0), 5);
            RoadSegment west = Find(city, new Vec2(-150, 0)), east = Find(city, new Vec2(150, 0));
            string error;
            // Forbid crossing over: each lane may only continue into the same-numbered lane.
            Assert.IsTrue(city.SetLaneConnection(centre, west, 0, east, 1, false, out error), error);
            Assert.IsTrue(city.SetLaneConnection(centre, west, 1, east, 0, false, out error), error);
            var traffic = new CityTraffic(city, 3);
            int checkedCrossings = 0;
            for (int n = 0; n < 6; n++)
            {
                Assert.IsTrue(traffic.TestSpawn(VehicleType.Car, west, 20 + n * 25, east, 200));
                Vehicle v = traffic.Vehicles[traffic.Vehicles.Count - 1];
                int enteredFrom = -1;
                for (int i = 0; i < 3000 && traffic.Vehicles.Contains(v); i++)
                {
                    traffic.Advance(0.1);
                    if (v.StepIndex == 1 && enteredFrom < 0 && !v.Arrived)
                    {
                        enteredFrom = v.PreviousLane;
                        Assert.AreEqual(enteredFrom, v.Lane, "a lane continues only into its own number");
                        checkedCrossings++;
                    }
                }
            }

            Assert.Greater(checkedCrossings, 0);
        }

        [Test]
        public void SignalsCycleAndNeverShowGreenToCrossingArmsTogether()
        {
            CitySimulation city = Cross();
            var traffic = new CityTraffic(city, 1);
            RoadNode node = null;
            foreach (RoadNode n in city.Roads.Nodes) { if (n.IsIntersection) { node = n; } }
            Assert.IsNotNull(node);
            Assert.IsTrue(node.HasTrafficLight);
            bool sawGreen = false;
            for (double t = 0; t < 60; t += 0.25)
            {
                int greens = 0;
                foreach (RoadSegment arm in node.Segments)
                {
                    if (traffic.Signals.StateFor(node, arm, t) == SignalState.Green) { greens++; sawGreen = true; }
                }

                Assert.LessOrEqual(greens, 2);
            }

            Assert.IsTrue(sawGreen);
        }

        [Test]
        public void TrafficFromTheEconomyAppearsAndRemovedRoadsClearIt()
        {
            var city = new CitySimulation(7);
            city.Treasury.Enable(1e8);
            city.SpawnChance = 1.0;
            city.PlaceRoad(city.PlanRoad(new Vec2(-300, 0), null, new Vec2(300, 0), RoadType.Street));
            city.PlaceRoad(city.PlanRoad(new Vec2(0, 0), null, new Vec2(0, 300), RoadType.Street));
            city.PaintZone(new Vec2(-150, 20), 120, ZoneType.Residential);
            city.PaintZone(new Vec2(150, 20), 120, ZoneType.Commercial);
            var traffic = new CityTraffic(city, 2);
            traffic.TripsPerVolume = 0.5;
            int peak = 0;
            for (int i = 0; i < 1500; i++)
            {
                city.Step();
                traffic.OnTick();
                traffic.Advance(0.5);
                if (traffic.Count > peak) { peak = traffic.Count; }
            }

            Assert.Greater(peak, 0, "trips should put vehicles on the road");
            Assert.Greater(traffic.Arrivals, 0);
            for (int x = -300; x <= 300; x += 20) { city.Bulldoze(new Vec2(x, 0), 30, true); city.Bulldoze(new Vec2(0, System.Math.Abs(x)), 30, true); }
            Assert.AreEqual(0, city.Roads.SegmentCount);
            traffic.Advance(0.5);
            Assert.AreEqual(0, traffic.Count);
        }
    }
}
