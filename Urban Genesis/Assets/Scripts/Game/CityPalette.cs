using UnityEngine;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Game
{
    /// <summary>All the colours used on the ground and in tool previews, in one place.</summary>
    public static class CityPalette
    {
        // Ground texture
        public static readonly Color32 Ground = new Color32(140, 152, 128, 255);
        public static readonly Color32 GroundLine = new Color32(121, 133, 110, 255);
        public static readonly Color32 Road = new Color32(52, 52, 58, 255);
        public static readonly Color32 RoadEdge = new Color32(96, 96, 106, 255);

        private static readonly Color32 ResidentialFill = new Color32(112, 196, 106, 255);
        private static readonly Color32 ResidentialLine = new Color32(88, 160, 84, 255);
        private static readonly Color32 CommercialFill = new Color32(92, 142, 230, 255);
        private static readonly Color32 CommercialLine = new Color32(70, 112, 190, 255);
        private static readonly Color32 IndustrialFill = new Color32(234, 198, 78, 255);
        private static readonly Color32 IndustrialLine = new Color32(190, 160, 58, 255);

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

        public static Color32 ZoneLine(ZoneType zone)
        {
            switch (zone)
            {
                case ZoneType.Residential: return ResidentialLine;
                case ZoneType.Commercial: return CommercialLine;
                case ZoneType.Industrial: return IndustrialLine;
                default: return GroundLine;
            }
        }

        /// <summary>Translucent version of a zone's colour, for tool previews.</summary>
        public static Color ZonePreview(ZoneType zone)
        {
            Color32 fill = ZoneFill(zone);
            return new Color(fill.r / 255f, fill.g / 255f, fill.b / 255f, 0.60f);
        }

        /// <summary>Gray level for building cubes (the game is grayscale for now).</summary>
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
