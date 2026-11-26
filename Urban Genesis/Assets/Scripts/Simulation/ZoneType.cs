namespace UrbanGenesis.Simulation
{
    public enum ZoneType
    {
        None = 0,
        Residential = 1,
        Commercial = 2,
        Industrial = 3,

        /// <summary>Not a zone the player paints: the land under power plants, hospitals and other buildings the player places.</summary>
        Civic = 4,

        /// <summary>Residential zone that grows apartment blocks and high-rises (unlocked by a milestone).</summary>
        DenseResidential = 5
    }

    public static class Zones
    {
        /// <summary>True for both kinds of housing zone.</summary>
        public static bool IsResidential(ZoneType zone)
        {
            return zone == ZoneType.Residential || zone == ZoneType.DenseResidential;
        }
    }
}
