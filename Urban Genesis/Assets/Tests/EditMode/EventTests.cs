using System.Collections.Generic;
using NUnit.Framework;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Tests
{
    [TestFixture]
    public class EventTests
    {
        private static CitySimulation Grid()
        {
            var city = new CitySimulation(7);
            city.Treasury.Enable(1e7);
            for (int i = -2; i <= 2; i++)
            {
                city.PlaceRoad(city.PlanRoad(new Vec2(-300, i * 100), null, new Vec2(300, i * 100), RoadType.Street));
            }

            return city;
        }

        private static int Closed(CitySimulation city)
        {
            int n = 0;
            foreach (RoadSegment s in city.Roads.Segments) { if (s.IsClosed) { n++; } }
            return n;
        }

        [Test]
        public void EventsAreOffUnlessSwitchedOn()
        {
            CitySimulation city = Grid();
            city.EventChancePerDay = 1.0;
            for (int i = 0; i < 6 * GameTime.TicksPerDay; i++) { city.Step(); }
            Assert.AreEqual(0, Closed(city));
        }

        [Test]
        public void ARoadClosesForADayAndOpensAgain()
        {
            CitySimulation city = Grid();
            city.RandomEventsEnabled = true;
            city.EventChancePerDay = 1.0;
            var messages = new List<string>();
            city.EventHappened += m => messages.Add(m);

            for (int i = 0; i < 3 * GameTime.TicksPerDay + 1; i++) { city.Step(); }
            Assert.AreEqual(1, Closed(city));
            Assert.AreEqual(1, messages.Count);
            StringAssert.Contains("closed for a day", messages[0]);

            for (int i = 0; i < GameTime.TicksPerDay; i++) { city.Step(); }
            Assert.AreEqual(2, messages.Count);
            StringAssert.Contains("open again", messages[1]);
        }

        [Test]
        public void GrantsAddToTheTreasuryAndTheDaysBooks()
        {
            var city = new CitySimulation(1);
            city.Treasury.Enable(1000);
            city.Treasury.Grant(500);
            Assert.AreEqual(1500, city.Treasury.Balance, 0.001);
            for (int i = 0; i < GameTime.TicksPerDay + 1; i++) { city.Step(); }
            Assert.AreEqual(500, city.Treasury.LastDayGranted, 0.001);
        }
    }
}
