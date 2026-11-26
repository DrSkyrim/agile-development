using System;
using System.Collections.Generic;
using NUnit.Framework;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Tests
{
    /// <summary>Water pipes are drawn across the land; the tower and the outlet stand by the river without a road.</summary>
    [TestFixture]
    public class PipeTests
    {
        private static CitySimulation Town()
        {
            var city = new CitySimulation(5);
            city.Treasury.Enable(1e7);
            city.Economy.DemandDrivesGrowth = false;
            city.PlaceRoad(city.PlanRoad(new Vec2(-300, 0), null, new Vec2(0, 0), RoadType.Street));
            city.PlaceRoad(city.PlanRoad(new Vec2(0, 0), null, new Vec2(300, 0), RoadType.Street));
            var river = new River();
            for (int y = -1000; y <= 1000; y += 40) { river.Points.Add(new Vec2(420, y)); }
            city.SetRiver(river);
            return city;
        }

        private static RoadSegment East(CitySimulation city)
        {
            foreach (RoadSegment s in city.Roads.Segments)
            {
                if (s.Curve.PointAtLength(s.Length * 0.5).X > 0) { return s; }
            }

            return null;
        }

        private static RoadSegment West(CitySimulation city)
        {
            foreach (RoadSegment s in city.Roads.Segments)
            {
                if (s.Curve.PointAtLength(s.Length * 0.5).X < 0) { return s; }
            }

            return null;
        }

        private static void StandTowerAndOutlet(CitySimulation city)
        {
            FacilityPlan tower = city.PlanFacility(BuildingShape.WaterTower, new Vec2(360, 0));
            Assert.IsTrue(tower.IsValid, tower.Error);
            Assert.IsNotNull(city.PlaceFacility(tower));
            FacilityPlan outlet = city.PlanFacility(BuildingShape.SewageOutlet, new Vec2(360, 100));
            Assert.IsTrue(outlet.IsValid, outlet.Error);
            Assert.IsNotNull(city.PlaceFacility(outlet));
        }

        [Test]
        public void TheTowerAndTheOutletNeedNoRoadButMustBeNearTheRiverAndOutOfTheWater()
        {
            CitySimulation city = Town();
            Assert.IsTrue(city.PlanFacility(BuildingShape.WaterTower, new Vec2(360, 0)).IsValid);
            StringAssert.Contains("Too far", city.PlanFacility(BuildingShape.WaterTower, new Vec2(100, 400)).Error);
            StringAssert.Contains("water", city.PlanFacility(BuildingShape.WaterTower, new Vec2(420, 0)).Error);

            Building tower = city.PlaceFacility(city.PlanFacility(BuildingShape.WaterTower, new Vec2(360, 0)));
            Assert.AreEqual(0, tower.Lots.Count);
            Assert.IsFalse(city.PlanFacility(BuildingShape.WaterTower, new Vec2(360, 10)).IsValid, "it is in the way now");
        }

        [Test]
        public void APipeClosesOnlyTheRoadsItRunsUnder()
        {
            CitySimulation city = Town();
            PipePlan over = city.PlanPipe(new Vec2(100, -100), new Vec2(100, 100));
            Assert.IsTrue(over.IsValid, over.Error);
            Assert.AreEqual(1, over.Crossed.Count);

            PipePlan beside = city.PlanPipe(new Vec2(50, 100), new Vec2(150, 100));
            Assert.IsTrue(beside.IsValid, beside.Error);
            Assert.AreEqual(0, beside.Crossed.Count);

            Assert.IsNull(city.LayPipe(new Vec2(50, 100), new Vec2(150, 100)));
            Assert.IsFalse(East(city).IsClosed);
            Assert.IsFalse(West(city).IsClosed);

            Assert.IsNull(city.LayPipe(new Vec2(100, -100), new Vec2(100, 100)));
            Assert.IsTrue(East(city).IsClosed);
            Assert.IsTrue(city.HasRoadworks(East(city)));
            Assert.IsFalse(West(city).IsClosed);
        }

        [Test]
        public void PipesThatCrossAreJoinedAndFeedTheRoadsTheyRunUnder()
        {
            CitySimulation city = Town();
            StandTowerAndOutlet(city);
            RoadSegment east = East(city);

            // Tower -> south, a long run west that crosses it, then north under the road.
            Assert.IsNull(city.LayPipe(new Vec2(360, 0), new Vec2(360, -150)));
            Assert.IsNull(city.LayPipe(new Vec2(400, -100), new Vec2(100, -100)));
            Assert.IsNull(city.LayPipe(new Vec2(100, -100), new Vec2(100, 100)));
            Assert.IsTrue(city.SegmentHasWater(east.Id), "joined where the pipes cross");
            Assert.IsFalse(city.SegmentHasSewage(east.Id), "the outlet is not connected yet");
            Assert.IsFalse(city.SegmentHasWater(West(city).Id));

            Assert.IsNull(city.LayPipe(new Vec2(360, 100), new Vec2(360, -100)));
            Assert.IsTrue(city.SegmentHasSewage(east.Id));
            StringAssert.Contains("Connected", Inspector.DescribeBuilding(city, city.FacilitiesOf(BuildingShape.WaterTower)[0]));
        }

        [Test]
        public void ALoosePipeCarriesNothing()
        {
            CitySimulation city = Town();
            StandTowerAndOutlet(city);
            Assert.IsNull(city.LayPipe(new Vec2(100, -100), new Vec2(100, 100)));
            Assert.IsFalse(city.SegmentHasWater(East(city).Id));
            StringAssert.Contains("Not connected", Inspector.DescribeBuilding(city, city.FacilitiesOf(BuildingShape.WaterTower)[0]));
        }

        [Test]
        public void ALaterCableOnlyExtendsTheClosureOfAnEarlierPipe()
        {
            CitySimulation city = Town();
            RoadSegment east = East(city);
            Assert.IsNotNull(city.PlaceFacility(city.PlanFacility(BuildingShape.PowerPlant, east.Curve.PointAtLength(east.Length * 0.5) + new Vec2(0, 12))));

            Assert.IsNull(city.LayPipe(new Vec2(100, -100), new Vec2(100, 100)));
            for (int i = 0; i < 2 * GameTime.TicksPerDay; i++) { city.Step(); }
            Assert.IsTrue(east.IsClosed);

            UtilityPlan plan = city.PlanUtility(east, UtilityKind.Power);
            Assert.IsTrue(plan.IsValid, plan.Error);
            Assert.IsNull(city.LayUtility(east, UtilityKind.Power));

            // The pipe alone would have reopened the road one day from now; the cable keeps it shut for three days from now.
            for (int i = 0; i < 2 * GameTime.TicksPerDay + GameTime.TicksPerDay / 2; i++) { city.Step(); }
            Assert.IsTrue(east.IsClosed, "five days in total, as the cable needs three more days");
            for (int i = 0; i < GameTime.TicksPerDay; i++) { city.Step(); }
            Assert.IsFalse(east.IsClosed);
        }

        [Test]
        public void ARoadClosedForAnotherReasonBlocksThePipe()
        {
            CitySimulation city = Town();
            RoadSegment east = East(city);
            east.OpenFraction = 0f;
            StringAssert.Contains("closed", city.PlanPipe(new Vec2(100, -100), new Vec2(100, 100)).Error);
            Assert.IsTrue(city.PlanPipe(new Vec2(-100, -100), new Vec2(-100, 100)).IsValid);
        }

        [Test]
        public void PipesHaveLimitsAndAPrice()
        {
            CitySimulation city = Town();
            StringAssert.Contains("short", city.PlanPipe(new Vec2(100, 100), new Vec2(103, 100)).Error);
            StringAssert.Contains("long", city.PlanPipe(new Vec2(-500, 100), new Vec2(300, 100)).Error);
            StringAssert.Contains("edge", city.PlanPipe(new Vec2(100, 100), new Vec2(1e6, 100)).Error);

            double before = city.Treasury.Balance;
            PipePlan plan = city.PlanPipe(new Vec2(0, 100), new Vec2(100, 100));
            Assert.AreEqual(city.Treasury.UtilityCost(100, UtilityKind.Water), plan.Cost, 0.01);
            city.LayPipe(new Vec2(0, 100), new Vec2(100, 100));
            Assert.AreEqual(before - plan.Cost, city.Treasury.Balance, 0.01);
        }

        [Test]
        public void PipeEndsSnapOntoExistingPipes()
        {
            CitySimulation city = Town();
            city.LayPipe(new Vec2(0, 100), new Vec2(100, 100));
            Vec2 snapped = city.SnapPipePoint(new Vec2(104, 103));
            Assert.AreEqual(100.0, snapped.X, 1e-9);
            Assert.AreEqual(100.0, snapped.Y, 1e-9);
            Vec2 onBody = city.SnapPipePoint(new Vec2(50, 105));
            Assert.AreEqual(50.0, onBody.X, 1e-9);
            Assert.AreEqual(100.0, onBody.Y, 1e-9);
            Assert.AreEqual(new Vec2(50, 140), city.SnapPipePoint(new Vec2(50, 140)));
        }

        [Test]
        public void TheBulldozerDigsPipesUp()
        {
            CitySimulation city = Town();
            city.LayPipe(new Vec2(0, 100), new Vec2(100, 100));
            Assert.AreEqual(1, city.PipeCount);
            Assert.AreEqual(0, city.Bulldoze(new Vec2(50, 140), 5, true));
            Assert.AreEqual(1, city.Bulldoze(new Vec2(50, 101), 5, true));
            Assert.AreEqual(0, city.PipeCount);
        }

        [Test]
        public void SavesKeepThePipesAndTheFreeStandingBuildings()
        {
            CitySimulation city = Town();
            StandTowerAndOutlet(city);
            city.LayPipe(new Vec2(360, 0), new Vec2(360, -150));
            city.LayPipe(new Vec2(400, -100), new Vec2(100, -100));
            city.LayPipe(new Vec2(100, -100), new Vec2(100, 100));

            int skipped;
            CitySimulation again = CitySimulation.Restore(city.Capture(), out skipped);
            Assert.AreEqual(0, skipped);
            Assert.AreEqual(3, again.PipeCount);
            Assert.IsTrue(again.HasFacility(BuildingShape.WaterTower));
            Assert.IsTrue(again.HasFacility(BuildingShape.SewageOutlet));
            Assert.IsTrue(again.SegmentHasWater(East(again).Id));
            Assert.IsTrue(again.HasRoadworks(East(again)));
        }
    }
}
