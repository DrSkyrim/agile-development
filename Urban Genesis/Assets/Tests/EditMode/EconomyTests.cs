using System;
using System.Collections.Generic;
using NUnit.Framework;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Tests
{
    [TestFixture]
    public class RoadGraphTests
    {
        private static RoadSegment Road(CitySimulation city, Vec2 a, Vec2 b)
        {
            RoadSegment segment = city.PlaceRoad(city.PlanRoad(a, null, b));
            Assert.IsNotNull(segment);
            return segment;
        }

        [Test]
        public void DistanceBetweenNeighbouringSegments_IsHalfPlusHalf()
        {
            var city = new CitySimulation(1);
            RoadSegment a = Road(city, new Vec2(-300, 0), new Vec2(0, 0));
            RoadSegment b = Road(city, new Vec2(0, 0), new Vec2(300, 0));
            var graph = new RoadGraph(city.Roads);
            Assert.AreEqual(300.0, graph.SegmentDistance(a, b), 1.0);
            Assert.AreEqual(a.Length / 3.0, graph.SegmentDistance(a, a), 0.01);
        }

        [Test]
        public void SeparateRoads_AreUnreachable()
        {
            var city = new CitySimulation(1);
            RoadSegment a = Road(city, new Vec2(-300, 0), new Vec2(0, 0));
            RoadSegment b = Road(city, new Vec2(-300, 300), new Vec2(0, 300));
            var graph = new RoadGraph(city.Roads);
            Assert.IsTrue(double.IsInfinity(graph.SegmentDistance(a, b)));
        }

        [Test]
        public void ClosedRoad_CutsTheRouteUntilReopened_AfterInvalidate()
        {
            var city = new CitySimulation(1);
            RoadSegment a = Road(city, new Vec2(-600, 0), new Vec2(-300, 0));
            RoadSegment mid = Road(city, new Vec2(-300, 0), new Vec2(0, 0));
            RoadSegment c = Road(city, new Vec2(0, 0), new Vec2(300, 0));
            var graph = new RoadGraph(city.Roads);
            Assert.IsFalse(double.IsInfinity(graph.SegmentDistance(a, c)));

            mid.OpenFraction = 0f;
            graph.Invalidate();
            Assert.IsTrue(double.IsInfinity(graph.SegmentDistance(a, c)));
            Assert.IsTrue(double.IsInfinity(graph.SegmentDistance(mid, c)), "a closed road also cuts off its own buildings");

            mid.OpenFraction = 0.5f;
            graph.Invalidate();
            double slow = graph.SegmentDistance(a, c);
            mid.OpenFraction = 1f;
            graph.Invalidate();
            Assert.IsTrue(slow > graph.SegmentDistance(a, c), "half-open roads cost more to drive");
        }
    }

    [TestFixture]
    public class EconomyTests
    {
        // A west-east chain of 300 m roads, so there are several segments and junctions to travel between.
        private static List<RoadSegment> Chain(CitySimulation city, double fromX, int count, double y)
        {
            var list = new List<RoadSegment>();
            for (int i = 0; i < count; i++)
            {
                RoadSegment s = city.PlaceRoad(city.PlanRoad(new Vec2(fromX + i * 300, y), null, new Vec2(fromX + (i + 1) * 300, y)));
                Assert.IsNotNull(s);
                list.Add(s);
            }

            return list;
        }

        private static void Run(CitySimulation city, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                city.Step();
            }
        }

        private static int Count(CitySimulation city, ZoneType zone)
        {
            return city.CountBuildings(zone);
        }

        private static double Total(CitySimulation city, Func<BuildingEconomy, double> pick)
        {
            double sum = 0;
            foreach (Building b in city.Buildings)
            {
                BuildingEconomy e = city.Economy.StatusOf(b);
                if (e != null) { sum += pick(e); }
            }

            return sum;
        }

        // Homes in the west, industry in the east, both on one road chain.
        private static CitySimulation HomesAndFactories(out List<RoadSegment> chain)
        {
            var city = new CitySimulation(3);
            city.Economy.DemandDrivesGrowth = false;
            city.SpawnChance = 1.0;
            chain = Chain(city, -900, 6, 0);
            city.PaintZone(new Vec2(-700, 0), 200, ZoneType.Residential);
            city.PaintZone(new Vec2(500, 0), 200, ZoneType.Industrial);
            Run(city, 400);
            city.Economy.Update();
            return city;
        }

        [Test]
        public void HomesOnly_HaveNoJobs_SoIndustryIsWantedAndMoreHomesAreNot()
        {
            var city = new CitySimulation(3);
            city.SpawnChance = 1.0;
            Chain(city, -900, 6, 0);
            city.PaintZone(new Vec2(0, 0), 900, ZoneType.Residential);
            Run(city, 400);

            EconomySnapshot s = city.Economy.Snapshot;
            Assert.IsTrue(s.Residents > 0, "a few homes grow before the lack of jobs stops it");
            Assert.AreEqual(1.0, s.UnemploymentRate, 0.001);
            Assert.IsTrue(city.Economy.IndustrialDemand > CityEconomyDefaults.IndustrialBase + 0.3, "industry is wanted: " + city.Economy.IndustrialDemand);
            Assert.IsTrue(city.Economy.ResidentialDemand < 0.05, "nobody moves in without jobs: " + city.Economy.ResidentialDemand);
            StringAssert.Contains("jobs", city.Economy.Advice);
        }

        [Test]
        public void ResidentialGrowthSlowsWhenThereAreNoJobs()
        {
            var gated = new CitySimulation(5);
            gated.SpawnChance = 1.0;
            var free = new CitySimulation(5);
            free.SpawnChance = 1.0;
            free.Economy.DemandDrivesGrowth = false;
            foreach (CitySimulation c in new[] { gated, free })
            {
                Chain(c, -900, 6, 0);
                c.PaintZone(new Vec2(0, 0), 900, ZoneType.Residential);
                Run(c, 300);
            }

            Assert.IsTrue(Count(gated, ZoneType.Residential) < Count(free, ZoneType.Residential) * 0.7,
                gated.BuildingCount + " gated vs " + free.BuildingCount + " ungated");
        }

        [Test]
        public void ConnectedFactories_EmployTheResidents()
        {
            List<RoadSegment> chain;
            CitySimulation city = HomesAndFactories(out chain);
            Assert.IsTrue(Count(city, ZoneType.Residential) > 10 && Count(city, ZoneType.Industrial) > 3);
            Assert.IsTrue(city.Economy.Snapshot.Employed > 0.5 * city.Economy.Snapshot.Workers, "employed " + city.Economy.Snapshot.Employed);
            Assert.IsTrue(city.Economy.Flows.Count > 0);
            foreach (TripFlow f in city.Economy.Flows)
            {
                if (f.Kind == FlowKind.Work)
                {
                    Assert.IsTrue(f.Volume > 0 && f.Distance > 0 && f.Distance <= city.Economy.Parameters.MaxWorkDistance);
                }
            }
        }

        [Test]
        public void RoadsThatDoNotConnect_CarryNoTrips()
        {
            var city = new CitySimulation(3);
            city.Economy.DemandDrivesGrowth = false;
            city.SpawnChance = 1.0;
            Chain(city, -600, 2, 0);
            Chain(city, -600, 2, 400);
            city.PaintZone(new Vec2(-300, 0), 200, ZoneType.Residential);
            city.PaintZone(new Vec2(-300, 400), 200, ZoneType.Industrial);
            Run(city, 300);
            city.Economy.Update();

            Assert.IsTrue(Count(city, ZoneType.Residential) > 3 && Count(city, ZoneType.Industrial) > 3);
            Assert.AreEqual(0.0, city.Economy.Snapshot.Employed, 0.0001);
            Assert.AreEqual(0, city.Economy.Flows.Count);
        }

        [Test]
        public void ClosingTheRoadBetween_StopsTheCommute_AndReopeningRestoresIt()
        {
            List<RoadSegment> chain;
            CitySimulation city = HomesAndFactories(out chain);
            double before = city.Economy.Snapshot.Employed;
            Assert.IsTrue(before > 0);

            RoadSegment between = chain[3];
            between.OpenFraction = 0f;
            city.Economy.Update();
            Assert.AreEqual(0.0, city.Economy.Snapshot.Employed, 0.0001, "no way through");

            between.OpenFraction = 1f;
            city.Economy.Update();
            Assert.AreEqual(before, city.Economy.Snapshot.Employed, 0.0001);
        }

        [Test]
        public void ShopsNeedGoodsFromAFactory_ToServeAnyone()
        {
            var city = new CitySimulation(4);
            city.Economy.DemandDrivesGrowth = false;
            city.SpawnChance = 1.0;
            Chain(city, -900, 6, 0);
            city.PaintZone(new Vec2(-700, 0), 300, ZoneType.Residential);
            city.PaintZone(new Vec2(-300, 0), 60, ZoneType.Commercial);
            Run(city, 300);
            city.Economy.Update();

            Assert.IsTrue(Count(city, ZoneType.Commercial) > 0);
            Assert.AreEqual(0.0, city.Economy.Snapshot.GoodsDelivered, 0.0001);
            Assert.AreEqual(0.0, Total(city, e => e.EffectiveService), 0.0001, "shops without goods serve nobody");
            Assert.AreEqual(0.0, city.Economy.Snapshot.ShopServed, 0.0001);

            city.PaintZone(new Vec2(300, 0), 200, ZoneType.Industrial);
            Run(city, 300);
            city.Economy.Update();
            Assert.IsTrue(city.Economy.Snapshot.GoodsDelivered > 0, "the factory supplies the shops");
            Assert.IsTrue(city.Economy.Snapshot.ShopServed > 0, "so residents can shop");
        }

        [Test]
        public void Matches_NeverExceedSupplyOrDemand_AndDemandStaysInRange()
        {
            for (int seed = 1; seed <= 3; seed++)
            {
                var city = new CitySimulation(seed);
                city.SpawnChance = 1.0;
                Chain(city, -900, 6, 0);
                city.PaintZone(new Vec2(-600, 0), 300, ZoneType.Residential);
                city.PaintZone(new Vec2(-100, 0), 150, ZoneType.Commercial);
                city.PaintZone(new Vec2(300, 0), 250, ZoneType.Industrial);

                for (int round = 0; round < 40; round++)
                {
                    Run(city, 25);
                    CityEconomy eco = city.Economy;
                    EconomySnapshot s = eco.Snapshot;
                    Assert.IsTrue(s.Employed <= s.Workers + 1e-6 && s.Employed <= s.Jobs + 1e-6);
                    Assert.IsTrue(s.GoodsDelivered <= s.GoodsDemand + 1e-6 && s.GoodsDelivered <= s.GoodsSupply + 1e-6);
                    Assert.IsTrue(s.ShopServed <= s.ShopDemand + 1e-6 && s.ShopServed <= s.ServiceCapacity + 1e-6);

                    double[] demand = { eco.ResidentialDemand, eco.CommercialDemand, eco.IndustrialDemand };
                    foreach (double d in demand)
                    {
                        Assert.IsTrue(d >= 0.0 && d <= 1.0 && !double.IsNaN(d), "demand " + d);
                    }

                    double work = 0, goods = 0, shopping = 0;
                    foreach (TripFlow f in eco.Flows)
                    {
                        Assert.IsNotNull(city.Roads.GetSegment(f.FromSegmentId));
                        Assert.IsNotNull(city.Roads.GetSegment(f.ToSegmentId));
                        if (f.Kind == FlowKind.Work) { work += f.Volume; }
                        if (f.Kind == FlowKind.Goods) { goods += f.Volume; }
                        if (f.Kind == FlowKind.Shopping) { shopping += f.Volume; }
                    }

                    Assert.AreEqual(s.Employed, work, 1e-6);
                    Assert.AreEqual(s.GoodsDelivered, goods, 1e-6);
                    Assert.AreEqual(s.ShopServed, shopping, 1e-6);
                    CityChecks.AssertAllRulesHold(city);
                }
            }
        }

        [Test]
        public void ABuildingKnowsItsOwnStatus()
        {
            List<RoadSegment> chain;
            CitySimulation city = HomesAndFactories(out chain);
            foreach (Building b in city.Buildings)
            {
                BuildingEconomy e = city.Economy.StatusOf(b);
                Assert.IsNotNull(e);
                if (b.Zone == ZoneType.Residential)
                {
                    Assert.AreEqual(b.Residents, e.Residents, 0.0001);
                    Assert.IsTrue(e.EmploymentFill >= 0 && e.EmploymentFill <= 1);
                }
                else
                {
                    Assert.IsTrue(e.Jobs > 0);
                    Assert.IsTrue(e.Staffing >= 0 && e.Staffing <= 1);
                }
            }
        }
    }

    // The base values live in EconomyParameters; the tests only need to know the starting point.
    internal static class CityEconomyDefaults
    {
        public static readonly double IndustrialBase = new EconomyParameters().BaseIndustrialDemand;
    }
}
