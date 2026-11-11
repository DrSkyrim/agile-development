using System;
using System.Collections.Generic;
using NUnit.Framework;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Tests
{
    [TestFixture]
    public class CityNamesTests
    {
        [Test]
        public void NamesAreUniqueAndShortEnoughForTheCityCard()
        {
            var seen = new HashSet<string>();
            foreach (string name in CityNames.All)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(name));
                Assert.IsTrue(name.Length <= 20, name);
                Assert.IsTrue(seen.Add(name), "duplicate: " + name);
            }

            Assert.IsTrue(CityNames.All.Count >= 100);
        }

        [Test]
        public void PickIsDeterministicForASeedAndVariesAcrossSeeds()
        {
            Assert.AreEqual(CityNames.Pick(new Random(7)), CityNames.Pick(new Random(7)));

            var picked = new HashSet<string>();
            for (int seed = 0; seed < 50; seed++)
            {
                picked.Add(CityNames.Pick(new Random(seed)));
            }

            Assert.IsTrue(picked.Count > 20);
        }
    }
}
