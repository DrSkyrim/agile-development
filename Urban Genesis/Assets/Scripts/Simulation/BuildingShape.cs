namespace UrbanGenesis.Simulation
{
    /// <summary>
    /// The kinds of building the city can grow. The simulation only stores which one a building is;
    /// the Unity layer decides what it looks like.
    /// </summary>
    public enum BuildingShape
    {
        // Residential
        Cottage,          // 1x1, small house with a pitched roof
        RowHouses,        // 2x1, two terraced houses of different heights
        ApartmentBlock,   // flat-roofed block on a dark plinth
        LBlock,           // 2x2, L-shaped block
        CourtyardBlock,   // 3x2, U-shaped block around a courtyard
        TerraceTower,     // 2x2, tower with stepped-back tiers

        // Commercial
        Shop,             // 1x1, shop with an awning and a roof sign
        OfficeSlab,       // office slab on a lobby base
        Tower,            // 1x1, slim stepped tower with a spire
        PodiumTower,      // 2x2, low podium with a tower on top
        Mall,             // 3x2, wide low mall with an entrance block

        // Industrial
        Shed,             // 1x1, small shed
        Warehouse,        // long hall with loading docks
        Factory,          // 2x2, hall, annex and chimneys
        SiloYard          // 2x1, grain silos
    }
}
