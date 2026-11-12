using NUnit.Framework;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Tests
{
    [TestFixture]
    public class TreasuryTests
    {
        private static readonly Vec2 A = new Vec2(-200, 0);
        private static readonly Vec2 B = new Vec2(200, 0);

        [Test]
        public void MoneyIsOffByDefault_RoadsAreFree()
        {
            var city = new CitySimulation(1);
            RoadPlan plan = city.PlanRoad(A, null, B);
            Assert.IsTrue(plan.IsValid);
            Assert.AreEqual(0.0, plan.Cost, 0.0001);
            Assert.IsNotNull(city.PlaceRoad(plan));
            Assert.AreEqual(0.0, city.Treasury.Balance, 0.0001);
        }

        [Test]
        public void PlacingARoadCostsMoneyPerMetre()
        {
            var city = new CitySimulation(1);
            city.Treasury.Enable(10000.0);
            RoadPlan plan = city.PlanRoad(A, null, B);
            Assert.IsTrue(plan.IsValid, plan.Error);
            Assert.AreEqual(400.0 * city.Treasury.BuildCostPerMetre, plan.Cost, 0.5);

            Assert.IsNotNull(city.PlaceRoad(plan));
            Assert.AreEqual(10000.0 - plan.Cost, city.Treasury.Balance, 0.0001);
        }

        [Test]
        public void ARoadYouCannotAfford_IsRefusedWithAClearMessage_AndNothingChanges()
        {
            var city = new CitySimulation(1);
            city.Treasury.Enable(1000.0);
            RoadPlan plan = city.PlanRoad(A, null, B);
            Assert.IsFalse(plan.IsValid);
            StringAssert.Contains("Not enough money", plan.Error);
            Assert.IsNull(city.PlaceRoad(plan));
            Assert.AreEqual(0, city.Roads.SegmentCount);
            Assert.AreEqual(1000.0, city.Treasury.Balance, 0.0001);
        }

        [Test]
        public void ZonesAndBuildingsAreFree()
        {
            var city = new CitySimulation(1);
            city.Treasury.Enable(city.Treasury.BuildCostPerMetre * 400.0);
            city.PlaceRoad(city.PlanRoad(A, null, B));
            double after = city.Treasury.Balance;
            city.Economy.DemandDrivesGrowth = false;
            city.SpawnChance = 1.0;
            city.PaintZone(new Vec2(0, 0), 150, ZoneType.Residential);
            city.PaintZone(new Vec2(0, 0), 150, ZoneType.Commercial);
            city.FillZone(new Vec2(0, 12), ZoneType.Industrial);
            for (int i = 0; i < 100; i++) { city.Step(); }
            Assert.IsTrue(city.BuildingCount > 0);
            // Only upkeep (and no income yet) moves the money, never a building or a zone.
            Assert.AreEqual(after - city.Treasury.WeeklyUpkeep * 100.0 / GameTime.TicksPerWeek, city.Treasury.Balance, 0.01);
        }

        [Test]
        public void BulldozingARoad_RefundsHalf()
        {
            var city = new CitySimulation(1);
            city.Treasury.Enable(10000.0);
            RoadSegment road = city.PlaceRoad(city.PlanRoad(A, null, B));
            double afterBuild = city.Treasury.Balance;
            city.Bulldoze(new Vec2(0, 0), 10, true);
            Assert.AreEqual(0, city.Roads.SegmentCount);
            Assert.AreEqual(afterBuild + road.Length * city.Treasury.BuildCostPerMetre * city.Treasury.RefundShare, city.Treasury.Balance, 0.01);
        }

        [Test]
        public void RoadsCostUpkeep_EverySingleTick()
        {
            var city = new CitySimulation(1);
            city.Treasury.Enable(10000.0);
            city.PlaceRoad(city.PlanRoad(A, null, B));
            double start = city.Treasury.Balance;
            double upkeep = 400.0 * city.Treasury.UpkeepPerMetrePerWeek;
            Assert.AreEqual(upkeep, city.Treasury.WeeklyUpkeep, 0.5);
            for (int i = 0; i < GameTime.TicksPerWeek; i++) { city.Step(); }
            Assert.AreEqual(start - city.Treasury.WeeklyUpkeep, city.Treasury.Balance, 1.0, "one week of upkeep");
        }

        [Test]
        public void EmployedWorkersAndShoppersPayTaxes()
        {
            var city = new CitySimulation(3);
            city.Economy.DemandDrivesGrowth = false;
            city.SpawnChance = 1.0;
            for (int i = 0; i < 6; i++)
            {
                city.PlaceRoad(city.PlanRoad(new Vec2(-900 + i * 300, 0), null, new Vec2(-600 + i * 300, 0)));
            }

            city.PaintZone(new Vec2(-700, 0), 250, ZoneType.Residential);
            city.PaintZone(new Vec2(-300, 0), 60, ZoneType.Commercial);
            city.PaintZone(new Vec2(300, 0), 200, ZoneType.Industrial);
            for (int i = 0; i < 400; i++) { city.Step(); }
            city.Economy.Update();

            city.Treasury.Enable(0.0);
            Assert.IsTrue(city.Treasury.WeeklyIncome > 0.0);
            EconomySnapshot s = city.Economy.Snapshot;
            Assert.AreEqual(s.Employed * city.Treasury.TaxPerEmployedWorkerPerWeek + s.ShopServed * city.Treasury.TaxPerShopperServedPerWeek,
                city.Treasury.WeeklyIncome, 0.001);
        }

        [Test]
        public void ACityOfOnlyHomes_EarnsNothing()
        {
            var city = new CitySimulation(3);
            city.Economy.DemandDrivesGrowth = false;
            city.SpawnChance = 1.0;
            city.PlaceRoad(city.PlanRoad(A, null, B));
            city.PaintZone(new Vec2(0, 0), 150, ZoneType.Residential);
            city.Treasury.Enable(1000.0);
            for (int i = 0; i < 100; i++) { city.Step(); }
            Assert.AreEqual(0.0, city.Treasury.WeeklyIncome, 0.0001);
            Assert.IsTrue(city.Treasury.WeeklyNet < 0.0);
        }
    }
}
