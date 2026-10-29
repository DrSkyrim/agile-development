namespace UrbanGenesis.Simulation
{
    /// <summary>
    /// Plain data for one building. The Unity layer reads this to decide what cube to show;
    /// nothing in here knows about Unity.
    /// </summary>
    public sealed class Building
    {
        public int Id { get; private set; }
        public GridCoord Cell { get; private set; }
        public ZoneType Zone { get; private set; }

        /// <summary>How many floors tall the building is. The Unity layer turns this into cube height.</summary>
        public int Floors { get; private set; }

        public Building(int id, GridCoord cell, ZoneType zone, int floors)
        {
            Id = id;
            Cell = cell;
            Zone = zone;
            Floors = floors;
        }
    }
}
