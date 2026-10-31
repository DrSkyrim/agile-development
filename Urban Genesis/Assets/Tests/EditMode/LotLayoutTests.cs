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

        private static void AssertNoOverlaps(CitySimulation city)
        {
            var lots = new List<Lot>(city.Lots.Lots);
            for (int i = 0; i < lots.Count; i++)
            {
                for (int j = i + 1; j < lots.Count; j++)
                {
                    if (lots[i].Key.SegmentId != lots[j].Key.SegmentId && lots[i].Bounds.Overlaps(lots[j].Bounds))
                    {
                        Assert.IsFalse(Geometry.ConvexOverlap(lots[i].Corners, lots[j].Corners, LotLayout.LotOverlapTolerance + 0.1),
                            lots[i].Key + " and " + lots[j].Key + " overlap");
                    }
                }
            }
        }

        [Test]
        public void AtATJunction_LotsOfTheTwoRoadsDoNotOverlap()
        {
            CitySimulation city = NewCity();
            Road(city, new Vec2(-200, 0), new Vec2(200, 0));
            Road(city, new Vec2(0, -150), new Vec2(0, 3));
            Road(city, new Vec2(0, 150), new Vec2(0, 3));

            AssertNoOverlaps(city);
            Assert.Greater(city.Lots.HiddenCount, 0, "the corners should have given up some lots");
        }

        [Test]
        public void OnTheInsideOfABend_LotsOfTheTwoSegmentsDoNotOverlap()
        {
            CitySimulation city = NewCity();
            Road(city, new Vec2(-200, 0), new Vec2(0, 0));
            Road(city, new Vec2(0, 0), new Vec2(120, 120));

            AssertNoOverlaps(city);
        }

        [Test]
        public void TheRoadNearerTheLot_KeepsIt_SoRoadsNextToEachOtherEachKeepTheirFrontRow()
        {
            CitySimulation city = NewCity();
            RoadSegment main = Road(city, new Vec2(-200, 0), new Vec2(200, 0));
            RoadSegment stub = Road(city, new Vec2(100, -150), new Vec2(100, 3));

            foreach (Lot lot in city.Lots.Lots)
            {
                if (lot.Key.SegmentId == stub.Id && lot.Key.Row == 0 && lot.Center.Y < -20)
                {
                    Assert.IsNotNull(lot, "stub road-side lots away from the main road must exist");
                }
            }

            // Right next to the stub, its own first row beats the main road's deeper rows.
            Lot beside = city.Lots.LotAt(new Vec2(104 + 8, -30));
            Assert.IsNotNull(beside);
            Assert.AreEqual(0, beside.Key.Row);
            Assert.AreNotEqual(main.Id, beside.Key.SegmentId);
        }

        [Test]
        public void RemovingARoad_BringsBackTheLotsItWasHiding_WithTheirZoning()
        {
            CitySimulation city = NewCity();
            Road(city, new Vec2(-200, 0), new Vec2(200, 0));
            int alone = city.Lots.Count;
            city.PaintZone(new Vec2(0, 0), 200, ZoneType.Residential);

            Road(city, new Vec2(0, -150), new Vec2(0, 3));
            var hiddenKeys = new List<LotKey>();
            foreach (Lot lot in city.Lots.HiddenLots)
            {
                if (lot.Zone != ZoneType.None)
                {
                    Assert.AreEqual(ZoneType.Residential, lot.Zone);
                    hiddenKeys.Add(lot.Key);
                }
            }

            Assert.Greater(hiddenKeys.Count, 0, "the main road's lots that lost the corner keep their zoning while hidden");

            city.Bulldoze(new Vec2(0, -100), 3, true);

            Assert.AreEqual(0, city.Lots.HiddenCount);
            Assert.AreEqual(alone, city.Lots.Count, "every lot is back");
            foreach (LotKey key in hiddenKeys)
            {
                Lot back = city.Lots.Get(key);
                Assert.IsNotNull(back, key + " did not come back");
                Assert.AreEqual(ZoneType.Residential, back.Zone, key + " lost its zoning");
            }
        }

        [Test]
        public void LotsWithBuildings_AreNeverTheOnesHidden()
        {
            CitySimulation city = NewCity();
            Road(city, new Vec2(-200, 0), new Vec2(200, 0));
            city.SpawnChance = 1.0;
            for (double x = -190; x <= 190; x += 16)
            {
                city.PaintZone(new Vec2(x, 0), 48, ZoneType.Residential);
            }

            for (int i = 0; i < 300; i++)
            {
                city.Step();
            }

            int buildings = city.BuildingCount;
            Assert.Greater(buildings, 10);

            // A road built through the middle (demolishing what is in its way) must not take lots out from under other buildings.
            RoadPlan plan = city.PlanRoad(new Vec2(100, -150), null, new Vec2(100, 3));
            Assert.IsTrue(plan.IsValid, plan.Error);
            int doomed = plan.Demolitions.Count;
            Assert.IsNotNull(city.PlaceRoad(plan, true));

            Assert.AreEqual(buildings - doomed, city.BuildingCount);
            foreach (Building b in city.Buildings)
            {
                foreach (Lot lot in b.Lots)
                {
                    Assert.AreSame(lot, city.Lots.Get(lot.Key), "a building's lot was hidden");
                }
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
