using UnityEngine;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Game
{
    /// <summary>All the colours used on the ground and in tool previews, in one place.</summary>
    public static class CityPalette
    {
        // Ground and roads
        public static readonly Color32 Ground = new Color32(140, 152, 128, 255);
        public static readonly Color32 GroundLine = new Color32(129, 141, 118, 255);
        public static readonly Color32 Road = new Color32(52, 52, 58, 255);
        public static readonly Color32 RoadEdge = new Color32(176, 176, 186, 255);
        public static readonly Color32 RoadCentre = new Color32(226, 196, 84, 255);

        private static readonly Color32 ResidentialFill = new Color32(112, 196, 106, 255);
        private static readonly Color32 CommercialFill = new Color32(92, 142, 230, 255);
        private static readonly Color32 IndustrialFill = new Color32(234, 198, 78, 255);

        // Lots that are not zoned yet (only shown while a zoning or bulldoze tool is active)
        public static readonly Color LotOutline = new Color(1f, 1f, 1f, 0.16f);

        // Tool preview overlay (translucent)
        public static readonly Color PreviewValid = new Color(0.35f, 0.85f, 1.00f, 0.50f);
        public static readonly Color PreviewInvalid = new Color(1.00f, 0.30f, 0.30f, 0.50f);
        public static readonly Color PreviewNeutral = new Color(1.00f, 1.00f, 1.00f, 0.12f);
        public static readonly Color PreviewHover = new Color(1.00f, 1.00f, 1.00f, 0.30f);
        public static readonly Color PreviewBulldoze = new Color(1.00f, 0.45f, 0.20f, 0.60f);

        public static Color32 ZoneFill(ZoneType zone)
        {
            switch (zone)
            {
                case ZoneType.Residential: return ResidentialFill;
                case ZoneType.Commercial: return CommercialFill;
                case ZoneType.Industrial: return IndustrialFill;
                default: return Ground;
            }
        }

        /// <summary>A zone's colour with the given opacity, for tinting lots.</summary>
        public static Color ZoneTint(ZoneType zone, float alpha)
        {
            Color32 fill = ZoneFill(zone);
            return new Color(fill.r / 255f, fill.g / 255f, fill.b / 255f, alpha);
        }

        /// <summary>Translucent version of a zone's colour, for tool previews.</summary>
        public static Color ZonePreview(ZoneType zone)
        {
            Color32 fill = ZoneFill(zone);
            return new Color(fill.r / 255f, fill.g / 255f, fill.b / 255f, 0.60f);
        }

        /// <summary>
        /// Gray level for one part of a building: the zone's base gray, nudged by a per-building shade
        /// (0 to 4) so neighbours differ a little, then lightened for roofs and accents or darkened for plinths.
        /// </summary>
        public static float PartBrightness(ZoneType zone, int shade, PartTone tone)
        {
            float brightness = BuildingBrightness(zone) + (shade - 2) * 0.04f;

            switch (tone)
            {
                case PartTone.Light: brightness += 0.14f; break;
                case PartTone.Dark: brightness -= 0.26f; break;
            }

            return Mathf.Clamp(brightness, 0.06f, 1f);
        }

        /// <summary>Gray level for the main walls of a building (the game is grayscale for now).</summary>
        public static float BuildingBrightness(ZoneType zone)
        {
            switch (zone)
            {
                case ZoneType.Residential: return 0.82f;
                case ZoneType.Commercial: return 0.62f;
                case ZoneType.Industrial: return 0.42f;
                default: return 0.70f;
            }
        }
    }
}
