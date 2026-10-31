using System.Collections.Generic;
using NUnit.Framework;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Tests
{
    internal static class TestBuildings
    {
        /// <summary>A building that is not standing anywhere, for testing designs and templates on their own.</summary>
        public static Building Make(BuildingTemplate template, int floors, int variant)
        {
            return new Building(1, template, floors, variant, Vec2.Zero, new Vec2(0.0, 1.0), new List<Lot>());
        }
    }

    [TestFixture]
    public class TickClockTests
    {
        [Test]
        public void Advance_AtNormalSpeed_ReturnsOneTickPerTickSeconds()
        {
            var clock = new TickClock(0.5, 10);

            Assert.AreEqual(0, clock.Advance(0.3));
            Assert.AreEqual(1, clock.Advance(0.3), "0.6 seconds in total, so one tick is due");
            Assert.AreEqual(2, clock.Advance(1.0));
        }

        [Test]
        public void Advance_AtDoubleSpeed_ReturnsTwiceAsManyTicks()
        {
            var clock = new TickClock(0.5, 10);
            clock.SetSpeedLevel(2);

            Assert.AreEqual(4, clock.Advance(1.0));
        }

        [Test]
        public void Advance_WhilePaused_ReturnsZeroAndKeepsNoBacklog()
        {
            var clock = new TickClock(0.5, 10);
            clock.SetSpeedLevel(0);

            Assert.AreEqual(0, clock.Advance(100.0));

            clock.SetSpeedLevel(1);
            Assert.AreEqual(0, clock.Advance(0.1));
        }

        [Test]
        public void Advance_ALongFrame_IsCappedAndTheBacklogDropped()
        {
            var clock = new TickClock(0.5, 5);

            Assert.AreEqual(5, clock.Advance(60.0));
            Assert.AreEqual(0, clock.Advance(0.1));
        }

        [Test]
        public void TogglePause_ResumesAtThePreviousSpeed()
        {
            var clock = new TickClock(0.5, 10);
            clock.SetSpeedLevel(3);

            clock.TogglePause();
            Assert.IsTrue(clock.IsPaused);

            clock.TogglePause();
            Assert.AreEqual(3, clock.SpeedLevel);
        }

        [Test]
        public void SetSpeedLevel_IsClampedToTheValidRange()
        {
            var clock = new TickClock(0.5, 10);

            clock.SetSpeedLevel(99);
            Assert.AreEqual(TickClock.MaxSpeedLevel, clock.SpeedLevel);

            clock.SetSpeedLevel(-4);
            Assert.AreEqual(0, clock.SpeedLevel);
        }
    }

    [TestFixture]
    public class BuildingCatalogTests
    {
        private static readonly ZoneType[] Zones = { ZoneType.Residential, ZoneType.Commercial, ZoneType.Industrial };

        [Test]
        public void EveryZone_HasAtLeastOneSingleCellTemplate()
        {
            foreach (ZoneType zone in Zones)
            {
                bool found = false;
                foreach (BuildingTemplate t in BuildingCatalog.ForZone(zone))
                {
                    if (t.CellCount == 1)
                    {
                        found = true;
                    }
                }

                Assert.IsTrue(found, zone + " needs a 1x1 template to fill gaps");
            }
        }

        [Test]
        public void EveryTemplate_HasSensibleNumbersAndBelongsToItsZone()
        {
            foreach (ZoneType zone in Zones)
            {
                foreach (BuildingTemplate t in BuildingCatalog.ForZone(zone))
                {
                    Assert.AreEqual(zone, t.Zone, t.Shape + " is listed under the wrong zone");
                    Assert.GreaterOrEqual(t.Width, 1);
                    Assert.GreaterOrEqual(t.Depth, 1);
                    Assert.GreaterOrEqual(t.MinFloors, 1);
                    Assert.GreaterOrEqual(t.MaxFloors, t.MinFloors);
                    Assert.IsTrue(t.Weight > 0.0, t.Shape + " can never be picked");
                }
            }
        }

        [Test]
        public void EveryShape_HasAtLeastOneTemplate()
        {
            var used = new HashSet<BuildingShape>();
            foreach (BuildingTemplate t in BuildingCatalog.All())
            {
                used.Add(t.Shape);
            }

            foreach (BuildingShape shape in System.Enum.GetValues(typeof(BuildingShape)))
            {
                Assert.IsTrue(used.Contains(shape), shape + " is never used by any template");
            }
        }

        [Test]
        public void Pick_SingleCellOnly_NeverReturnsABiggerTemplate()
        {
            var rng = new System.Random(3);
            foreach (ZoneType zone in Zones)
            {
                for (int i = 0; i < 200; i++)
                {
                    Assert.AreEqual(1, BuildingCatalog.Pick(zone, rng, true).CellCount);
                }
            }
        }

        [Test]
        public void Pick_EventuallyReturnsEveryTemplateOfAZone()
        {
            var rng = new System.Random(4);
            foreach (ZoneType zone in Zones)
            {
                var seen = new HashSet<BuildingTemplate>();
                for (int i = 0; i < 3000; i++)
                {
                    seen.Add(BuildingCatalog.Pick(zone, rng, false));
                }

                Assert.AreEqual(BuildingCatalog.ForZone(zone).Count, seen.Count, "some " + zone + " templates are never picked");
            }
        }
    }

    [TestFixture]
    public class ResidentialTests
    {
        [Test]
        public void ResidentialBuildings_AreSmallHomes_WithAtMostFourApartmentsAndTwoFloors()
        {
            foreach (BuildingTemplate t in BuildingCatalog.ForZone(ZoneType.Residential))
            {
                Assert.IsTrue(t.Homes >= 1 && t.Homes <= 4, t.Shape + " homes " + t.Homes);
                Assert.IsTrue(t.MaxFloors <= 2, t.Shape + " is taller than two floors");
                Assert.IsTrue(t.CellCount <= 3, t.Shape + " is too big");
            }
        }

        [Test]
        public void Residents_AreTwoToFourPerHome_AndZeroOutsideResidential()
        {
            foreach (BuildingTemplate t in BuildingCatalog.All())
            {
                for (int variant = 0; variant < 12; variant++)
                {
                    Building b = TestBuildings.Make(t, t.MinFloors, variant);
                    if (t.Zone == ZoneType.Residential)
                    {
                        Assert.IsTrue(b.Residents >= 2 * t.Homes && b.Residents <= 4 * t.Homes, t.Shape + " residents " + b.Residents);
                    }
                    else
                    {
                        Assert.AreEqual(0, b.Residents);
                    }
                }
            }
        }
    }

    [TestFixture]
    public class BuildingDesignerTests
    {
        private const float Tolerance = 0.001f;

        private static string Signature(List<BuildingPart> parts)
        {
            var text = new List<string>();
            foreach (BuildingPart p in parts)
            {
                text.Add(string.Format("{0} {1:0.00},{2:0.00} {3:0.00}x{4:0.00} h{5:0.00} e{6:0.00} {7} {8}",
                    p.Kind, p.CenterX, p.CenterY, p.SizeX, p.SizeY, p.Height, p.Elevation, p.Tone, p.RidgeAlongX));
            }

            return string.Join("|", text.ToArray());
        }

        [Test]
        public void EveryTemplate_GetsPartsThatStayInsideItsFootprint_ForManyVariantsAndFloorCounts()
        {
            foreach (BuildingTemplate t in BuildingCatalog.All())
            {
                for (int floors = t.MinFloors; floors <= t.MaxFloors; floors++)
                {
                    for (int variant = 0; variant < 60; variant++)
                    {
                        var b = TestBuildings.Make(t, floors, variant);
                        List<BuildingPart> parts = BuildingDesigner.Design(b);
                        string what = t.Shape + " " + t.Width + "x" + t.Depth + " floors " + floors + " variant " + variant;

                        Assert.Greater(parts.Count, 0, what + " has no parts");
                        foreach (BuildingPart p in parts)
                        {
                            Assert.IsTrue(p.SizeX > 0f && p.SizeY > 0f, what + ": a part has no size");
                            Assert.IsTrue(p.Height > 0f, what + ": a part has no height");
                            Assert.IsTrue(p.Elevation >= 0f, what + ": a part is below the ground");
                            Assert.IsTrue(p.CenterX - p.SizeX * 0.5f >= -t.Width * 0.5f - Tolerance, what + ": a part sticks out on the left");
                            Assert.IsTrue(p.CenterX + p.SizeX * 0.5f <= t.Width * 0.5f + Tolerance, what + ": a part sticks out on the right");
                            Assert.IsTrue(p.CenterY - p.SizeY * 0.5f >= -t.Depth * 0.5f - Tolerance, what + ": a part sticks out at the front");
                            Assert.IsTrue(p.CenterY + p.SizeY * 0.5f <= t.Depth * 0.5f + Tolerance, what + ": a part sticks out at the back");
                        }
                    }
                }
            }
        }

        [Test]
        public void EveryBuilding_IsAtLeastAsTallAsItsFloorCount()
        {
            foreach (BuildingTemplate t in BuildingCatalog.All())
            {
                for (int floors = t.MinFloors; floors <= t.MaxFloors; floors++)
                {
                    for (int variant = 0; variant < 30; variant++)
                    {
                        var b = TestBuildings.Make(t, floors, variant);
                        float top = 0f;
                        foreach (BuildingPart p in BuildingDesigner.Design(b))
                        {
                            top = System.Math.Max(top, p.Elevation + p.Height);
                        }

                        Assert.IsTrue(top >= floors - Tolerance, t.Shape + " with " + floors + " floors only reaches " + top);
                    }
                }
            }
        }

        [Test]
        public void SameBuilding_AlwaysGetsTheSameDesign()
        {
            foreach (BuildingTemplate t in BuildingCatalog.All())
            {
                var b = TestBuildings.Make(t, t.MinFloors, 12345);

                Assert.AreEqual(Signature(BuildingDesigner.Design(b)), Signature(BuildingDesigner.Design(b)), t.Shape + " is not deterministic");
            }
        }

        [Test]
        public void EveryTemplate_HasMoreThanOneLook_AcrossVariants()
        {
            foreach (BuildingTemplate t in BuildingCatalog.All())
            {
                var looks = new HashSet<string>();
                for (int variant = 0; variant < 60; variant++)
                {
                    looks.Add(Signature(BuildingDesigner.Design(TestBuildings.Make(t, t.MinFloors, variant))));
                }

                Assert.Greater(looks.Count, 1, t.Shape + " " + t.Width + "x" + t.Depth + " always looks the same");
            }
        }

        [Test]
        public void DifferentShapes_LookDifferent()
        {
            var looks = new HashSet<string>();
            int shapes = 0;
            var seen = new HashSet<BuildingShape>();
            foreach (BuildingTemplate t in BuildingCatalog.All())
            {
                if (seen.Add(t.Shape))
                {
                    shapes++;
                    looks.Add(Signature(BuildingDesigner.Design(TestBuildings.Make(t, t.MinFloors, 0))));
                }
            }

            Assert.AreEqual(shapes, looks.Count);
        }
    }
}
