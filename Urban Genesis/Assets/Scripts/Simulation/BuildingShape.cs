namespace UrbanGenesis.Simulation
{
    /// <summary>
    /// The kinds of building the city can grow. The simulation only stores which one a building is;
    /// the Unity layer decides what it looks like.
    /// </summary>
    public enum BuildingShape
    {
        // Residential (small homes; dense housing comes with a later milestone)
        Cottage,          // 1x1, small house with a pitched roof
        Bungalow,         // 1x1, low house with a hipped roof and a porch
        Chalet,           // 1x1, steep roof and a front deck
        Villa,            // 1x1, two storeys with a corner tower
        Farmhouse,        // 1x2, house with a barn and a silo behind
        RowHouses,        // 2x1 / 3x1, terraced or semi-detached houses
        FourPlex,         // 2x1, two storeys, four apartments

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
