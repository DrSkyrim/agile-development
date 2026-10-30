using System.Collections.Generic;
using NUnit.Framework;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Tests
{
    [TestFixture]
    public class LotLayoutTests
    {
        private static CitySimulation NewCity()
        {
            return new CitySimulation(1);
        }

        private static RoadSegment Road(CitySimulation city, Vec2 a, Vec2 b)
        {
            RoadPlan plan = city.PlanRoad(a, null, b);
            Assert.IsTrue(plan.IsValid, plan.Error);
            return city.PlaceRoad(plan);
        }

        [Test]
        public void AStraightRoad_GetsFourRowsOfLotsOnBothSides()
        {
            CitySimulation city = NewCity();
            Road(city, new Vec2(-200, 0), new Vec2(200, 0));

            Assert.AreEqual(50 * 4 * 2, city.Lots.Count);
        }

        [Test]
        public void Lots_SitBesideTheRoad_NotUnderIt()
        {
            CitySimulation city = NewCity();
            RoadSegment s = Road(city, new Vec2(-200, 0), new Vec2(200, 0));

            Lot left = city.Lots.Get(new LotKey(s.Id, 0, 3, 0));
            Lot right = city.Lots.Get(new LotKey(s.Id, 1, 3, 0));
            Lot deep = city.Lots.Get(new LotKey(s.Id, 0, 3, 3));

            Assert.AreEqual(-200 + 3 * 8 + 4, left.Center.X, 1e-6);
            Assert.AreEqual(12.0, left.Center.Y, 1e-6);
            Assert.AreEqual(-12.0, right.Center.Y, 1e-6);
            Assert.AreEqual(36.0, deep.Center.Y, 1e-6);
            Assert.IsTrue(Geometry.IsConvexAnticlockwise(right.Corners));
        }

        [Test]
        public void LotsAreEightMetresSquare_OnAStraightRoad()
        {
            CitySimulation city = NewCity();
            RoadSegment s = Road(city, new Vec2(-200, 0), new Vec2(200, 0));
            Lot lot = city.Lots.Get(new LotKey(s.Id, 0, 5, 1));

            Assert.AreEqual(64.0, Geometry.SignedArea(lot.Corners), 1e-6);
        }

        [Test]
        public void LotsOnACurve_FollowItAndStayConvex()
        {
            CitySimulation city = NewCity();
            Vec2 a = new Vec2(-150, 0);
            Vec2 b = new Vec2(150, 0);
            RoadPlan plan = city.PlanRoad(a, RoadNetwork.ControlThrough(a, new Vec2(0, 90), b), b);
            Assert.IsTrue(plan.IsValid, plan.Error);
            RoadSegment s = city.PlaceRoad(plan);

            int count = 0;
            foreach (Lot lot in city.Lots.Lots)
            {
                Assert.AreEqual(s.Id, lot.Key.SegmentId);
                Assert.IsTrue(Geometry.IsConvexAnticlockwise(lot.Corners), lot.Key + " is not convex");
                Assert.GreaterOrEqual(s.Curve.DistanceTo(lot.Center), RoadNetwork.HalfWidth - 0.9, lot.Key + " is on the road");
                count++;
            }

            Assert.Greater(count, 100);
        }

        [Test]
        public void ARoadBuiltThroughTheLots_RemovesTheLotsItCovers()
        {
            CitySimulation city = NewCity();
            Road(city, new Vec2(-200, 0), new Vec2(200, 0));
            RoadSegment stub = Road(city, new Vec2(0, -150), new Vec2(0, 3));

            foreach (Lot lot in city.Lots.Lots)
            {
                if (lot.Key.SegmentId != stub.Id)
                {
                    Assert.GreaterOrEqual(stub.Curve.DistanceTo(lot.Center), RoadNetwork.HalfWidth - 0.9, lot.Key + " is under the new road");
                }
            }

            Assert.Greater(city.Lots.Count, 0);
        }

        [Test]
        public void SplittingARoad_KeepsTheZoneOfTheLotsAlongIt()
        {
            CitySimulation city = NewCity();
            Road(city, new Vec2(-200, 0), new Vec2(200, 0));
            city.PaintZone(new Vec2(-120, 12), 6, ZoneType.Residential);
            int zonedBefore = CountZoned(city, ZoneType.Residential);
            Assert.Greater(zonedBefore, 0);

            Road(city, new Vec2(100, -150), new Vec2(100, 3));

            Assert.AreEqual(zonedBefore, CountZoned(city, ZoneType.Residential));
        }

        [Test]
        public void RemovingARoad_RemovesItsLots()
        {
            CitySimulation city = NewCity();
            Road(city, new Vec2(-200, 0), new Vec2(200, 0));
            Assert.Greater(city.Lots.Count, 0);

            city.Bulldoze(new Vec2(0, 0), 4, true);

            Assert.AreEqual(0, city.Lots.Count);
            Assert.AreEqual(0, city.Roads.SegmentCount);
        }

        [Test]
        public void LotsInCircle_ReturnsOnlyLotsWithTheirCentreInside()
        {
            CitySimulation city = NewCity();
            Road(city, new Vec2(-200, 0), new Vec2(200, 0));

            List<Lot> found = city.Lots.LotsInCircle(new Vec2(0, 12), 10);
            Assert.Greater(found.Count, 0);
            foreach (Lot lot in found)
            {
                Assert.LessOrEqual(Vec2.Distance(lot.Center, new Vec2(0, 12)), 10.0 + 1e-9);
            }
        }

        private static int CountZoned(CitySimulation city, ZoneType zone)
        {
            int n = 0;
            foreach (Lot lot in city.Lots.Lots)
            {
                if (lot.Zone == zone)
                {
                    n++;
                }
            }

            return n;
        }
    }
}
