using NUnit.Framework;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Tests
{
    [TestFixture]
    public class InspectorTests
    {
        [Test]
        public void BuildingsAndRoadsAreDescribedInPlainWords()
        {
            var city = new CitySimulation(5);
            city.Treasury.Enable(1e7);
            city.SpawnChance = 1.0;
            city.Economy.DemandDrivesGrowth = false;
            Assert.IsNotNull(city.PlaceRoad(city.PlanRoad(new Vec2(-300, 0), null, new Vec2(300, 0), RoadType.Street)));
            city.FillZone(new Vec2(0, 12), ZoneType.Residential);
            city.FillZone(new Vec2(0, -12), ZoneType.Commercial);
            for (int i = 0; i < 200; i++) { city.Step(); }
            city.RefreshEconomyIfStale();

            double along;
            RoadSegment road = city.Roads.FindSegmentNear(new Vec2(0, 0), 5.0, out along);
            string roadText = Inspector.DescribeRoad(city, null, road);
            StringAssert.Contains("Single-lane road", roadText);
            StringAssert.Contains("Capacity", roadText);
            StringAssert.Contains("Upkeep", roadText);
            Assert.Greater(Inspector.Capacity(road), 10.0);

            Building home;
            Assert.IsTrue(city.TryGetBuildingAt(new Vec2(0, 12), out home) || city.BuildingCount > 0);
            int described = 0;
            for (int id = 0; id < 400; id++)
            {
                Building b = city.GetBuilding(id);
                if (b == null) { continue; }
                string text = Inspector.DescribeBuilding(city, b);
                StringAssert.Contains("#" + id, text);
                StringAssert.Contains("per week", text);
                described++;
            }

            Assert.Greater(described, 3);
        }
    }
}
