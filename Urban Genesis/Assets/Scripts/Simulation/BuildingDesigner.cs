using System;
using System.Collections.Generic;

namespace UrbanGenesis.Simulation
{
    /// <summary>
    /// Turns a <see cref="Building"/> into a handful of solids (boxes, cylinders, roofs) that make it look like
    /// the right kind of building. The same building always gets the same design: the small differences between
    /// two buildings of one kind come from <see cref="Building.Variant"/>.
    ///
    /// Designs are drawn on a flat plan of the footprint. The plan's corner (0, 0) is the front-left corner and
    /// (W, D) the back-right corner, where W and D are the template's width and depth in cells. Everything stays
    /// inside that plan, which tests check for every template.
    /// </summary>
    public static class BuildingDesigner
    {
        public static List<BuildingPart> Design(Building building)
        {
            var sketch = new Sketch(building.Template.Width, building.Template.Depth, building.Floors, new Random(building.Variant));

            switch (building.Shape)
            {
                case BuildingShape.Cottage: Cottage(sketch); break;
                case BuildingShape.RowHouses: RowHouses(sketch); break;
                case BuildingShape.ApartmentBlock: ApartmentBlock(sketch); break;
                case BuildingShape.LBlock: LBlock(sketch); break;
                case BuildingShape.CourtyardBlock: CourtyardBlock(sketch); break;
                case BuildingShape.TerraceTower: TerraceTower(sketch); break;

                case BuildingShape.Shop: Shop(sketch); break;
                case BuildingShape.OfficeSlab: OfficeSlab(sketch); break;
                case BuildingShape.Tower: Tower(sketch); break;
                case BuildingShape.PodiumTower: PodiumTower(sketch); break;
                case BuildingShape.Mall: Mall(sketch); break;

                case BuildingShape.Shed: Shed(sketch); break;
                case BuildingShape.Warehouse: Warehouse(sketch); break;
                case BuildingShape.Factory: Factory(sketch); break;
                case BuildingShape.SiloYard: SiloYard(sketch); break;

                default:
                    throw new ArgumentOutOfRangeException("building", "There is no design for the shape " + building.Shape + ".");
            }

            return sketch.Parts;
        }

        // ------------------------------------------------------------ residential

        private static void Cottage(Sketch s)
        {
            float f = s.Floors;
            s.Box(0.18f, 0.18f, 0.82f, 0.82f, f, 0f, PartTone.Wall);
            s.Gable(0.12f, 0.12f, 0.88f, 0.88f, 0.7f, f, s.Rng.Next(2) == 0, PartTone.Light);
            s.Box(0.60f, 0.30f, 0.72f, 0.42f, 0.6f, f + 0.25f, PartTone.Dark); // chimney
        }

        private static void RowHouses(Sketch s)
        {
            // One narrow house per cell of width, alternating in height.
            for (int i = 0; i < s.W; i++)
            {
                float h = i % 2 == 0 ? s.Floors : Math.Max(1f, s.Floors - 1f);
                s.Box(i + 0.08f, 0.12f, i + 0.92f, 0.88f, h, 0f, PartTone.Wall);
                s.Gable(i + 0.04f, 0.08f, i + 0.96f, 0.92f, 0.6f, h, false, PartTone.Light);
            }
        }

        private static void ApartmentBlock(Sketch s)
        {
            float f = s.Floors;
            const float plinth = 0.6f;

            s.Box(0.05f, 0.05f, s.W - 0.05f, s.D - 0.05f, plinth, 0f, PartTone.Dark);

            bool stepped = s.Rng.Next(2) == 0;
            float roofX = s.W * 0.5f;
            float roofY = s.D * 0.5f;

            if (stepped && s.D >= 2)
            {
                // The front half is lower than the back half.
                s.Box(0.14f, 0.14f, s.W - 0.14f, s.D * 0.5f, Math.Max(2f, f - 2f), plinth, PartTone.Wall);
                s.Box(0.14f, s.D * 0.5f, s.W - 0.14f, s.D - 0.14f, f, plinth, PartTone.Wall);
                roofY = s.D * 0.75f;
            }
            else if (stepped)
            {
                // Too shallow for that, so one end steps down instead.
                s.Box(0.14f, 0.14f, s.W * 0.5f, s.D - 0.14f, f, plinth, PartTone.Wall);
                s.Box(s.W * 0.5f, 0.14f, s.W - 0.14f, s.D - 0.14f, Math.Max(2f, f - 2f), plinth, PartTone.Wall);
                roofX = s.W * 0.25f;
            }
            else
            {
                s.Box(0.14f, 0.14f, s.W - 0.14f, s.D - 0.14f, f, plinth, PartTone.Wall);
            }

            s.Box(roofX - 0.3f, roofY - 0.18f, roofX + 0.3f, roofY + 0.18f, 0.5f, plinth + f, PartTone.Light);
        }

        private static void LBlock(Sketch s)
        {
            // A 2x2 footprint with one quarter left empty: two wings that meet at a corner.
            float f = s.Floors;
            int missingX = s.Rng.Next(2);
            int missingY = s.Rng.Next(2);

            int fullRow = 1 - missingY;
            int armColumn = 1 - missingX;

            s.Box(0.1f, fullRow + 0.1f, 1.9f, fullRow + 0.9f, f, 0f, PartTone.Wall);

            float armFrom = missingY == 0 ? 0.1f : 0.9f;
            float armTo = missingY == 0 ? 1.1f : 1.9f;
            s.Box(armColumn + 0.1f, armFrom, armColumn + 0.9f, armTo, Math.Max(2f, f - 1f), 0f, PartTone.Wall);

            // A small tower where the two wings meet.
            s.Box(armColumn + 0.25f, fullRow + 0.25f, armColumn + 0.75f, fullRow + 0.75f, 1f, f, PartTone.Light);
        }

        private static void CourtyardBlock(Sketch s)
        {
            // A U of three wings around a courtyard that opens towards the front.
            float f = s.Floors;
            float armHeight = s.Rng.Next(2) == 0 ? f : Math.Max(2f, f - 1f);
            float backFrom = s.D - 0.9f;

            s.Box(0.1f, backFrom, s.W - 0.1f, s.D - 0.1f, f, 0f, PartTone.Wall);
            s.Box(0.1f, 0.1f, 0.9f, backFrom + 0.1f, armHeight, 0f, PartTone.Wall);
            s.Box(s.W - 0.9f, 0.1f, s.W - 0.1f, backFrom + 0.1f, armHeight, 0f, PartTone.Wall);

            s.Box(s.W * 0.5f - 0.3f, 0.3f, s.W * 0.5f + 0.3f, 0.7f, 0.5f, 0f, PartTone.Light);          // pavilion in the courtyard
            s.Box(s.W * 0.5f - 0.35f, s.D - 0.7f, s.W * 0.5f + 0.35f, s.D - 0.3f, 0.6f, f, PartTone.Light); // roof unit
        }

        private static void TerraceTower(Sketch s)
        {
            // Three tiers, each set back from the one below, plus an aerial.
            float f = s.Floors;
            float lower = f * 0.4f;
            float middle = f * 0.35f;
            float upper = f - lower - middle;

            s.Box(0.10f, 0.10f, 1.90f, 1.90f, lower, 0f, PartTone.Wall);

            if (s.Rng.Next(2) == 0)
            {
                // Tiers set back evenly all round.
                s.Box(0.35f, 0.35f, 1.65f, 1.65f, middle, lower, PartTone.Wall);
                s.Box(0.60f, 0.60f, 1.40f, 1.40f, upper, lower + middle, PartTone.Light);
                s.Cylinder(1.0f, 1.0f, 0.1f, 1.2f, f, PartTone.Dark);
            }
            else
            {
                // Tiers climbing towards the back corner, like a staircase.
                s.Box(0.55f, 0.55f, 1.90f, 1.90f, middle, lower, PartTone.Wall);
                s.Box(1.05f, 1.05f, 1.90f, 1.90f, upper, lower + middle, PartTone.Light);
                s.Cylinder(1.5f, 1.5f, 0.1f, 1.2f, f, PartTone.Dark);
            }
        }

        // ------------------------------------------------------------ commercial

        private static void Shop(Sketch s)
        {
            float f = s.Floors;
            s.Box(0.10f, 0.12f, 0.90f, 0.88f, f, 0f, PartTone.Wall);
            s.Box(0.10f, 0.03f, 0.90f, 0.20f, 0.15f, 0.6f, PartTone.Dark);  // awning over the door

            if (s.Rng.Next(2) == 0)
            {
                s.Box(0.30f, 0.42f, 0.70f, 0.52f, 0.5f, f, PartTone.Light);  // roof sign
            }
            else
            {
                s.Cylinder(0.70f, 0.65f, 0.28f, 0.5f, f, PartTone.Light);    // rooftop tank
            }
        }

        private static void OfficeSlab(Sketch s)
        {
            float f = s.Floors;
            s.Box(0.04f, 0.06f, s.W - 0.04f, s.D - 0.06f, 1f, 0f, PartTone.Dark); // lobby

            float roofX;
            if (s.Rng.Next(2) == 0)
            {
                s.Box(0.14f, 0.16f, s.W - 0.14f, s.D - 0.16f, f - 1f, 1f, PartTone.Wall);
                roofX = s.W * 0.5f;
            }
            else
            {
                // One end of the slab steps down.
                s.Box(0.14f, 0.16f, s.W * 0.5f, s.D - 0.16f, f - 1f, 1f, PartTone.Wall);
                s.Box(s.W * 0.5f, 0.16f, s.W - 0.14f, s.D - 0.16f, Math.Max(1f, f - 3f), 1f, PartTone.Wall);
                roofX = s.W * 0.25f;
            }

            s.Box(roofX - 0.3f, s.D * 0.5f - 0.2f, roofX + 0.3f, s.D * 0.5f + 0.2f, 0.5f, f, PartTone.Light);
        }

        private static void Tower(Sketch s)
        {
            float f = s.Floors;
            float shaft = f * (s.Rng.Next(2) == 0 ? 0.65f : 0.5f);

            s.Box(0.10f, 0.10f, 0.90f, 0.90f, shaft, 0f, PartTone.Wall);
            s.Box(0.20f, 0.20f, 0.80f, 0.80f, f - shaft, shaft, PartTone.Light);

            if (s.Rng.Next(2) == 0)
            {
                s.Pyramid(0.30f, 0.30f, 0.70f, 0.70f, 1.4f, f, PartTone.Dark);  // spire
            }
            else
            {
                s.Cylinder(0.50f, 0.50f, 0.12f, 2.0f, f, PartTone.Dark);        // radio mast
            }
        }

        private static void PodiumTower(Sketch s)
        {
            float f = s.Floors;

            s.Box(0.04f, 0.04f, 1.96f, 1.96f, 1f, 0f, PartTone.Dark);                // podium
            s.Box(0.12f, 0.12f, 1.88f, 0.55f, 2f, 1f, PartTone.Light);                // low wing along the front

            if (s.Rng.Next(2) == 0)
            {
                s.Box(0.45f, 0.70f, 1.55f, 1.70f, f - 1f, 1f, PartTone.Wall);          // tower in the middle
                s.Box(0.55f, 0.80f, 1.45f, 1.60f, 0.7f, f, PartTone.Light);            // crown
                s.Pyramid(0.80f, 1.05f, 1.20f, 1.45f, 1.2f, f + 0.7f, PartTone.Dark);  // spire
            }
            else
            {
                s.Box(0.20f, 0.70f, 1.10f, 1.70f, f - 1f, 1f, PartTone.Wall);          // tall tower to one side
                s.Box(0.28f, 0.78f, 1.02f, 1.62f, 0.7f, f, PartTone.Light);            // crown
                s.Box(1.30f, 0.90f, 1.85f, 1.50f, f - 2f, 1f, PartTone.Wall);          // shorter tower beside it
            }
        }

        private static void Mall(Sketch s)
        {
            float f = s.Floors;

            s.Box(0.05f, 0.05f, s.W - 0.05f, s.D - 0.05f, f, 0f, PartTone.Wall);

            if (s.Rng.Next(2) == 0)
            {
                s.Box(s.W * 0.5f - 0.5f, 0.02f, s.W * 0.5f + 0.5f, 0.70f, f + 1f, 0f, PartTone.Light); // entrance block in the middle
            }
            else
            {
                s.Box(0.20f, 0.02f, 1.20f, 0.70f, f + 1f, 0f, PartTone.Light);                           // entrance block at one end
            }

            s.Box(0.30f, 0.90f, 1.00f, s.D - 0.30f, 0.4f, f, PartTone.Light);                       // skylights
            s.Box(s.W - 1.00f, 0.90f, s.W - 0.30f, s.D - 0.30f, 0.4f, f, PartTone.Light);
        }

        // ------------------------------------------------------------ industrial

        private static void Shed(Sketch s)
        {
            s.Box(0.10f, 0.10f, 0.90f, 0.90f, 1f, 0f, PartTone.Wall);
            s.Gable(0.06f, 0.06f, 0.94f, 0.94f, 0.45f, 1f, s.Rng.Next(2) == 0, PartTone.Light);
        }

        private static void Warehouse(Sketch s)
        {
            float f = s.Floors;
            s.Box(0.05f, 0.25f, s.W - 0.05f, s.D - 0.05f, f, 0f, PartTone.Wall);

            for (int i = 0; i < s.W; i++)
            {
                s.Gable(i + 0.05f, 0.25f, i + 0.95f, s.D - 0.05f, 0.4f, f, false, PartTone.Light); // sawtooth roof
                s.Box(i + 0.20f, 0.03f, i + 0.80f, 0.25f, 0.5f, 0f, PartTone.Dark);               // loading dock
            }

            if (s.Rng.Next(2) == 0)
            {
                s.Cylinder(s.W - 0.5f, 0.6f, 0.25f, 0.7f, f, PartTone.Dark); // vent stack on the roof
            }
        }

        private static void Factory(Sketch s)
        {
            float f = s.Floors;

            s.Box(0.06f, 0.06f, 1.94f, 1.10f, f, 0f, PartTone.Wall);                        // main hall
            s.Gable(0.06f, 0.06f, 1.00f, 1.10f, 0.45f, f, false, PartTone.Light);           // two saw-tooth roofs
            s.Gable(1.00f, 0.06f, 1.94f, 1.10f, 0.45f, f, false, PartTone.Light);
            s.Box(0.10f, 1.20f, 1.15f, 1.90f, 1f, 0f, PartTone.Light);                      // annex
            s.Cylinder(1.50f, 1.55f, 0.30f, f + 3f, 0f, PartTone.Dark);                     // chimneys
            s.Cylinder(1.78f, 1.30f, 0.24f, f + 2f, 0f, PartTone.Dark);
        }

        private static void SiloYard(Sketch s)
        {
            float tall = 3f + s.Rng.Next(2);

            for (int i = 0; i < s.W; i++)
            {
                float h = i % 2 == 0 ? tall : tall - 0.5f;
                s.Cylinder(i + 0.5f, 0.5f, 0.85f, h, 0f, PartTone.Wall);
                s.Pyramid(i + 0.1f, 0.1f, i + 0.9f, 0.9f, 0.6f, h, PartTone.Light);          // conical cap
            }

            s.Box(0.5f, 0.42f, s.W - 0.5f, 0.58f, 0.2f, tall - 1f, PartTone.Dark);           // walkway between the silos
        }

        // ------------------------------------------------------------ drawing helper

        /// <summary>
        /// Collects parts. Designs give corners on the plan (origin at the front-left); this converts them to
        /// the centred coordinates <see cref="BuildingPart"/> uses. Half of all buildings are mirrored left to
        /// right, so two of the same kind often differ in which end the chimney or the low wing is on.
        /// </summary>
        private sealed class Sketch
        {
            private readonly bool mirror;

            public readonly float W;
            public readonly float D;
            public readonly float Floors;
            public readonly Random Rng;
            public readonly List<BuildingPart> Parts = new List<BuildingPart>();

            public Sketch(int width, int depth, int floors, Random rng)
            {
                W = width;
                D = depth;
                Floors = floors;
                Rng = rng;
                mirror = rng.Next(2) == 0;
            }

            public void Box(float x0, float y0, float x1, float y1, float height, float elevation, PartTone tone)
            {
                Add(PartKind.Box, x0, y0, x1, y1, height, elevation, tone, false);
            }

            public void Gable(float x0, float y0, float x1, float y1, float height, float elevation, bool ridgeAlongX, PartTone tone)
            {
                Add(PartKind.GableRoof, x0, y0, x1, y1, height, elevation, tone, ridgeAlongX);
            }

            public void Pyramid(float x0, float y0, float x1, float y1, float height, float elevation, PartTone tone)
            {
                Add(PartKind.Pyramid, x0, y0, x1, y1, height, elevation, tone, false);
            }

            public void Cylinder(float centerX, float centerY, float diameter, float height, float elevation, PartTone tone)
            {
                float r = diameter * 0.5f;
                Add(PartKind.Cylinder, centerX - r, centerY - r, centerX + r, centerY + r, height, elevation, tone, false);
            }

            private void Add(PartKind kind, float x0, float y0, float x1, float y1, float height, float elevation, PartTone tone, bool ridgeAlongX)
            {
                float left = mirror ? W - x1 : x0;
                float right = mirror ? W - x0 : x1;

                Parts.Add(new BuildingPart(
                    kind,
                    (left + right) * 0.5f - W * 0.5f,
                    (y0 + y1) * 0.5f - D * 0.5f,
                    right - left,
                    y1 - y0,
                    Math.Max(0.05f, height),
                    Math.Max(0f, elevation),
                    tone,
                    ridgeAlongX));
            }
        }
    }
}
