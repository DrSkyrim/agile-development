using System;
using System.Collections.Generic;

namespace UrbanGenesis.Simulation
{
    /// <summary>What one building gets out of the city right now. Filled in by <see cref="CityEconomy.Update"/>.</summary>
    public sealed class BuildingEconomy
    {
        public int BuildingId;
        public int SegmentId;

        // What the building offers and needs (full strength).
        public double Residents;
        public double Workers;
        public double Jobs;
        public double GoodsIn;
        public double GoodsOut;
        public double Service;

        /// <summary>Homes: share of the workers who have a job within reach (0 to 1).</summary>
        public double EmploymentFill = 1.0;

        /// <summary>Homes: share of the shopping trips that found a shop within reach (0 to 1).</summary>
        public double ShoppingFill = 1.0;

        /// <summary>Businesses: share of the jobs that are filled (0 to 1). Output and service scale with it.</summary>
        public double Staffing = 1.0;

        /// <summary>Shops: share of the goods they need that arrive (0 to 1). Service scales with it.</summary>
        public double GoodsFill = 1.0;

        /// <summary>Shoppers this shop can really serve, after staffing and deliveries.</summary>
        public double EffectiveService;

        /// <summary>Goods this building really produces, after staffing.</summary>
        public double EffectiveGoodsOut;
    }

    /// <summary>The city's totals, recalculated on every economy update. All shares are 0 to 1.</summary>
    public sealed class EconomySnapshot
    {
        public double Residents;
        public double Workers;
        public double Jobs;
        public double Employed;

        public double GoodsDemand;
        public double GoodsSupply;
        public double GoodsDelivered;

        public double ShopDemand;
        public double ServiceCapacity;
        public double ShopServed;

        public double Unemployed { get { return Workers - Employed; } }
        public double UnfilledJobs { get { return Jobs - Employed; } }
        public double UnemploymentRate { get { return Workers > 0.0 ? (Workers - Employed) / Workers : 0.0; } }
        public double UnfilledJobShare { get { return Jobs > 0.0 ? (Jobs - Employed) / Jobs : 0.0; } }
        public double GoodsSatisfaction { get { return GoodsDemand > 0.0 ? GoodsDelivered / GoodsDemand : 1.0; } }
        public double GoodsSurplusShare { get { return GoodsSupply > 0.0 ? (GoodsSupply - GoodsDelivered) / GoodsSupply : 0.0; } }
        public double ShopSatisfaction { get { return ShopDemand > 0.0 ? ShopServed / ShopDemand : 1.0; } }
        public double SpareServiceShare { get { return ServiceCapacity > 0.0 ? (ServiceCapacity - ShopServed) / ServiceCapacity : 0.0; } }

        /// <summary>How content residents are, from jobs and shops (0 to 1). Meaningless while nobody lives here.</summary>
        public double Happiness
        {
            get { return Residents > 0.0 ? (1.0 - UnemploymentRate) * 0.5 + ShopSatisfaction * 0.5 : 1.0; }
        }
    }

    /// <summary>
    /// The city's supply and demand. Three needs link the three zone types in a loop:
    ///  * <b>Work</b>: industry and commerce need workers, homes supply them (and need jobs).
    ///  * <b>Goods</b>: shops need goods, industry supplies them.
    ///  * <b>Shopping</b>: homes need shops, shops supply service (and need goods and staff to do it).
    /// Each need is matched between buildings that are connected by open roads and close enough, nearest pairs first.
    /// What is left unmatched becomes demand for more of the right zone, and growth follows that demand
    /// (see <see cref="CitySimulation.Step"/>).
    ///
    /// The matched trips are kept as <see cref="Flows"/>: who travels from which road segment to which, and how many.
    /// Traffic will route exactly these trips over the roads and feed congestion back through <see cref="Roads"/>
    /// (<see cref="RoadGraph.Cost"/>), so the economy reacts to jams and closures without any change to this class.
    /// Matching works per road segment, not per building, to stay cheap in a big city; buildings on one segment share
    /// the same match rates.
    /// </summary>
    public sealed class CityEconomy
    {
        private const double Epsilon = 1e-9;

        private readonly CitySimulation sim;
        private readonly Dictionary<int, BuildingEconomy> byBuilding = new Dictionary<int, BuildingEconomy>();
        private List<TripFlow> flows = new List<TripFlow>();
        private double closureSignature = double.NaN;

        public EconomyParameters Parameters { get; private set; }
        public RoadGraph Roads { get; private set; }
        public EconomySnapshot Snapshot { get; private set; }

        /// <summary>Economy updates between the simulation's ticks (the economy runs every this many ticks).</summary>
        public int UpdateIntervalTicks { get; set; }

        /// <summary>If true, buildings only grow where there is demand. Turn off for tests that want unconditional growth.</summary>
        public bool DemandDrivesGrowth { get; set; }

        /// <summary>Number of updates so far.</summary>
        public int Revision { get; private set; }

        public double ResidentialDemand { get; private set; }
        public double CommercialDemand { get; private set; }
        public double IndustrialDemand { get; private set; }

        /// <summary>The most pressing problem in words, or empty if the economy is in order.</summary>
        public string Advice { get; private set; }

        /// <summary>Matched trips from the last update. Replaced as a whole on every update.</summary>
        public IList<TripFlow> Flows { get { return flows; } }

        public event Action Updated;

        internal CityEconomy(CitySimulation simulation)
        {
            sim = simulation;
            Parameters = new EconomyParameters();
            Roads = new RoadGraph(sim.Roads);
            Snapshot = new EconomySnapshot();
            UpdateIntervalTicks = 4;
            DemandDrivesGrowth = true;
            Advice = "";
            ResidentialDemand = Parameters.BaseResidentialDemand;
            CommercialDemand = Parameters.BaseCommercialDemand;
            IndustrialDemand = Parameters.BaseIndustrialDemand;

            sim.Roads.SegmentAdded += s => Roads.Invalidate();
            sim.Roads.SegmentRemoved += s => Roads.Invalidate();
            sim.Roads.SegmentSplit += (old, a, b, l) => Roads.Invalidate();
        }

        /// <summary>The 0 to 1 demand for a zone type (how much the city wants more of it right now).</summary>
        public double DemandFor(ZoneType zone)
        {
            switch (zone)
            {
                case ZoneType.Residential: return ResidentialDemand;
                case ZoneType.Commercial: return CommercialDemand;
                case ZoneType.Industrial: return IndustrialDemand;
                default: return 0.0;
            }
        }

        /// <summary>What a building gets from the city, or null if it has not been through an update yet.</summary>
        public BuildingEconomy StatusOf(Building building)
        {
            BuildingEconomy status;
            return byBuilding.TryGetValue(building.Id, out status) ? status : null;
        }

        // ---------------------------------------------------------------------------------------------

        private sealed class SegmentTotals
        {
            public int SegmentId;
            public double Residents;
            public double Workers, Jobs;
            public double GoodsOut, GoodsIn;
            public double Shoppers, Service;

            // Results of matching.
            public double WorkersEmployed, JobsFilled;
            public double GoodsSent, GoodsReceived;
            public double ShoppersServed, ServiceUsed;

            public double Staffing { get { return Jobs > Epsilon ? Math.Min(1.0, JobsFilled / Jobs) : 1.0; } }
            public double GoodsFill { get { return GoodsIn > Epsilon ? Math.Min(1.0, GoodsReceived / GoodsIn) : 1.0; } }
        }

        /// <summary>Recalculates matches, demand and per-building status from the city as it is now.</summary>
        public void Update()
        {
            DetectRoadChanges();

            EconomyParameters p = Parameters;
            byBuilding.Clear();
            var segments = new Dictionary<int, SegmentTotals>();
            var snapshot = new EconomySnapshot();

            // 1. What every building offers and needs, added up per road segment.
            foreach (Building b in sim.Buildings)
            {
                if (b.Lots.Count == 0)
                {
                    continue;
                }

                int segmentId = b.Lots[0].Key.SegmentId;
                if (sim.Roads.GetSegment(segmentId) == null)
                {
                    continue;
                }

                var e = new BuildingEconomy { BuildingId = b.Id, SegmentId = segmentId };
                double units = EconomyParameters.Units(b);
                if (b.Zone == ZoneType.Residential)
                {
                    e.Residents = b.Residents;
                    e.Workers = e.Residents * p.WorkingShare;
                }
                else
                {
                    EconomyProfile profile = p.ProfileOf(b.Shape);
                    e.Jobs = profile.Jobs * units;
                    e.Service = profile.Service * units;
                    e.GoodsIn = profile.GoodsIn * units;
                    e.GoodsOut = profile.GoodsOut * units;
                }

                byBuilding[b.Id] = e;

                SegmentTotals t = TotalsOf(segments, segmentId);
                t.Residents += e.Residents;
                t.Workers += e.Workers;
                t.Jobs += e.Jobs;
                t.Shoppers += e.Residents * p.ShoppingPerResident;
            }

            var flowList = new List<TripFlow>();

            // 2. Work: homes send workers to jobs.
            Match(FlowKind.Work, segments, t => t.Workers, t => t.Jobs, (t, v) => t.WorkersEmployed += v, (t, v) => t.JobsFilled += v,
                p.MaxWorkDistance, true, flowList);

            // Businesses can only produce what their staff allows. Goods supply and need depend on that.
            foreach (BuildingEconomy e in byBuilding.Values)
            {
                SegmentTotals t = segments[e.SegmentId];
                e.Staffing = t.Staffing;
                Building b = sim.GetBuilding(e.BuildingId);
                EconomyProfile profile = p.ProfileOf(b.Shape);
                double units = EconomyParameters.Units(b);
                if (b.Zone != ZoneType.Residential)
                {
                    e.GoodsIn = profile.GoodsIn * units * e.Staffing;
                    e.EffectiveGoodsOut = profile.GoodsOut * units * e.Staffing;
                    t.GoodsIn += e.GoodsIn;
                    t.GoodsOut += e.EffectiveGoodsOut;
                }
            }

            // 3. Goods: factories send goods to shops.
            Match(FlowKind.Goods, segments, t => t.GoodsOut, t => t.GoodsIn, (t, v) => t.GoodsSent += v, (t, v) => t.GoodsReceived += v,
                p.MaxGoodsDistance, true, flowList);

            // Shops can only serve what their staff and deliveries allow.
            foreach (BuildingEconomy e in byBuilding.Values)
            {
                SegmentTotals t = segments[e.SegmentId];
                e.GoodsFill = t.GoodsFill;
                Building b = sim.GetBuilding(e.BuildingId);
                if (b.Zone != ZoneType.Residential)
                {
                    e.EffectiveService = p.ProfileOf(b.Shape).Service * EconomyParameters.Units(b) * e.Staffing * e.GoodsFill;
                    t.Service += e.EffectiveService;
                }
            }

            // 4. Shopping: homes send shoppers to shops. (The shoppers are the consumers; the trip starts at their home.)
            Match(FlowKind.Shopping, segments, t => t.Service, t => t.Shoppers, (t, v) => t.ServiceUsed += v, (t, v) => t.ShoppersServed += v,
                p.MaxShoppingDistance, false, flowList);

            // 5. Hand the results back to the buildings and add up the city.
            foreach (BuildingEconomy e in byBuilding.Values)
            {
                SegmentTotals t = segments[e.SegmentId];
                if (e.Residents > 0.0)
                {
                    e.EmploymentFill = t.Workers > Epsilon ? Math.Min(1.0, t.WorkersEmployed / t.Workers) : 1.0;
                    e.ShoppingFill = t.Shoppers > Epsilon ? Math.Min(1.0, t.ShoppersServed / t.Shoppers) : 1.0;
                }
            }

            foreach (SegmentTotals t in segments.Values)
            {
                snapshot.Residents += t.Residents;
                snapshot.Workers += t.Workers;
                snapshot.Jobs += t.Jobs;
                snapshot.Employed += t.WorkersEmployed;
                snapshot.GoodsDemand += t.GoodsIn;
                snapshot.GoodsSupply += t.GoodsOut;
                snapshot.GoodsDelivered += t.GoodsReceived;
                snapshot.ShopDemand += t.Shoppers;
                snapshot.ServiceCapacity += t.Service;
                snapshot.ShopServed += t.ShoppersServed;
            }

            Snapshot = snapshot;
            flows = flowList;
            Revision++;
            UpdateDemand();
            Advice = ChooseAdvice();
            sim.Treasury.Recompute();

            Action updated = Updated;
            if (updated != null)
            {
                updated();
            }
        }

        // ---------------------------------------------------------------------------------------------
        // Demand

        private void UpdateDemand()
        {
            EconomyParameters p = Parameters;
            EconomySnapshot s = Snapshot;
            double unemployment = s.UnemploymentRate;
            double goodsSat = s.GoodsSatisfaction;

            // Homes: people move in where jobs are going unfilled and shops have room; they stay away while unemployed.
            double residential = p.BaseResidentialDemand + 0.9 * s.UnfilledJobShare + 0.25 * s.SpareServiceShare - 1.1 * unemployment;

            // Industry: wanted when shops lack goods and when people lack jobs; unwanted while goods pile up unsold.
            double industrial = p.BaseIndustrialDemand + 0.8 * unemployment + 0.9 * (1.0 - goodsSat) - 0.9 * s.GoodsSurplusShare;

            // Shops: wanted when residents cannot shop and there are goods to sell; offices also employ people.
            double commercial = p.BaseCommercialDemand + 0.9 * (1.0 - s.ShopSatisfaction) * goodsSat + 0.4 * unemployment
                + 0.6 * s.GoodsSurplusShare - 0.9 * s.SpareServiceShare;

            ResidentialDemand = Follow(ResidentialDemand, residential);
            CommercialDemand = Follow(CommercialDemand, commercial);
            IndustrialDemand = Follow(IndustrialDemand, industrial);
        }

        private double Follow(double current, double target)
        {
            target = Math.Max(0.0, Math.Min(1.0, target));
            return current + (target - current) * Parameters.DemandSmoothing;
        }

        private string ChooseAdvice()
        {
            EconomySnapshot s = Snapshot;
            if (s.Residents > 0.0 && s.Workers > 0.0 && s.UnemploymentRate > 0.25)
            {
                return "Residents need jobs: zone industrial or commercial near their homes.";
            }

            if (s.GoodsDemand > 0.0 && s.GoodsSatisfaction < 0.6)
            {
                return "Shops are short of goods: they need industry connected by road.";
            }

            if (s.ShopDemand > 0.0 && s.ShopSatisfaction < 0.6)
            {
                return "Residents have no shops within reach: zone commercial close to homes.";
            }

            if (s.Jobs > 0.0 && s.UnfilledJobShare > 0.4)
            {
                return "Businesses cannot find workers: zone more residential nearby.";
            }

            return "";
        }

        // ---------------------------------------------------------------------------------------------
        // Matching

        private struct Pair
        {
            public SegmentTotals Supplier;
            public SegmentTotals Consumer;
            public double Distance;
        }

        /// <summary>
        /// Matches one kind of supply with its demand, nearest pairs first. Only segments connected by open roads
        /// and closer than <paramref name="maxDistance"/> are matched. Every match becomes a <see cref="TripFlow"/>;
        /// <paramref name="supplierIsOrigin"/> says which way the trip runs.
        /// </summary>
        private void Match(FlowKind kind, Dictionary<int, SegmentTotals> segments,
            Func<SegmentTotals, double> supply, Func<SegmentTotals, double> demand,
            Action<SegmentTotals, double> supplied, Action<SegmentTotals, double> consumed,
            double maxDistance, bool supplierIsOrigin, List<TripFlow> output)
        {
            var suppliers = new List<SegmentTotals>();
            var consumers = new List<SegmentTotals>();
            foreach (SegmentTotals t in segments.Values)
            {
                if (supply(t) > Epsilon) { suppliers.Add(t); }
                if (demand(t) > Epsilon) { consumers.Add(t); }
            }

            if (suppliers.Count == 0 || consumers.Count == 0)
            {
                return;
            }

            suppliers.Sort((a, b) => a.SegmentId.CompareTo(b.SegmentId));
            consumers.Sort((a, b) => a.SegmentId.CompareTo(b.SegmentId));

            var pairs = new List<Pair>();
            foreach (SegmentTotals s in suppliers)
            {
                RoadSegment from = sim.Roads.GetSegment(s.SegmentId);
                foreach (SegmentTotals c in consumers)
                {
                    double d = Roads.SegmentDistance(from, sim.Roads.GetSegment(c.SegmentId));
                    if (d <= maxDistance)
                    {
                        pairs.Add(new Pair { Supplier = s, Consumer = c, Distance = d });
                    }
                }
            }

            pairs.Sort((a, b) =>
            {
                int byDistance = a.Distance.CompareTo(b.Distance);
                if (byDistance != 0) { return byDistance; }
                int bySupplier = a.Supplier.SegmentId.CompareTo(b.Supplier.SegmentId);
                return bySupplier != 0 ? bySupplier : a.Consumer.SegmentId.CompareTo(b.Consumer.SegmentId);
            });

            var supplyLeft = new Dictionary<int, double>();
            var demandLeft = new Dictionary<int, double>();
            foreach (SegmentTotals s in suppliers) { supplyLeft[s.SegmentId] = supply(s); }
            foreach (SegmentTotals c in consumers) { demandLeft[c.SegmentId] = demand(c); }

            var pairsOf = new Dictionary<int, List<Pair>>();
            foreach (Pair pair in pairs)
            {
                List<Pair> list;
                if (!pairsOf.TryGetValue(pair.Supplier.SegmentId, out list))
                {
                    list = new List<Pair>();
                    pairsOf[pair.Supplier.SegmentId] = list;
                }

                list.Add(pair);
            }

            var flowIndex = new Dictionary<long, int>();
            Action<Pair, double> transfer = (pair, amount) =>
            {
                int si = pair.Supplier.SegmentId;
                int ci = pair.Consumer.SegmentId;
                supplyLeft[si] -= amount;
                demandLeft[ci] -= amount;
                supplied(pair.Supplier, amount);
                consumed(pair.Consumer, amount);

                int from = supplierIsOrigin ? si : ci;
                int to = supplierIsOrigin ? ci : si;
                long key = ((long)from << 32) ^ (uint)to;
                int index;
                if (flowIndex.TryGetValue(key, out index))
                {
                    TripFlow existing = output[index];
                    existing.Volume += amount;
                    output[index] = existing;
                }
                else
                {
                    flowIndex[key] = output.Count;
                    output.Add(new TripFlow { Kind = kind, FromSegmentId = from, ToSegmentId = to, Volume = amount, Distance = pair.Distance });
                }
            };

            // Rounds: every round each supplier ships a slice of what it has left, split between the consumers it can
            // reach in proportion to how much they still want and how near they are (nearer pulls harder, but far
            // consumers still get a share). Without this, the nearest building would take everything and a factory a
            // little further from the homes would never be staffed.
            double scale = Math.Max(1.0, maxDistance * 0.25);
            int rounds = Math.Max(1, Parameters.MatchingRounds);
            for (int round = 0; round < rounds; round++)
            {
                foreach (SegmentTotals s in suppliers)
                {
                    double slice = supplyLeft[s.SegmentId] / (rounds - round);
                    if (slice <= Epsilon)
                    {
                        continue;
                    }

                    List<Pair> reachable;
                    if (!pairsOf.TryGetValue(s.SegmentId, out reachable))
                    {
                        continue;
                    }

                    double totalWeight = 0.0;
                    for (int i = 0; i < reachable.Count; i++)
                    {
                        totalWeight += Weight(reachable[i], demandLeft, scale);
                    }

                    if (totalWeight <= Epsilon)
                    {
                        continue;
                    }

                    for (int i = 0; i < reachable.Count; i++)
                    {
                        double share = slice * Weight(reachable[i], demandLeft, scale) / totalWeight;
                        double amount = Math.Min(share, Math.Min(supplyLeft[s.SegmentId], demandLeft[reachable[i].Consumer.SegmentId]));
                        if (amount > Epsilon)
                        {
                            transfer(reachable[i], amount);
                        }
                    }
                }
            }

            // Whatever the slices left behind goes to the nearest consumer that still wants it.
            foreach (Pair pair in pairs)
            {
                double amount = Math.Min(supplyLeft[pair.Supplier.SegmentId], demandLeft[pair.Consumer.SegmentId]);
                if (amount > Epsilon)
                {
                    transfer(pair, amount);
                }
            }
        }

        private static double Weight(Pair pair, Dictionary<int, double> demandLeft, double scale)
        {
            double ratio = pair.Distance / scale;
            return demandLeft[pair.Consumer.SegmentId] / (1.0 + ratio * ratio);
        }

        private static SegmentTotals TotalsOf(Dictionary<int, SegmentTotals> segments, int segmentId)
        {
            SegmentTotals t;
            if (!segments.TryGetValue(segmentId, out t))
            {
                t = new SegmentTotals { SegmentId = segmentId };
                segments[segmentId] = t;
            }

            return t;
        }

        /// <summary>Forgets cached routes when a road's open fraction (closure, lane works) has changed.</summary>
        private void DetectRoadChanges()
        {
            double signature = 0.0;
            foreach (RoadSegment segment in sim.Roads.Segments)
            {
                signature += (segment.Id + 1) * (double)segment.OpenFraction;
            }

            if (signature != closureSignature)
            {
                closureSignature = signature;
                Roads.Invalidate();
            }
        }
    }
}
