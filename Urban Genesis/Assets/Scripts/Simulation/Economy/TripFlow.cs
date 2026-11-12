namespace UrbanGenesis.Simulation
{
    /// <summary>What a trip carries. Each kind is one "need" that pulls people or goods across the road network.</summary>
    public enum FlowKind
    {
        /// <summary>Workers travelling from homes to jobs (commercial and industrial buildings).</summary>
        Work,

        /// <summary>Goods travelling from factories and warehouses to shops.</summary>
        Goods,

        /// <summary>Residents travelling from homes to shops.</summary>
        Shopping
    }

    /// <summary>
    /// A bundle of matched trips between two road segments: the output of the economy and, later, the input of traffic.
    /// The economy decides WHO goes WHERE and how much (<see cref="Volume"/>); traffic will decide which roads they use
    /// and how congested those get. Origins and destinations are road segments (every building stands beside exactly one).
    /// </summary>
    public struct TripFlow
    {
        public FlowKind Kind;

        /// <summary>Segment the trips start on (homes for Work and Shopping, factories for Goods).</summary>
        public int FromSegmentId;

        /// <summary>Segment the trips end on (jobs, or shops).</summary>
        public int ToSegmentId;

        /// <summary>Units matched per economy update (people for Work and Shopping, goods for Goods).</summary>
        public double Volume;

        /// <summary>Travel cost along the roads, in metres of free-flowing road (see <see cref="IRoadCost"/>).</summary>
        public double Distance;
    }
}
