namespace UrbanGenesis.Simulation
{
    /// <summary>How simulation ticks map to the game's calendar. One tick is ten minutes of game time.</summary>
    public static class GameTime
    {
        public const double MinutesPerTick = 10.0;
        public const int TicksPerDay = 24 * 6;
        public const int TicksPerWeek = 7 * TicksPerDay;
    }
}
