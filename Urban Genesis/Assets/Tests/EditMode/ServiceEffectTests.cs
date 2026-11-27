using System;
using System.Collections.Generic;
using NUnit.Framework;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Tests
{
    /// <summary>What hospitals and schools do for the city, and the half refund for everything the player pays for.</summary>
    [TestFixture]
    public class ServiceEffectTests
    {
        private static CitySimulation Town()
        {
            var city = new CitySimulation(5);
            city.Treasury.Enable(1e7);
            city.SpawnChance = 1.0;
            city.Economy.DemandDrivesGrowth = false;
            city.PlaceRoad(city.PlanRoad(new Vec2(-300, 0), null, new Vec2(0, 0), RoadType.Street));
            city.PlaceRoad(city.PlanRoad(new Vec2(0, 0), null, new Vec2(300, 0), RoadType.Street));
            city.FillZone(new Vec2(-150, 12), ZoneType.Residential);
            city.FillZone(new Vec2(150, 12), ZoneType.Residential);
            city.PaintZone(new Vec2(200, -12), 50, ZoneType.Industrial);
            for (int i = 0; i < 300; i++) { city.Step(); }
            return city;
        }

        /// <summary>The same city with all milestones passed, so every service can be built.</summary>
        private static CitySimulation Unlocked(CitySimulation city)
        {
            CitySnapshot snapshot = city.Capture();
            snapshot.MilestonesReached = CityMilestones.Targets.Length;
            int skipped;
            CitySimulation again = CitySimulation.Restore(snapshot, out skipped);
            again.Treasury.Enable(1e7);
            return again;
        }

        private static RoadSegment Segment(CitySimulation city, bool west)
        {
            foreach (RoadSegment s in city.Roads.Segments)
            {
                if ((s.Curve.PointAtLength(s.Length * 0.5).X < 0) == west) { return s; }
            }

            return null;
        }

        [Test]
        public void ACivicBuildingGivesHalfItsPriceBackWhenDemolished()
        {
            CitySimulation city = Unlocked(Town());
            FacilityPlan plan = city.PlanFacility(BuildingShape.Hospital, new Vec2(-200, -12));
            Assert.IsTrue(plan.IsValid, plan.Error);
            Assert.IsNotNull(city.PlaceFacility(plan));
            double before = city.Treasury.Balance;
            city.Bulldoze(new Vec2(-200, -12), 5, true);
            Assert.IsFalse(city.HasFacility(BuildingShape.Hospital));
            Assert.AreEqual(before + CivicInfo.Cost(BuildingShape.Hospital) * 0.5, city.Treasury.Balance, 0.01);
        }

        [Test]
        public void ARoadGivesBackHalfOfItsPriceAndItsCable()
        {
            CitySimulation city = Unlocked(Town());
            RoadSegment east = Segment(city, false);
            Assert.IsNull(city.LayUtility(east, UtilityKind.Power));
            double expected = (city.Treasury.RoadCost(east.Length, east.Type) + city.Treasury.UtilityCost(east.Length, UtilityKind.Power)) * 0.5;
            Assert.AreEqual(expected, city.Treasury.RoadRefund(east), 0.01);

            double before = city.Treasury.Balance;
            Vec2 middle = east.Curve.PointAtLength(east.Length * 0.5);
            city.Bulldoze(middle, 5, true);
            Assert.AreEqual(before + expected, city.Treasury.Balance, 0.01);
        }

        [Test]
        public void APipeGivesBackHalfOfItsPrice()
        {
            CitySimulation city = Unlocked(Town());
            double start = city.Treasury.Balance;
            Assert.IsNull(city.LayPipe(new Vec2(0, 200), new Vec2(100, 200)));
            double paid = start - city.Treasury.Balance;
            Assert.AreEqual(1400.0, paid, 0.01);
            double before = city.Treasury.Balance;
            Assert.AreEqual(1, city.Bulldoze(new Vec2(50, 201), 5, true));
            Assert.AreEqual(before + paid * 0.5, city.Treasury.Balance, 0.01);
        }

        [Test]
        public void HospitalsAndSchoolsCoverTheCityInProportionToItsSize()
        {
            CitySimulation city = Unlocked(Town());
            Assert.AreEqual(0.0, city.ServiceCoverage(BuildingShape.Hospital), 1e-9);
            Assert.IsNotNull(city.PlaceFacility(city.PlanFacility(BuildingShape.Hospital, new Vec2(-200, -12))));
            Assert.AreEqual(Math.Min(1.0, CitySimulation.ResidentsPerHospital / city.Population), city.ServiceCoverage(BuildingShape.Hospital), 1e-9);
            Assert.AreEqual(0.0, city.ServiceCoverage(BuildingShape.School), 1e-9);
            StringAssert.Contains("Looks after", Inspector.DescribeBuilding(city, city.FacilitiesOf(BuildingShape.Hospital)[0]));
        }

        [Test]
        public void AHospitalMakesPeopleWantToMoveIn()
        {
            CitySimulation without = Unlocked(Town());
            CitySimulation with = Unlocked(Town());
            Assert.IsNotNull(with.PlaceFacility(with.PlanFacility(BuildingShape.Hospital, new Vec2(-200, -12))));
            for (int i = 0; i < 12; i++) { with.Economy.Update(); without.Economy.Update(); }

            Assert.IsTrue(without.Economy.ResidentialDemand > 0.02 && without.Economy.ResidentialDemand < 0.8, "a test that can show the difference: " + without.Economy.ResidentialDemand);
            Assert.IsTrue(with.Economy.ResidentialDemand > without.Economy.ResidentialDemand + 0.05,
                with.Economy.ResidentialDemand + " vs " + without.Economy.ResidentialDemand);
        }

        [Test]
        public void ASchoolMakesMoreResidentsAbleToWork()
        {
            CitySimulation without = Unlocked(Town());
            CitySimulation with = Unlocked(Town());
            Assert.IsNotNull(with.PlaceFacility(with.PlanFacility(BuildingShape.School, new Vec2(-200, -12))));
            for (int i = 0; i < 12; i++) { with.Economy.Update(); without.Economy.Update(); }

            Assert.IsTrue(with.Economy.Snapshot.Workers > without.Economy.Snapshot.Workers * 1.1,
                with.Economy.Snapshot.Workers + " vs " + without.Economy.Snapshot.Workers);
        }
    }
}
