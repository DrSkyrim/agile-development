using System;
using System.Collections.Generic;

namespace UrbanGenesis.Simulation
{
    /// <summary>
    /// The whole city model: roads, the lots around them, zones and buildings. It has no idea Unity exists.
    /// The Unity layer sends it commands (PlanRoad/PlaceRoad, PaintZone, Bulldoze), calls <see cref="Step"/>
    /// on a fixed timer, and listens to the events (or watches <see cref="Revision"/>) to update what is on screen.
    ///
    /// Rules the model keeps true at all times:
    ///  * Roads are free-form curves <see cref="RoadNetwork.RoadWidth"/> metres wide and never cross each other.
    ///  * Lots exist only beside roads, up to <see cref="LotLayout.Rows"/> rows deep, and never under a road.
    ///  * A building stands on a block of lots beside ONE road. Its front is on that road: the lots of its first row
    ///    touch the road, so every building has a direct connection to a road.
    ///  * Every lot a building stands on is zoned for the building's type, and buildings never overlap each other or a road.
    /// </summary>
    public sealed partial class CitySimulation
    {
        public const double DefaultWorldSize = 4096.0;

        /// <summary>A building may stick into a road by this much (metres) because curves bend the straight edges.</summary>
        public const double BuildingRoadTolerance = 0.4;

        /// <summary>Two buildings may overlap by this much (metres) beside a tight bend.</summary>
        public const double BuildingOverlapTolerance = 1.0;

        /// <summary>A building is moved to touch the road, but is refused if that would take more than this (metres).</summary>
        public const double RoadContactTolerance = 1.5;

        private const double HashCell = 32.0;

        private readonly Dictionary<int, Building> buildings = new Dictionary<int, Building>();
        private readonly Dictionary<long, List<Building>> buildingHash = new Dictionary<long, List<Building>>();
        private readonly Random rng;
        private int nextBuildingId = 1;

        private readonly List<Lot> residentialCandidates = new List<Lot>();
        private readonly List<Lot> commercialCandidates = new List<Lot>();
        private readonly List<Lot> industrialCandidates = new List<Lot>();

        public RoadNetwork Roads { get; private set; }
        public LotLayout Lots { get; private set; }

        /// <summary>Supply, demand and trips: what the city wants and what drives growth.</summary>
        public CityEconomy Economy { get; private set; }

        /// <summary>Money: roads cost, working people pay taxes. Off until enabled.</summary>
        public CityTreasury Treasury { get; private set; }

        /// <summary>Number of simulation ticks run so far.</summary>
        public int TickCount { get; private set; }

        /// <summary>Goes up every time roads, lots, zones or buildings change. Handy for "redraw if changed".</summary>
        public int Revision { get; private set; }

        /// <summary>Chance (0 to 1) that each growth attempt on a tick actually places a building.</summary>
        public double SpawnChance { get; set; }

        /// <summary>How many growth attempts each zone type gets per tick.</summary>
        public int GrowthAttemptsPerZonePerTick { get; set; }

        /// <summary>
        /// How strongly big buildings are preferred (0 to 1). Growth always tries the biggest buildings first; at 1 it
        /// always takes the biggest one that fits, below 1 it sometimes skips a big one to keep the skyline varied.
        /// </summary>
        public double BigBuildingPriority { get; set; }

        public event Action<Building> BuildingSpawned;
        public event Action<Building> BuildingRemoved;

        /// <summary>Raised when the zone of a lot changes.</summary>
        public event Action<Lot> ZoneChanged;

        public CitySimulation(int seed) : this(DefaultWorldSize, seed)
        {
        }

        public CitySimulation(double worldSize, int seed)
        {
            double half = worldSize * 0.5;
            Roads = new RoadNetwork(new Aabb(-half, -half, half, half));
            Lots = new LotLayout(Roads);
            Economy = new CityEconomy(this);
            Treasury = new CityTreasury(this);
            rng = new Random(seed);
            SpawnChance = 0.5;
            GrowthAttemptsPerZonePerTick = 2;
            BigBuildingPriority = 0.85;

            Roads.SegmentSplit += OnSegmentSplit;
            Roads.SegmentAdded += OnSegmentAdded;
            Roads.SegmentRemoved += OnSegmentRemoved;
        }

        public int BuildingCount { get { return buildings.Count; } }

        /// <summary>The building with this id, or null.</summary>
        public Building GetBuilding(int id)
        {
            Building b;
            return buildings.TryGetValue(id, out b) ? b : null;
        }
        public IEnumerable<Building> Buildings { get { return buildings.Values; } }

        public int CountBuildings(ZoneType zone)
        {
            int count = 0;
            foreach (Building b in buildings.Values)
            {
                if (b.Zone == zone)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>Finds the building standing on a spot of ground.</summary>
        public bool TryGetBuildingAt(Vec2 p, out Building building)
        {
            List<Building> bucket;
            if (buildingHash.TryGetValue(HashKey(CellOf(p.X), CellOf(p.Y)), out bucket))
            {
                for (int i = 0; i < bucket.Count; i++)
                {
                    if (bucket[i].Bounds.Contains(p) && Geometry.PointInConvex(p, bucket[i].Footprint))
                    {
                        building = bucket[i];
                        return true;
                    }
                }
            }

            building = null;
            return false;
        }

        // ---------------------------------------------------------------- roads

        /// <summary>
        /// Checks a road before building it against the network's rules (length, bend, junctions, clearance).
        /// <paramref name="control"/> is null for a straight road. Nothing is changed. Buildings in the way do not make
        /// a plan invalid: they are listed in <see cref="RoadPlan.Demolitions"/>, and <see cref="PlaceRoad"/> only
        /// builds over them if it is told to demolish them.
        /// </summary>
        public RoadPlan PlanRoad(Vec2 start, Vec2? control, Vec2 end, RoadType type = RoadType.Street)
        {
            RoadPlan plan = Roads.Plan(start, control, end, type);
            if (!plan.IsValid)
            {
                return plan;
            }

            plan.Demolitions = BuildingsOnRoad(plan.Curve, plan.Type);
            plan.Cost = Treasury.RoadCost(plan.Curve.Length, plan.Type);
            if (!Treasury.CanAfford(plan.Cost))
            {
                plan.IsValid = false;
                plan.Error = "Not enough money: this road costs " + FormatMoney(plan.Cost) + ", you have " + FormatMoney(Treasury.Balance) + ".";
            }

            return plan;
        }

        /// <summary>
        /// Builds a road from a valid plan. If buildings are in the way the road is only built when
        /// <paramref name="demolishBuildings"/> is true, and then they are demolished first. Returns the new segment,
        /// or null if nothing was built (invalid plan, or buildings in the way and not allowed to demolish them).
        /// </summary>
        /// <summary>Where the highway to the outside world enters the map (at <see cref="EdgeNode"/>) and where it meets the city (<see cref="CityNode"/>).</summary>
        public sealed class OutsideConnection
        {
            public RoadSegment Segment { get; internal set; }
            public RoadNode EdgeNode { get { return Segment.A; } }
            public RoadNode CityNode { get { return Segment.B; } }
        }

        /// <summary>Raised when traffic lights are switched on or off at an intersection.</summary>
        public event Action<RoadNode> TrafficLightsChanged;

        /// <summary>Switches the traffic lights at an intersection on or off. Does nothing (and returns false) if it is not an intersection.</summary>
        public bool SetTrafficLights(RoadNode node, bool on)
        {
            if (node == null || !node.CanHaveTrafficLights || node.TrafficLightsOff == !on)
            {
                return false;
            }

            node.TrafficLightsOff = !on;
            Revision++;
            Action<RoadNode> changed = TrafficLightsChanged;
            if (changed != null)
            {
                changed(node);
            }

            return true;
        }

        /// <summary>Raised when lane connections change at an intersection.</summary>
        public event Action<RoadNode> LaneConnectionsChanged;

        /// <summary>
        /// Allows or forbids one lane of <paramref name="from"/> to continue into <paramref name="to"/> at an intersection.
        /// Returns false with a reason in <paramref name="error"/> if that is not possible (for example it would leave the lane with no exit).
        /// </summary>
        public bool SetLaneTurn(RoadNode node, RoadSegment from, int lane, RoadSegment to, bool allowed, out string error)
        {
            error = null;
            if (node == null || from == null || to == null || !node.IsIntersection)
            {
                error = "Lane connections can only be set at intersections.";
                return false;
            }

            if (!node.IsTurnPossible(from, to) || lane < 0 || lane >= node.ArrivingLanes(from))
            {
                error = "Traffic cannot make that turn.";
                return false;
            }

            if (node.IsTurnAllowed(from, lane, to) == allowed)
            {
                return true;
            }

            if (!allowed && node.CountExits(from, lane) <= 1)
            {
                error = "Every lane needs at least one exit.";
                return false;
            }

            node.SetTurn(from.Id, lane, to.Id, allowed);
            Revision++;
            Action<RoadNode> changed = LaneConnectionsChanged;
            if (changed != null)
            {
                changed(node);
            }

            return true;
        }

        /// <summary>Lets every lane go everywhere again at this intersection.</summary>
        public void ResetLaneTurns(RoadNode node)
        {
            if (node == null || node.BlockedTurns.Count == 0)
            {
                return;
            }

            node.ClearTurns();
            Revision++;
            Action<RoadNode> changed = LaneConnectionsChanged;
            if (changed != null)
            {
                changed(node);
            }
        }

        private readonly List<OutsideConnection> outsideConnections = new List<OutsideConnection>();

        /// <summary>The highways that link the city to the outside world. They are free, cannot be bulldozed, and carry the trips in from outside later.</summary>
        public IList<OutsideConnection> OutsideConnections { get { return outsideConnections; } }

        /// <summary>
        /// Builds a highway from a random spot on the map's edge to a random spot a few hundred metres from the middle,
        /// where the player can connect streets to its end. Until there are maps with terrain, this stands in for the road
        /// that links the city to the world. It costs nothing, has no upkeep and cannot be bulldozed.
        /// Returns null if no valid route was found (it tries many).
        /// </summary>
        public OutsideConnection CreateOutsideConnection(Random random)
        {
            double half = Roads.WorldBounds.MaxX;
            double edgeInset = RoadNetwork.LotSize * 5.0 + 20.0;
            double reach = half - edgeInset;

            for (int attempt = 0; attempt < 80; attempt++)
            {
                double along = (random.NextDouble() * 2.0 - 1.0) * half * 0.6;
                Vec2 edge;
                switch (random.Next(4))
                {
                    case 0: edge = new Vec2(along, -reach); break;
                    case 1: edge = new Vec2(along, reach); break;
                    case 2: edge = new Vec2(-reach, along); break;
                    default: edge = new Vec2(reach, along); break;
                }

                double angle = random.NextDouble() * Math.PI * 2.0;
                double distance = 250.0 + random.NextDouble() * 200.0;
                Vec2 end = new Vec2(Math.Cos(angle) * distance, Math.Sin(angle) * distance);

                // A gentle bend, so it does not look ruled with a ruler.
                double length = Vec2.Distance(edge, end);
                Vec2 direction = (end - edge) * (1.0 / length);
                Vec2 normal = new Vec2(-direction.Y, direction.X);
                double sag = (random.NextDouble() * 2.0 - 1.0) * length * 0.08;
                Vec2 control = (edge + end) * 0.5 + normal * (2.0 * sag);

                RoadPlan plan = Roads.Plan(edge, control, end, RoadType.Highway);
                if (!plan.IsValid)
                {
                    continue;
                }

                RoadSegment segment = Roads.AddRoad(plan);
                if (segment == null)
                {
                    continue;
                }

                segment.IsOutsideConnection = true;
                var connection = new OutsideConnection { Segment = segment };
                outsideConnections.Add(connection);
                Treasury.Recompute();
                Revision++;
                return connection;
            }

            return null;
        }

        public RoadSegment PlaceRoad(RoadPlan plan, bool demolishBuildings = false)
        {
            if (plan == null || !plan.IsValid)
            {
                return null;
            }

            // Look again: the city may have changed since the plan was made.
            List<Building> inTheWay = BuildingsOnRoad(plan.Curve, plan.Type);
            if (inTheWay.Count > 0 && !demolishBuildings)
            {
                return null;
            }

            for (int i = 0; i < inTheWay.Count; i++)
            {
                RemoveBuilding(inTheWay[i]);
            }

            double cost = Treasury.RoadCost(plan.Curve.Length, plan.Type);
            if (!Treasury.CanAfford(cost))
            {
                return null;
            }

            RoadSegment segment = Roads.AddRoad(plan);
            if (segment != null)
            {
                Treasury.Charge(cost);
                Treasury.Recompute();
            }

            Revision++;
            return segment;
        }

        private List<Building> BuildingsOnRoad(RoadCurve curve, RoadType type)
        {
            var found = new List<Building>();
            var probe = new RoadSegment(0, null, null, curve, 1f, type);
            Aabb box = curve.Bounds.Expanded(probe.HalfWidth);
            IList<Vec2[]> surface = probe.Surface;

            foreach (Building b in buildings.Values)
            {
                if (!b.Bounds.Overlaps(box))
                {
                    continue;
                }

                for (int i = 0; i < surface.Count; i++)
                {
                    if (Geometry.ConvexOverlap(b.Footprint, surface[i], BuildingRoadTolerance))
                    {
                        found.Add(b);
                        break;
                    }
                }
            }

            return found;
        }

        private void OnSegmentSplit(RoadSegment old, RoadSegment first, RoadSegment second, double length)
        {
            Lots.Remap(old, first, second, length);
        }

        private void OnSegmentAdded(RoadSegment segment)
        {
            RefreshAround(segment);
        }

        private void OnSegmentRemoved(RoadSegment segment)
        {
            ClearRemovedLots(Lots.RemoveSegment(segment.Id));
            RefreshAround(segment);
            Revision++;
        }

        /// <summary>Re-checks the lots of every road near the segment, since roads cover (or uncover) each other's land.</summary>
        private void RefreshAround(RoadSegment segment)
        {
            double margin = LotLayout.Rows * RoadNetwork.LotSize + RoadNetwork.RoadWidth;
            foreach (RoadSegment near in Roads.SegmentsNear(segment.Curve.Bounds, margin))
            {
                ClearRemovedLots(Lots.Refresh(near));
            }

            // Where lots of different roads cover the same ground, only one of them keeps it.
            Lots.ResolveOverlaps();

            Revision++;
        }

        /// <summary>Anything that stood on a lot that no longer exists goes with it.</summary>
        private void ClearRemovedLots(List<Lot> removed)
        {
            for (int i = 0; i < removed.Count; i++)
            {
                if (removed[i].Building != null)
                {
                    RemoveBuilding(removed[i].Building);
                }
            }
        }

        // ---------------------------------------------------------------- zoning and bulldozing

        /// <summary>
        /// Zones every lot whose centre is within <paramref name="radius"/> metres of the point. Zoning over a different
        /// zone replaces it (and removes any building standing on that lot). <see cref="ZoneType.None"/> clears zoning.
        /// Returns how many lots changed.
        /// </summary>
        public int PaintZone(Vec2 center, double radius, ZoneType zone)
        {
            return SetZone(Lots.LotsInCircle(center, radius), zone);
        }

        /// <summary>
        /// Fills: zones every lot on the same side of the same stretch of road as the lot under <paramref name="point"/>
        /// (all rows, from one junction to the next). Does nothing if the point is not on a lot.
        /// Returns how many lots changed.
        /// </summary>
        public int FillZone(Vec2 point, ZoneType zone)
        {
            List<Lot> lots = LotsToFill(point);
            return lots == null ? 0 : SetZone(lots, zone);
        }

        /// <summary>The lots <see cref="FillZone"/> would zone for this point (so a preview can show them), or null if there is no lot there.</summary>
        public List<Lot> LotsToFill(Vec2 point)
        {
            Lot lot = Lots.LotAt(point);
            return lot == null ? null : Lots.LotsOfSide(lot.Key.SegmentId, lot.Key.Side);
        }

        private int SetZone(List<Lot> lots, ZoneType zone)
        {
            int changed = 0;
            foreach (Lot lot in lots)
            {
                if (lot.Zone == zone)
                {
                    continue;
                }

                if (lot.Building != null)
                {
                    RemoveBuilding(lot.Building);
                }

                lot.Zone = zone;
                changed++;
                Action<Lot> handler = ZoneChanged;
                if (handler != null)
                {
                    handler(lot);
                }
            }

            if (changed > 0)
            {
                Revision++;
            }

            return changed;
        }

        /// <summary>
        /// Clears the ground around a point: removes buildings that come within <paramref name="radius"/> metres of it and
        /// clears the zoning of lots in that circle. With <paramref name="includeRoad"/> set, a road under the point is
        /// removed too (the whole stretch between its two junctions, with everything beside it).
        /// Returns how many things (roads, buildings, lots) changed.
        /// </summary>
        public int Bulldoze(Vec2 center, double radius, bool includeRoad)
        {
            int changed = 0;

            if (includeRoad)
            {
                double length;
                RoadSegment segment = Roads.FindSegmentNear(center, RoadSpecs.MaxHalfWidth, out length);
                if (segment != null && (segment.IsOutsideConnection))
                {
                    segment = null;
                }

                if (segment != null && segment.Curve.DistanceTo(center) > segment.HalfWidth)
                {
                    segment = null;
                }

                if (segment != null && Roads.RemoveSegment(segment))
                {
                    Treasury.RefundRoad(segment);
                    Treasury.Recompute();
                    changed++;
                }
            }

            var doomed = new List<Building>();
            Aabb box = new Aabb(center.X - radius, center.Y - radius, center.X + radius, center.Y + radius);
            foreach (Building b in buildings.Values)
            {
                if (b.Bounds.Overlaps(box) && DistanceToPolygon(center, b.Footprint) <= radius)
                {
                    doomed.Add(b);
                }
            }

            for (int i = 0; i < doomed.Count; i++)
            {
                RemoveBuilding(doomed[i]);
                changed++;
            }

            changed += PaintZone(center, radius, ZoneType.None);

            if (changed > 0)
            {
                Revision++;
            }

            return changed;
        }

        private static double DistanceToPolygon(Vec2 p, Vec2[] polygon)
        {
            if (Geometry.PointInConvex(p, polygon))
            {
                return 0.0;
            }

            double best = double.MaxValue;
            for (int i = 0; i < polygon.Length; i++)
            {
                best = Math.Min(best, Geometry.DistanceToSegment(p, polygon[i], polygon[(i + 1) % polygon.Length]));
            }

            return best;
        }

        // ---------------------------------------------------------------- simulation

        /// <summary>Formats an amount of money like $1,234.</summary>
        public static string FormatMoney(double amount)
        {
            return (amount < 0 ? "-$" : "$") + Math.Abs(Math.Round(amount)).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>Advances the city by one tick.</summary>
        public void Step()
        {
            TickCount++;

            if ((TickCount - 1) % Math.Max(1, Economy.UpdateIntervalTicks) == 0)
            {
                Economy.Update();
            }

            Treasury.Tick();

            residentialCandidates.Clear();
            commercialCandidates.Clear();
            industrialCandidates.Clear();

            // Only lots on the road itself (row 0) can start a building: that is what gives it a road connection.
            foreach (Lot lot in Lots.Lots)
            {
                if (lot.Key.Row != 0 || lot.Building != null)
                {
                    continue;
                }

                switch (lot.Zone)
                {
                    case ZoneType.Residential: residentialCandidates.Add(lot); break;
                    case ZoneType.Commercial: commercialCandidates.Add(lot); break;
                    case ZoneType.Industrial: industrialCandidates.Add(lot); break;
                }
            }

            Grow(residentialCandidates, ZoneType.Residential);
            Grow(commercialCandidates, ZoneType.Commercial);
            Grow(industrialCandidates, ZoneType.Industrial);
        }

        private void Grow(List<Lot> candidates, ZoneType zone)
        {
            for (int attempt = 0; attempt < GrowthAttemptsPerZonePerTick && candidates.Count > 0; attempt++)
            {
                double chance = SpawnChance;
                if (Economy.DemandDrivesGrowth)
                {
                    chance *= Economy.DemandFor(zone);
                }

                if (rng.NextDouble() >= chance)
                {
                    continue;
                }

                int index = rng.Next(candidates.Count);
                Lot seed = candidates[index];
                candidates[index] = candidates[candidates.Count - 1];
                candidates.RemoveAt(candidates.Count - 1);

                // A bigger building placed earlier this tick may already stand here.
                if (seed.Building != null)
                {
                    continue;
                }

                GrowAt(seed, zone);
            }
        }

        /// <summary>
        /// Grows one building on a road-side lot. Templates are tried biggest first (see <see cref="BigBuildingPriority"/>);
        /// the first one that fits on the lots around the seed is built, and 1x1 buildings are the last resort.
        /// </summary>
        private void GrowAt(Lot seed, ZoneType zone)
        {
            List<BuildingTemplate> order = TemplatesBiggestFirst(zone);
            for (int i = 0; i < order.Count; i++)
            {
                BuildingTemplate template = order[i];
                if (template.CellCount > 1 && rng.NextDouble() >= BigBuildingPriority)
                {
                    continue;
                }

                if (TryPlace(template, seed))
                {
                    return;
                }
            }

            // Every 1x1 template fits anywhere the seed lot itself is free, but the priority roll may have skipped them all.
            TryPlace(BuildingCatalog.Pick(zone, rng, true), seed);
        }

        private List<BuildingTemplate> TemplatesBiggestFirst(ZoneType zone)
        {
            IList<BuildingTemplate> templates = BuildingCatalog.ForZone(zone);
            var entries = new List<KeyValuePair<double, BuildingTemplate>>(templates.Count);

            foreach (BuildingTemplate t in templates)
            {
                // Same-size templates are put in a random order, favouring the common ones.
                double tie = -Math.Log(Math.Max(1e-9, rng.NextDouble())) / t.Weight;
                entries.Add(new KeyValuePair<double, BuildingTemplate>(-t.CellCount * 1000.0 + tie, t));
            }

            entries.Sort((a, b) => a.Key.CompareTo(b.Key));

            var result = new List<BuildingTemplate>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                result.Add(entries[i].Value);
            }

            return result;
        }

        /// <summary>
        /// Tries every position along the road that covers the seed lot with this template and builds on a
        /// random one that fits (zoned, free, clear of roads and other buildings, and touching the road).
        /// </summary>
        private bool TryPlace(BuildingTemplate template, Lot seed)
        {
            RoadSegment segment = Roads.GetSegment(seed.Key.SegmentId);
            if (segment == null)
            {
                return false;
            }

            List<Lot> bestLots = null;
            Vec2 bestCenter = Vec2.Zero;
            Vec2 bestBack = Vec2.Zero;
            int fits = 0;

            for (int col = seed.Key.Col - template.Width + 1; col <= seed.Key.Col; col++)
            {
                if (col < 0)
                {
                    continue;
                }

                List<Lot> lots = CollectLots(seed.Key, col, template);
                if (lots == null)
                {
                    continue;
                }

                Vec2 center;
                Vec2 back;
                if (!Place(segment, seed.Key.Side, col, template, out center, out back))
                {
                    continue;
                }

                fits++;
                if (rng.Next(fits) == 0)
                {
                    bestLots = lots;
                    bestCenter = center;
                    bestBack = back;
                }
            }

            if (bestLots == null)
            {
                return false;
            }

            SpawnBuilding(template, bestCenter, bestBack, bestLots);
            return true;
        }

        /// <summary>The lots a template would stand on, or null if any is missing, taken or zoned for something else.</summary>
        private List<Lot> CollectLots(LotKey seed, int firstCol, BuildingTemplate template)
        {
            var lots = new List<Lot>(template.CellCount);
            for (int row = 0; row < template.Depth; row++)
            {
                for (int i = 0; i < template.Width; i++)
                {
                    Lot lot = Lots.Get(new LotKey(seed.SegmentId, seed.Side, firstCol + i, row));
                    if (lot == null || lot.Building != null || lot.Zone != template.Zone)
                    {
                        return null;
                    }

                    lots.Add(lot);
                }
            }

            return lots;
        }

        /// <summary>
        /// Works out where the building would stand: centred over its lots, front edge moved to just touch the
        /// road, and checked against roads, other buildings and the edge of the map.
        /// </summary>
        private bool Place(RoadSegment segment, int side, int firstCol, BuildingTemplate template, out Vec2 center, out Vec2 back)
        {
            double widthM = template.Width * RoadNetwork.LotSize;
            double depthM = template.Depth * RoadNetwork.LotSize;
            double sMid = (firstCol + template.Width * 0.5) * RoadNetwork.LotSize;

            back = segment.Curve.NormalAtLength(sMid) * (side == 0 ? 1.0 : -1.0);
            center = segment.Curve.PointAtLength(sMid) + back * (RoadNetwork.HalfWidth + depthM * 0.5);

            // Straight edges cut across bends, so measure the real gap between the front edge and the road and close it.
            Vec2[] footprint = Building.MakeFootprint(center, back, widthM, depthM);
            double nearest = double.MaxValue;
            const int samples = 8;
            for (int k = 0; k <= samples; k++)
            {
                Vec2 p = Vec2.Lerp(footprint[0], footprint[1], k / (double)samples);
                nearest = Math.Min(nearest, segment.Curve.DistanceTo(p));
            }

            double shift = RoadNetwork.HalfWidth - nearest;
            if (Math.Abs(shift) > RoadContactTolerance)
            {
                return false;
            }

            center = center + back * shift;
            footprint = Building.MakeFootprint(center, back, widthM, depthM);

            Aabb inner = Roads.WorldBounds.Expanded(-RoadNetwork.LotSize);
            for (int i = 0; i < footprint.Length; i++)
            {
                if (!inner.Contains(footprint[i]))
                {
                    return false;
                }
            }

            return !OverlapsRoad(footprint) && !OverlapsBuilding(footprint);
        }

        private bool OverlapsRoad(Vec2[] footprint)
        {
            Aabb box = Aabb.Of(footprint);
            foreach (RoadSegment segment in Roads.SegmentsNear(box, 0.0))
            {
                IList<Vec2[]> surface = segment.Surface;
                for (int i = 0; i < surface.Count; i++)
                {
                    if (Aabb.Of(surface[i]).Overlaps(box) && Geometry.ConvexOverlap(footprint, surface[i], BuildingRoadTolerance))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool OverlapsBuilding(Vec2[] footprint)
        {
            Aabb box = Aabb.Of(footprint);
            for (int cx = CellOf(box.MinX); cx <= CellOf(box.MaxX); cx++)
            {
                for (int cy = CellOf(box.MinY); cy <= CellOf(box.MaxY); cy++)
                {
                    List<Building> bucket;
                    if (!buildingHash.TryGetValue(HashKey(cx, cy), out bucket))
                    {
                        continue;
                    }

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        if (bucket[i].Bounds.Overlaps(box) && Geometry.ConvexOverlap(footprint, bucket[i].Footprint, BuildingOverlapTolerance))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private void SpawnBuilding(BuildingTemplate template, Vec2 center, Vec2 back, List<Lot> lots)
        {
            int floors = rng.Next(template.MinFloors, template.MaxFloors + 1);
            int variant = rng.Next();
            AddBuilding(new Building(nextBuildingId++, template, floors, variant, center, back, lots));
        }

        private void AddBuilding(Building building)
        {
            IList<Lot> lots = building.Lots;
            buildings[building.Id] = building;
            for (int i = 0; i < lots.Count; i++)
            {
                lots[i].Building = building;
            }

            HashBuilding(building, true);
            Revision++;

            Action<Building> spawned = BuildingSpawned;
            if (spawned != null)
            {
                spawned(building);
            }
        }

        private void RemoveBuilding(Building building)
        {
            if (!buildings.Remove(building.Id))
            {
                return;
            }

            for (int i = 0; i < building.Lots.Count; i++)
            {
                if (building.Lots[i].Building == building)
                {
                    building.Lots[i].Building = null;
                }
            }

            HashBuilding(building, false);
            Revision++;

            Action<Building> removed = BuildingRemoved;
            if (removed != null)
            {
                removed(building);
            }
        }

        // ---------------------------------------------------------------- spatial index for buildings

        private void HashBuilding(Building b, bool add)
        {
            for (int cx = CellOf(b.Bounds.MinX); cx <= CellOf(b.Bounds.MaxX); cx++)
            {
                for (int cy = CellOf(b.Bounds.MinY); cy <= CellOf(b.Bounds.MaxY); cy++)
                {
                    long key = HashKey(cx, cy);
                    List<Building> bucket;
                    if (add)
                    {
                        if (!buildingHash.TryGetValue(key, out bucket))
                        {
                            bucket = new List<Building>();
                            buildingHash[key] = bucket;
                        }

                        bucket.Add(b);
                    }
                    else if (buildingHash.TryGetValue(key, out bucket))
                    {
                        bucket.Remove(b);
                    }
                }
            }
        }

        private static int CellOf(double v) { return (int)Math.Floor(v / HashCell); }
        private static long HashKey(int cx, int cy) { return ((long)cx << 32) ^ (uint)cy; }
    }
}
