using System;
using System.Collections.Generic;

namespace UrbanGenesis.Simulation
{
    /// <summary>
    /// Every building template, grouped by zone. To add a new building, add a line here
    /// (and teach the Unity layer to draw its shape). Every zone must keep at least one 1x1 template,
    /// because that is what fills the gaps that bigger buildings do not fit into.
    /// </summary>
    public static class BuildingCatalog
    {
        private static readonly BuildingTemplate[] Residential =
        {
            new BuildingTemplate(BuildingShape.Cottage,        ZoneType.Residential, 1, 1, 1, 2, 5.0),
            new BuildingTemplate(BuildingShape.RowHouses,      ZoneType.Residential, 2, 1, 2, 3, 3.0),
            new BuildingTemplate(BuildingShape.ApartmentBlock, ZoneType.Residential, 2, 1, 3, 5, 2.0),
            new BuildingTemplate(BuildingShape.ApartmentBlock, ZoneType.Residential, 2, 2, 3, 6, 2.0),
            new BuildingTemplate(BuildingShape.LBlock,         ZoneType.Residential, 2, 2, 3, 5, 1.5),
            new BuildingTemplate(BuildingShape.CourtyardBlock, ZoneType.Residential, 3, 2, 3, 5, 1.0),
            new BuildingTemplate(BuildingShape.TerraceTower,   ZoneType.Residential, 2, 2, 5, 8, 1.0)
        };

        private static readonly BuildingTemplate[] Commercial =
        {
            new BuildingTemplate(BuildingShape.Shop,        ZoneType.Commercial, 1, 1, 1, 2, 4.0),
            new BuildingTemplate(BuildingShape.OfficeSlab,  ZoneType.Commercial, 2, 1, 3, 6, 3.0),
            new BuildingTemplate(BuildingShape.Tower,       ZoneType.Commercial, 1, 1, 5, 9, 2.0),
            new BuildingTemplate(BuildingShape.PodiumTower, ZoneType.Commercial, 2, 2, 4, 8, 1.5),
            new BuildingTemplate(BuildingShape.Mall,        ZoneType.Commercial, 3, 2, 1, 2, 1.0)
        };

        private static readonly BuildingTemplate[] Industrial =
        {
            new BuildingTemplate(BuildingShape.Shed,      ZoneType.Industrial, 1, 1, 1, 1, 3.0),
            new BuildingTemplate(BuildingShape.Warehouse, ZoneType.Industrial, 2, 1, 1, 2, 3.0),
            new BuildingTemplate(BuildingShape.Warehouse, ZoneType.Industrial, 3, 1, 1, 2, 2.0),
            new BuildingTemplate(BuildingShape.Factory,   ZoneType.Industrial, 2, 2, 1, 2, 2.0),
            new BuildingTemplate(BuildingShape.SiloYard,  ZoneType.Industrial, 2, 1, 1, 1, 2.0)
        };

        private static readonly BuildingTemplate[] None = new BuildingTemplate[0];

        public static IList<BuildingTemplate> ForZone(ZoneType zone)
        {
            switch (zone)
            {
                case ZoneType.Residential: return Residential;
                case ZoneType.Commercial: return Commercial;
                case ZoneType.Industrial: return Industrial;
                default: return None;
            }
        }

        public static IEnumerable<BuildingTemplate> All()
        {
            foreach (BuildingTemplate t in Residential) { yield return t; }
            foreach (BuildingTemplate t in Commercial) { yield return t; }
            foreach (BuildingTemplate t in Industrial) { yield return t; }
        }

        /// <summary>
        /// Picks a template for a zone at random, weighted by <see cref="BuildingTemplate.Weight"/>.
        /// With <paramref name="singleCellOnly"/> set, only 1x1 templates can be picked.
        /// </summary>
        public static BuildingTemplate Pick(ZoneType zone, Random rng, bool singleCellOnly)
        {
            IList<BuildingTemplate> templates = ForZone(zone);

            double total = 0.0;
            foreach (BuildingTemplate t in templates)
            {
                if (!singleCellOnly || t.CellCount == 1)
                {
                    total += t.Weight;
                }
            }

            if (total <= 0.0)
            {
                throw new InvalidOperationException("No building template is available for zone " + zone + ".");
            }

            double roll = rng.NextDouble() * total;
            BuildingTemplate last = null;
            foreach (BuildingTemplate t in templates)
            {
                if (singleCellOnly && t.CellCount != 1)
                {
                    continue;
                }

                last = t;
                roll -= t.Weight;
                if (roll < 0.0)
                {
                    return t;
                }
            }

            return last;
        }
    }
}
