using System;
using System.Collections.Generic;
using NUnit.Framework;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Tests
{
    [TestFixture]
    public class InfrastructureTests
    {
        private static CitySimulation Town(int seed = 5)
        {
            var city = new CitySimulation(seed);
            city.Treasury.Enable(1e7);
            city.SpawnChance = 1.0;
            city.Economy.DemandDrivesGrowth = false;
            city.PlaceRoad(city.PlanRoad(new Vec2(-300, 0), null, new Vec2(0, 0), RoadType.Street));
            city.PlaceRoad(city.PlanRoad(new Vec2(0, 0), null, new Vec2(300, 0), RoadType.Street));
            return city;
        }

        private static RoadSegment Pick(CitySimulation city, bool west)
        {
            RoadSegment best = null;
            foreach (RoadSegment s in city.Roads.Segments)
            {
                double x = s.Curve.PointAtLength(s.Length * 0.5).X;
                if (best == null || (west ? x < best.Curve.PointAtLength(best.Length * 0.5).X : x > best.Curve.PointAtLength(best.Length * 0.5).X)) { best = s; }
            }

            return best;
        }

        private static void GrowTo(CitySimulation city, int population)
        {
            city.FillZone(new Vec2(-150, 12), ZoneType.Residential);
            city.FillZone(new Vec2(-150, -12), ZoneType.Residential);
            city.FillZone(new Vec2(150, 12), ZoneType.Residential);
            city.FillZone(new Vec2(150, -12), ZoneType.Residential);
            for (int i = 0; i < 600 && city.Population < population; i++) { city.Step(); }
            Assert.GreaterOrEqual(city.Population, population);
        }

        [Test]
        public void MilestonesUnlockTheServicesInOrder()
        {
            CitySimulation city = Town();
            var messages = new List<string>();
            city.EventHappened += m => messages.Add(m);
            Assert.IsFalse(city.IsUnlocked(Feature.Hospital));
            Assert.IsFalse(city.IsUnlocked(BuildingShape.Hospital));
            Assert.IsTrue(city.IsUnlocked(BuildingShape.PowerPlant));

            GrowTo(city, 100);
            for (int i = 0; i < 8; i++) { city.Step(); }
            Assert.GreaterOrEqual(city.MilestonesReached, 1);
            Assert.IsTrue(city.IsUnlocked(Feature.Hospital));
            Assert.IsFalse(city.IsUnlocked(Feature.PoliceStation));
            Assert.IsTrue(messages.Exists(m => m.Contains("hospital")));
            Assert.AreEqual("unlocks the fire station and the dense residential zone", CityMilestones.Reward(2));
        }

        [Test]
        public void ARoadCannotCrossTheRiver()
        {
            var city = new CitySimulation(1);
            city.Treasury.Enable(1e7);
            var river = new River();
            for (int y = -1000; y <= 1000; y += 40) { river.Points.Add(new Vec2(200, y)); }
            city.SetRiver(river);

            Assert.IsFalse(city.PlanRoad(new Vec2(0, 0), null, new Vec2(400, 0)).IsValid);
            StringAssert.Contains("river", city.PlanRoad(new Vec2(0, 0), null, new Vec2(400, 0)).Error);
            Assert.IsTrue(city.PlanRoad(new Vec2(-300, 0), null, new Vec2(100, 0)).IsValid);
        }

        [Test]
        public void ARiverIsCreatedClearOfTheExistingRoads()
        {
            var city = Town();
            Assert.IsTrue(city.CreateRiver(new Random(3)));
            Assert.IsNotNull(city.River);
            foreach (RoadSegment s in city.Roads.Segments)
            {
                foreach (Vec2 p in s.Curve.Sample(20.0)) { Assert.Greater(city.River.DistanceTo(p), city.River.BlockRadius); }
            }
        }

        [Test]
        public void CivicBuildingsAreLockedUntilTheirMilestoneAndCostMoney()
        {
            CitySimulation city = Town();
            Vec2 spot = new Vec2(-150, 12);
            FacilityPlan locked = city.PlanFacility(BuildingShape.Hospital, spot);
            Assert.IsFalse(locked.IsValid);
            StringAssert.Contains("Locked", locked.Error);

            double before = city.Treasury.Balance;
            FacilityPlan plant = city.PlanFacility(BuildingShape.PowerPlant, spot);
            Assert.IsTrue(plant.IsValid, plant.Error);
            Building b = city.PlaceFacility(plant);
            Assert.IsNotNull(b);
            Assert.AreEqual(before - CivicInfo.Cost(BuildingShape.PowerPlant), city.Treasury.Balance, 0.01);
            Assert.IsTrue(city.HasFacility(BuildingShape.PowerPlant));
            Assert.IsFalse(city.PlanFacility(BuildingShape.PowerPlant, spot).IsValid, "the lots are taken now");

            // Painting a zone over it must not remove it.
            city.PaintZone(spot, 40, ZoneType.Commercial);
            Assert.IsTrue(city.HasFacility(BuildingShape.PowerPlant));
        }

        [Test]
        public void WaterTowersNeedTheRiver()
        {
            CitySimulation city = Town();
            FacilityPlan none = city.PlanFacility(BuildingShape.WaterTower, new Vec2(-150, 12));
            Assert.IsFalse(none.IsValid);
            StringAssert.Contains("no river", none.Error);
        }

        [Test]
        public void CablesCloseTheRoadForDaysAndCarryPowerFromThePlant()
        {
            CitySimulation city = Town();
            city.RequireUtilities = true;
            RoadSegment west = Pick(city, true);
            RoadSegment east = Pick(city, false);
            Vec2 onWest = west.Curve.PointAtLength(west.Length * 0.5) + new Vec2(0, 12);

            Assert.IsNotNull(city.PlaceFacility(city.PlanFacility(BuildingShape.PowerPlant, onWest)));
            Assert.IsFalse(city.SegmentHasPower(west.Id), "no cable yet");

            Assert.IsNull(city.LayUtility(west, UtilityKind.Power));
            Assert.IsTrue(west.IsClosed);
            Assert.IsTrue(city.HasRoadworks(west));
            Assert.IsTrue(city.SegmentHasPower(west.Id));
            Assert.IsFalse(city.SegmentHasPower(east.Id), "the east road has no cable");
            StringAssert.Contains("already", city.LayUtility(west, UtilityKind.Power));

            Assert.IsNull(city.LayUtility(east, UtilityKind.Power));
            Assert.IsTrue(city.SegmentHasPower(east.Id), "connected through the junction");

            for (int i = 0; i < 3 * GameTime.TicksPerDay + 2; i++) { city.Step(); }
            Assert.IsFalse(west.IsClosed);
            Assert.IsFalse(city.HasRoadworks(west));
        }

        [Test]
        public void WithoutServicesBuildingsStopGrowingAndEarningOnceTheFirstMilestoneIsPassed()
        {
            CitySimulation city = Town();
            GrowTo(city, 100);
            for (int i = 0; i < 8; i++) { city.Step(); }
            city.RequireUtilities = true;
            Assert.IsTrue(city.UtilitiesRequired);

            int buildings = city.BuildingCount;
            for (int i = 0; i < 100; i++) { city.Step(); }
            Assert.AreEqual(buildings, city.BuildingCount, "nothing new grows without services");

            city.Economy.Update();
            Assert.AreEqual(0.0, city.Economy.Snapshot.Residents, 0.001, "unserved homes do not count for the economy");
            Building any = null;
            foreach (Building b in city.Buildings) { any = b; break; }
            StringAssert.Contains("NOT SERVED", Inspector.DescribeBuilding(city, any));
        }

        [Test]
        public void SavesKeepTheRiverTheCablesAndTheCivicBuildings()
        {
            CitySimulation city = Town();
            Assert.IsTrue(city.CreateRiver(new Random(3)));
            RoadSegment west = Pick(city, true);
            Vec2 spot = west.Curve.PointAtLength(west.Length * 0.5) + new Vec2(0, 12);
            Assert.IsNotNull(city.PlaceFacility(city.PlanFacility(BuildingShape.PowerPlant, spot)));
            city.LayUtility(west, UtilityKind.Power);

            int skipped;
            CitySimulation again = CitySimulation.Restore(city.Capture(), out skipped);
            Assert.AreEqual(0, skipped);
            Assert.IsNotNull(again.River);
            Assert.AreEqual(city.River.Points.Count, again.River.Points.Count);
            Assert.IsTrue(again.HasFacility(BuildingShape.PowerPlant));
            Assert.IsTrue(again.Roads.GetSegment(west.Id).HasPower);
            Assert.IsTrue(again.HasRoadworks(again.Roads.GetSegment(west.Id)));
            Assert.IsTrue(again.SegmentHasPower(west.Id));
        }

        [Test]
        public void ABuildingWithoutAFireStationBurnsDown()
        {
            CitySimulation city = Town();
            GrowTo(city, 60);
            city.RandomEventsEnabled = true;
            city.FireChancePerDay = 1.0;
            city.EventChancePerDay = 0.0;
            var messages = new List<string>();
            city.EventHappened += m => messages.Add(m);
            Building fired = null;
            city.FireStarted += b => fired = b;
            int before = city.BuildingCount;

            for (int i = 0; i < 5 * GameTime.TicksPerDay + 1 && fired == null; i++) { city.Step(); }
            Assert.IsNotNull(fired);
            Assert.AreSame(fired, city.BurningBuilding);
            city.SpawnChance = 0.0;
            for (int i = 0; i < 80; i++) { city.Step(); }
            Assert.IsNull(city.BurningBuilding);
            Assert.IsTrue(messages.Exists(m => m.Contains("burned down")));
            Assert.IsNull(city.GetBuilding(fired.Id));
        }

        [Test]
        public void DenseResidentialGrowsTallHomes()
        {
            CitySimulation city = Town();
            city.FillZone(new Vec2(-150, 12), ZoneType.DenseResidential);
            for (int i = 0; i < 300; i++) { city.Step(); }
            int blocks = 0;
            foreach (Building b in city.Buildings)
            {
                Assert.AreEqual(ZoneType.DenseResidential, b.Zone);
                Assert.IsTrue(b.IsResidential);
                Assert.GreaterOrEqual(b.Floors, 4);
                blocks++;
            }

            Assert.Greater(blocks, 2);
            Assert.Greater(city.Population, 30);
        }
    }
}
