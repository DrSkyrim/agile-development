using System;
using System.Collections.Generic;

namespace UrbanGenesis.Simulation
{
    /// <summary>Things a population milestone unlocks.</summary>
    public enum Feature
    {
        Hospital,
        PoliceStation,
        FireStation,
        DenseResidential,
        School
    }

    public enum UtilityKind
    {
        Power,
        Water
    }

    /// <summary>A river across the map. Roads and lots keep out of its banks; water towers and sewage outlets must stand near it.</summary>
    public sealed class River
    {
        public List<Vec2> Points = new List<Vec2>();

        /// <summary>Half the width of the water, metres.</summary>
        public double HalfWidth = 20.0;

        /// <summary>Roads may not come closer than this to the middle of the river (the banks).</summary>
        public double BlockRadius = 45.0;

        /// <summary>Water towers and sewage outlets must have their middle within this distance of the middle of the river.</summary>
        public double IntakeRadius = 85.0;

        public double DistanceTo(Vec2 p)
        {
            double best = double.MaxValue;
            for (int i = 0; i + 1 < Points.Count; i++)
            {
                best = Math.Min(best, DistanceToSegment(p, Points[i], Points[i + 1]));
            }

            return best;
        }

        /// <summary>The point on the river's middle line that is closest to <paramref name="p"/>.</summary>
        public Vec2 NearestPoint(Vec2 p)
        {
            double best = double.MaxValue;
            Vec2 result = p;
            for (int i = 0; i + 1 < Points.Count; i++)
            {
                Vec2 a = Points[i];
                Vec2 ab = Points[i + 1] - a;
                double len2 = ab.LengthSquared;
                double t = len2 < 1e-9 ? 0.0 : Math.Max(0.0, Math.Min(1.0, Vec2.Dot(p - a, ab) / len2));
                Vec2 q = a + ab * t;
                double d = Vec2.Distance(p, q);
                if (d < best) { best = d; result = q; }
            }

            return result;
        }

        private static double DistanceToSegment(Vec2 p, Vec2 a, Vec2 b)
        {
            Vec2 ab = b - a;
            double len2 = ab.LengthSquared;
            double t = len2 < 1e-9 ? 0.0 : Math.Max(0.0, Math.Min(1.0, Vec2.Dot(p - a, ab) / len2));
            return Vec2.Distance(p, a + ab * t);
        }
    }

    /// <summary>The population milestones and what each one unlocks.</summary>
    public static class CityMilestones
    {
        public static readonly int[] Targets = { 100, 1000, 5000, 10000, 25000, 50000, 100000 };

        public static Feature[] Unlocks(int index)
        {
            switch (index)
            {
                case 0: return new[] { Feature.Hospital };
                case 1: return new[] { Feature.PoliceStation };
                case 2: return new[] { Feature.FireStation, Feature.DenseResidential };
                case 3: return new[] { Feature.School };
                default: return new Feature[0];
            }
        }

        public static string Name(Feature feature)
        {
            switch (feature)
            {
                case Feature.Hospital: return "hospital";
                case Feature.PoliceStation: return "police station";
                case Feature.FireStation: return "fire station";
                case Feature.DenseResidential: return "dense residential zone";
                default: return "school";
            }
        }

        /// <summary>"unlocks the hospital", or an empty string for milestones without a reward.</summary>
        public static string Reward(int index)
        {
            Feature[] unlocks = Unlocks(index);
            if (unlocks.Length == 0) { return ""; }
            var names = new List<string>();
            foreach (Feature f in unlocks) { names.Add(Name(f)); }
            return "unlocks the " + string.Join(" and the ", names.ToArray());
        }
    }

    /// <summary>Facts about the buildings the player places: names, prices and rules.</summary>
    public static class CivicInfo
    {
        public static readonly BuildingShape[] All =
        {
            BuildingShape.PowerPlant, BuildingShape.WaterTower, BuildingShape.SewageOutlet,
            BuildingShape.Hospital, BuildingShape.PoliceStation, BuildingShape.FireStation, BuildingShape.School
        };

        public static bool IsCivic(BuildingShape shape)
        {
            return Array.IndexOf(All, shape) >= 0;
        }

        public static string Name(BuildingShape shape)
        {
            switch (shape)
            {
                case BuildingShape.PowerPlant: return "Power plant";
                case BuildingShape.WaterTower: return "Water tower";
                case BuildingShape.SewageOutlet: return "Sewage outlet";
                case BuildingShape.Hospital: return "Hospital";
                case BuildingShape.PoliceStation: return "Police station";
                case BuildingShape.FireStation: return "Fire station";
                case BuildingShape.School: return "School";
                default: return shape.ToString();
            }
        }

        public static double Cost(BuildingShape shape)
        {
            switch (shape)
            {
                case BuildingShape.PowerPlant: return 15000.0;
                case BuildingShape.WaterTower: return 6000.0;
                case BuildingShape.SewageOutlet: return 5000.0;
                case BuildingShape.Hospital: return 12000.0;
                case BuildingShape.PoliceStation: return 8000.0;
                case BuildingShape.FireStation: return 8000.0;
                case BuildingShape.School: return 10000.0;
                default: return 0.0;
            }
        }

        public static string Description(BuildingShape shape)
        {
            switch (shape)
            {
                case BuildingShape.PowerPlant: return "Feeds every road that has a power cable connected to it.";
                case BuildingShape.WaterTower: return "Supplies water to every road a connected pipe runs across. Stands near the river, no road needed.";
                case BuildingShape.SewageOutlet: return "Takes the sewage away through the same pipes. Stands near the river, no road needed.";
                case BuildingShape.Hospital: return "Ambulances drive out to the homes.";
                case BuildingShape.PoliceStation: return "Police cars patrol the streets.";
                case BuildingShape.FireStation: return "Puts out fires. Without one, burning buildings are lost.";
                case BuildingShape.School: return "School buses collect the children.";
                default: return "";
            }
        }

        /// <summary>True for buildings that stand anywhere near the river instead of on a lot beside a road.</summary>
        public static bool IsFreestanding(BuildingShape shape)
        {
            return shape == BuildingShape.WaterTower || shape == BuildingShape.SewageOutlet;
        }

        public static bool NeedsRiver(BuildingShape shape)
        {
            return shape == BuildingShape.WaterTower || shape == BuildingShape.SewageOutlet;
        }

        public static Feature? RequiredFeature(BuildingShape shape)
        {
            switch (shape)
            {
                case BuildingShape.Hospital: return Feature.Hospital;
                case BuildingShape.PoliceStation: return Feature.PoliceStation;
                case BuildingShape.FireStation: return Feature.FireStation;
                case BuildingShape.School: return Feature.School;
                default: return null;
            }
        }
    }

    public sealed class FacilityPlan
    {
        public bool IsValid;
        public string Error = "";
        public double Cost;
        public BuildingTemplate Template;
        public Vec2 Center;
        public Vec2 Back;
        public Vec2[] Footprint;
        internal List<Lot> Lots;
    }

    /// <summary>A water and sewage pipe: a straight underground run between two points, free of roads.</summary>
    public sealed class WaterPipe
    {
        public int Id { get; internal set; }
        public Vec2 A { get; internal set; }
        public Vec2 B { get; internal set; }
        public double Length { get { return Vec2.Distance(A, B); } }
        public Aabb Bounds { get { return new Aabb(Math.Min(A.X, B.X), Math.Min(A.Y, B.Y), Math.Max(A.X, B.X), Math.Max(A.Y, B.Y)); } }
    }

    public sealed class PipePlan
    {
        public bool IsValid;
        public string Error = "";
        public double Cost;
        public double Length;
        public Vec2 A;
        public Vec2 B;

        /// <summary>The roads the pipe runs under; each is closed for the roadworks.</summary>
        public List<RoadSegment> Crossed = new List<RoadSegment>();
    }

    public sealed class UtilityPlan
    {
        public bool IsValid;
        public string Error = "";
        public double Cost;
        public RoadSegment Segment;
        public UtilityKind Kind;
    }

    public sealed partial class CitySimulation
    {
        // ------------------------------------------------------------ milestones and unlocks

        /// <summary>How many population milestones have been reached (they stay reached when the population falls again).</summary>
        public int MilestonesReached { get; internal set; }

        public bool IsUnlocked(Feature feature)
        {
            for (int i = 0; i < MilestonesReached; i++)
            {
                foreach (Feature f in CityMilestones.Unlocks(i))
                {
                    if (f == feature) { return true; }
                }
            }

            return false;
        }

        public bool IsUnlocked(BuildingShape civicShape)
        {
            Feature? required = CivicInfo.RequiredFeature(civicShape);
            return !required.HasValue || IsUnlocked(required.Value);
        }

        /// <summary>The number of people living in the city's homes.</summary>
        public int Population
        {
            get
            {
                int total = 0;
                foreach (Building b in buildings.Values) { total += b.Residents; }
                return total;
            }
        }

        private void CheckMilestones()
        {
            if (MilestonesReached >= CityMilestones.Targets.Length || TickCount % 4 != 0)
            {
                return;
            }

            int population = Population;
            while (MilestonesReached < CityMilestones.Targets.Length && population >= CityMilestones.Targets[MilestonesReached])
            {
                int index = MilestonesReached;
                MilestonesReached++;
                string reward = CityMilestones.Reward(index);
                string text = "Milestone: " + CityMilestones.Targets[index].ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " residents!"
                    + (reward.Length > 0 ? " This " + reward + "." : "");
                if (index == 0 && RequireUtilities)
                {
                    text += " From now on new buildings need power, water and sewage.";
                }

                RaiseEvent(text);
            }
        }

        // ------------------------------------------------------------ river

        public River River { get; private set; }

        public void SetRiver(River river)
        {
            River = river;
            if (river == null)
            {
                Roads.IsBlocked = null;
            }
            else
            {
                Roads.IsBlocked = p => river.DistanceTo(p) < river.BlockRadius;
            }

            Revision++;
        }

        /// <summary>
        /// Lays a winding river across the map, clear of the roads that exist (the highway from the outside).
        /// Tries a number of times; returns false (and leaves the map without a river) if nothing fits.
        /// </summary>
        public bool CreateRiver(Random random)
        {
            double half = Roads.WorldBounds.MaxX;
            for (int attempt = 0; attempt < 60; attempt++)
            {
                double angle = random.NextDouble() * Math.PI;
                var direction = new Vec2(Math.Cos(angle), Math.Sin(angle));
                var normal = new Vec2(-direction.Y, direction.X);
                double offset = (300.0 + random.NextDouble() * half * 0.4) * (random.Next(2) == 0 ? -1.0 : 1.0);
                double amplitude = 50.0 + random.NextDouble() * 90.0;
                double wave = 0.0015 + random.NextDouble() * 0.002;
                double phase = random.NextDouble() * Math.PI * 2.0;

                var river = new River();
                for (double t = -half * 1.5; t <= half * 1.5; t += 40.0)
                {
                    Vec2 p = direction * t + normal * (offset + amplitude * Math.Sin(t * wave + phase));
                    if (Math.Abs(p.X) <= half && Math.Abs(p.Y) <= half) { river.Points.Add(p); }
                }

                if (river.Points.Count < 8) { continue; }

                bool clear = true;
                foreach (RoadSegment segment in Roads.Segments)
                {
                    foreach (Vec2 q in segment.Curve.Sample(20.0))
                    {
                        if (river.DistanceTo(q) < river.BlockRadius + 150.0) { clear = false; break; }
                    }

                    if (!clear) { break; }
                }

                if (clear)
                {
                    SetRiver(river);
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------ facilities (buildings the player places)

        /// <summary>Checks whether a civic building of this kind can stand at the point, and what it would cost.</summary>
        public FacilityPlan PlanFacility(BuildingShape shape, Vec2 point)
        {
            var plan = new FacilityPlan { Template = BuildingCatalog.CivicTemplate(shape) };
            if (plan.Template == null)
            {
                plan.Error = "Unknown building.";
                return plan;
            }

            plan.Cost = CivicInfo.Cost(shape);
            if (!IsUnlocked(shape))
            {
                Feature f = CivicInfo.RequiredFeature(shape).Value;
                plan.Error = "Locked: reach the milestone that unlocks the " + CityMilestones.Name(f) + ".";
                return plan;
            }

            if (CivicInfo.IsFreestanding(shape))
            {
                return PlanFreestanding(plan, shape, point);
            }

            Lot lot = Lots.LotAt(point);
            if (lot == null)
            {
                plan.Error = "Point at the land beside a road.";
                return plan;
            }

            RoadSegment segment = Roads.GetSegment(lot.Key.SegmentId);
            BuildingTemplate template = plan.Template;
            int firstCol = lot.Key.Col - (template.Width - 1) / 2;
            if (firstCol < 0) { firstCol = 0; }

            var seed = new LotKey(lot.Key.SegmentId, lot.Key.Side, lot.Key.Col, 0);
            List<Lot> lots = CollectLots(seed, firstCol, template);
            Vec2 center;
            Vec2 back;
            if (lots == null || segment == null || !Place(segment, lot.Key.Side, firstCol, template, out center, out back))
            {
                plan.Error = "No room: it needs a free " + template.Width + "x" + template.Depth + " lot area (" + template.Width * RoadNetwork.LotSize + " x "
                    + template.Depth * RoadNetwork.LotSize + " m) with its front on a road.";
                return plan;
            }

            plan.Center = center;
            plan.Back = back;
            plan.Footprint = Building.MakeFootprint(center, back, template.Width * RoadNetwork.LotSize, template.Depth * RoadNetwork.LotSize);
            plan.Lots = lots;

            if (!Treasury.CanAfford(plan.Cost))
            {
                plan.Error = "Not enough money: " + FormatMoney(plan.Cost) + " needed.";
                return plan;
            }

            plan.IsValid = true;
            return plan;
        }

        /// <summary>A water tower or sewage outlet: it needs open ground near the river, not a lot beside a road.</summary>
        private FacilityPlan PlanFreestanding(FacilityPlan plan, BuildingShape shape, Vec2 point)
        {
            if (River == null)
            {
                plan.Error = "This map has no river.";
                return plan;
            }

            BuildingTemplate template = plan.Template;
            Vec2 nearest = River.NearestPoint(point);
            Vec2 away = point - nearest;
            Vec2 back = away.LengthSquared < 1e-6 ? new Vec2(0.0, 1.0) : away.Normalized;
            plan.Center = point;
            plan.Back = back;
            plan.Footprint = Building.MakeFootprint(point, back, template.Width * RoadNetwork.LotSize, template.Depth * RoadNetwork.LotSize);
            plan.Lots = new List<Lot>();

            double half = Roads.WorldBounds.MaxX;
            foreach (Vec2 corner in plan.Footprint)
            {
                if (Math.Abs(corner.X) > half || Math.Abs(corner.Y) > half)
                {
                    plan.Error = "That is off the edge of the map.";
                    return plan;
                }

                if (River.DistanceTo(corner) < River.HalfWidth + 1.0)
                {
                    plan.Error = "That is in the water: build on the bank.";
                    return plan;
                }
            }

            if (River.DistanceTo(point) > River.IntakeRadius)
            {
                plan.Error = "Too far from the river: a " + CivicInfo.Name(shape).ToLowerInvariant() + " must stand within " + (int)River.IntakeRadius + " m of it.";
                return plan;
            }

            if (OverlapsRoad(plan.Footprint) || OverlapsBuilding(plan.Footprint))
            {
                plan.Error = "No room: something is in the way.";
                return plan;
            }

            if (!Treasury.CanAfford(plan.Cost))
            {
                plan.Error = "Not enough money: " + FormatMoney(plan.Cost) + " needed.";
                return plan;
            }

            plan.IsValid = true;
            return plan;
        }

        public Building PlaceFacility(FacilityPlan plan)
        {
            if (plan == null || !plan.IsValid) { return null; }
            Treasury.Charge(plan.Cost);
            var building = new Building(nextBuildingId++, plan.Template, 1, rng.Next(), plan.Center, plan.Back, plan.Lots);
            AddBuilding(building);
            servicesDirty = true;
            return building;
        }

        public bool HasFacility(BuildingShape shape)
        {
            foreach (Building b in buildings.Values)
            {
                if (b.Shape == shape) { return true; }
            }

            return false;
        }

        public List<Building> FacilitiesOf(BuildingShape shape)
        {
            var list = new List<Building>();
            foreach (Building b in buildings.Values)
            {
                if (b.Shape == shape) { list.Add(b); }
            }

            return list;
        }

        // ------------------------------------------------------------ cables (along roads) and pipes (anywhere)

        public const int UtilityWorkDays = 3;

        /// <summary>Two pipes closer together than this (crossing, touching, ends meeting) are joined.</summary>
        public const double PipeConnectRadius = 4.0;

        /// <summary>A pipe end clicked this close to another pipe snaps onto it.</summary>
        public const double PipeSnapRadius = 12.0;

        /// <summary>A pipe feeds a road if it runs under it or ends this close beside it.</summary>
        public const double PipeReach = 8.0;

        /// <summary>Clicking with the bulldozer this close to a pipe digs it up.</summary>
        public const double BulldozePipeRadius = 4.0;

        public const double MinPipeLength = 8.0;
        public const double MaxPipeLength = 600.0;

        private readonly Dictionary<int, int> worksUntil = new Dictionary<int, int>();
        private readonly List<WaterPipe> pipes = new List<WaterPipe>();
        private int nextPipeId = 1;

        public bool HasRoadworks(RoadSegment segment)
        {
            return worksUntil.ContainsKey(segment.Id);
        }

        public IEnumerable<WaterPipe> Pipes { get { return pipes; } }

        public int PipeCount { get { return pipes.Count; } }

        /// <summary>
        /// Closes the road for <see cref="UtilityWorkDays"/> days from now. If crews are already digging there,
        /// the closure is simply extended, so a cable laid on top of a pipe costs only the extra days.
        /// </summary>
        private void StartRoadworks(RoadSegment segment)
        {
            int end = TickCount + UtilityWorkDays * GameTime.TicksPerDay;
            int existing;
            if (worksUntil.TryGetValue(segment.Id, out existing) && existing > end) { end = existing; }
            worksUntil[segment.Id] = end;
            segment.OpenFraction = 0f;
        }

        public static bool HasLine(RoadSegment segment, UtilityKind kind)
        {
            return kind == UtilityKind.Power && segment.HasPower;
        }

        /// <summary>Checks a power cable along a road. (Water pipes are free of roads: see <see cref="PlanPipe"/>.)</summary>
        public UtilityPlan PlanUtility(RoadSegment segment, UtilityKind kind)
        {
            var plan = new UtilityPlan { Segment = segment, Kind = kind };
            if (kind != UtilityKind.Power)
            {
                plan.Error = "Water pipes are drawn across the land, not along a road.";
                return plan;
            }

            if (segment.IsOutsideConnection || segment.HasLots == false)
            {
                plan.Error = "Highways carry no power cables: lay them along ordinary roads.";
                return plan;
            }

            if (segment.HasPower)
            {
                plan.Error = "This road already has a power cable.";
                return plan;
            }

            if (segment.IsClosed && !HasRoadworks(segment))
            {
                plan.Error = "The road is closed right now. Try again when it reopens.";
                return plan;
            }

            plan.Cost = Treasury.UtilityCost(segment.Length, kind);
            if (!Treasury.CanAfford(plan.Cost))
            {
                plan.Error = "Not enough money: " + FormatMoney(plan.Cost) + " needed.";
                return plan;
            }

            plan.IsValid = true;
            return plan;
        }

        /// <summary>Lays a power cable and closes the road for <see cref="UtilityWorkDays"/> days while the crews dig. Returns an error message, or null.</summary>
        public string LayUtility(RoadSegment segment, UtilityKind kind)
        {
            UtilityPlan plan = PlanUtility(segment, kind);
            if (!plan.IsValid) { return plan.Error; }

            Treasury.Charge(plan.Cost);
            segment.HasPower = true;
            StartRoadworks(segment);
            servicesDirty = true;
            Revision++;
            return null;
        }

        /// <summary>The pipe end or pipe body nearest to the point if there is one within <see cref="PipeSnapRadius"/>, otherwise the point itself.</summary>
        public Vec2 SnapPipePoint(Vec2 p)
        {
            Vec2 best = p;
            double bestDistance = PipeSnapRadius;
            bool snapped = false;
            foreach (WaterPipe pipe in pipes)
            {
                double d = Vec2.Distance(p, pipe.A);
                if (d < bestDistance) { bestDistance = d; best = pipe.A; snapped = true; }
                d = Vec2.Distance(p, pipe.B);
                if (d < bestDistance) { bestDistance = d; best = pipe.B; snapped = true; }
            }

            if (snapped) { return best; }

            foreach (WaterPipe pipe in pipes)
            {
                double u;
                double d = Geometry.DistanceToSegment(p, pipe.A, pipe.B, out u);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = pipe.A + (pipe.B - pipe.A) * u;
                }
            }

            return best;
        }

        /// <summary>Checks a pipe between two points: its length, cost, and the roads it would run under (those get closed).</summary>
        public PipePlan PlanPipe(Vec2 from, Vec2 to)
        {
            var plan = new PipePlan { A = SnapPipePoint(from) };
            plan.B = SnapPipePoint(to);
            plan.Length = Vec2.Distance(plan.A, plan.B);

            double half = Roads.WorldBounds.MaxX;
            if (Math.Abs(plan.A.X) > half || Math.Abs(plan.A.Y) > half || Math.Abs(plan.B.X) > half || Math.Abs(plan.B.Y) > half)
            {
                plan.Error = "That is off the edge of the map.";
                return plan;
            }

            if (plan.Length < MinPipeLength)
            {
                plan.Error = "Too short: pick a point further away.";
                return plan;
            }

            if (plan.Length > MaxPipeLength)
            {
                plan.Error = "Too long: a pipe run can be at most " + (int)MaxPipeLength + " m. Lay it in pieces.";
                return plan;
            }

            plan.Crossed = RoadsAbove(plan.A, plan.B);
            foreach (RoadSegment road in plan.Crossed)
            {
                if (road.IsClosed && !HasRoadworks(road))
                {
                    plan.Error = "A road on the way is closed right now. Try again when it reopens.";
                    return plan;
                }
            }

            plan.Cost = Treasury.UtilityCost(plan.Length, UtilityKind.Water);
            if (!Treasury.CanAfford(plan.Cost))
            {
                plan.Error = "Not enough money: " + FormatMoney(plan.Cost) + " needed.";
                return plan;
            }

            plan.IsValid = true;
            return plan;
        }

        /// <summary>Lays the pipe; every road it runs under is closed for the roadworks. Returns an error message, or null.</summary>
        public string LayPipe(Vec2 from, Vec2 to)
        {
            PipePlan plan = PlanPipe(from, to);
            if (!plan.IsValid) { return plan.Error; }

            Treasury.Charge(plan.Cost);
            pipes.Add(new WaterPipe { Id = nextPipeId++, A = plan.A, B = plan.B });
            foreach (RoadSegment road in plan.Crossed) { StartRoadworks(road); }
            servicesDirty = true;
            Revision++;
            return null;
        }

        /// <summary>Roads whose surface the straight line a-b passes over.</summary>
        public List<RoadSegment> RoadsAbove(Vec2 a, Vec2 b)
        {
            var found = new List<RoadSegment>();
            var box = new Aabb(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
            foreach (RoadSegment road in Roads.SegmentsNear(box, RoadSpecs.MaxHalfWidth))
            {
                if (DistanceToRoad(a, b, road) <= road.HalfWidth) { found.Add(road); }
            }

            return found;
        }

        private static double DistanceToRoad(Vec2 a, Vec2 b, RoadSegment road)
        {
            List<Vec2> line = road.Curve.Sample(4.0);
            double best = double.MaxValue;
            for (int i = 0; i + 1 < line.Count; i++)
            {
                best = Math.Min(best, Geometry.SegmentDistance(a, b, line[i], line[i + 1]));
            }

            return best;
        }

        public WaterPipe PipeNear(Vec2 p, double radius)
        {
            WaterPipe best = null;
            double bestDistance = radius;
            foreach (WaterPipe pipe in pipes)
            {
                double d = Geometry.DistanceToSegment(p, pipe.A, pipe.B);
                if (d <= bestDistance) { bestDistance = d; best = pipe; }
            }

            return best;
        }

        /// <summary>Digs the pipe up again. Nothing is refunded and no road is closed.</summary>
        public bool RemovePipe(WaterPipe pipe)
        {
            if (pipe == null || !pipes.Remove(pipe)) { return false; }
            servicesDirty = true;
            Revision++;
            return true;
        }

        /// <summary>True if a pipe touches the (water tower or sewage outlet) building.</summary>
        public bool IsConnectedToPipes(Building b)
        {
            foreach (WaterPipe pipe in pipes)
            {
                if (PipeTouches(pipe, b)) { return true; }
            }

            return false;
        }

        private static bool PipeTouches(WaterPipe pipe, Building b)
        {
            if (Geometry.PointInConvex(pipe.A, b.Footprint) || Geometry.PointInConvex(pipe.B, b.Footprint)) { return true; }
            Vec2[] f = b.Footprint;
            for (int i = 0; i < f.Length; i++)
            {
                if (Geometry.SegmentDistance(pipe.A, pipe.B, f[i], f[(i + 1) % f.Length]) <= PipeConnectRadius) { return true; }
            }

            return false;
        }

        // ------------------------------------------------------------ who is served

        /// <summary>If true, buildings only grow and earn where power, water and sewage reach them (once the first milestone is passed).</summary>
        public bool RequireUtilities { get; set; }

        public bool UtilitiesRequired { get { return RequireUtilities && MilestonesReached >= 1; } }

        private bool servicesDirty = true;
        private readonly HashSet<int> poweredSegments = new HashSet<int>();
        private readonly HashSet<int> wateredSegments = new HashSet<int>();
        private readonly HashSet<int> sewerSegments = new HashSet<int>();

        internal void RecomputeServices()
        {
            servicesDirty = false;
            FloodPower();

            // Pipes that touch each other form one network; a network feeds what its tower or outlet is joined to.
            int n = pipes.Count;
            var next = new List<int>[n];
            for (int i = 0; i < n; i++) { next[i] = new List<int>(); }
            for (int i = 0; i < n; i++)
            {
                Aabb box = pipes[i].Bounds.Expanded(PipeConnectRadius);
                for (int j = i + 1; j < n; j++)
                {
                    if (box.Overlaps(pipes[j].Bounds) && Geometry.SegmentDistance(pipes[i].A, pipes[i].B, pipes[j].A, pipes[j].B) <= PipeConnectRadius)
                    {
                        next[i].Add(j);
                        next[j].Add(i);
                    }
                }
            }

            FloodPipes(BuildingShape.WaterTower, next, wateredSegments);
            FloodPipes(BuildingShape.SewageOutlet, next, sewerSegments);
        }

        private void FloodPower()
        {
            poweredSegments.Clear();
            var queue = new Queue<RoadSegment>();
            foreach (Building b in buildings.Values)
            {
                if (b.Shape != BuildingShape.PowerPlant || b.Lots.Count == 0) { continue; }
                RoadSegment host = Roads.GetSegment(b.Lots[0].Key.SegmentId);
                if (host != null && host.HasPower && poweredSegments.Add(host.Id)) { queue.Enqueue(host); }
            }

            while (queue.Count > 0)
            {
                RoadSegment s = queue.Dequeue();
                RoadNode[] ends = { s.A, s.B };
                for (int e = 0; e < 2; e++)
                {
                    foreach (RoadSegment other in ends[e].Segments)
                    {
                        if (other.HasPower && poweredSegments.Add(other.Id)) { queue.Enqueue(other); }
                    }
                }
            }
        }

        private void FloodPipes(BuildingShape source, List<int>[] next, HashSet<int> servedSegments)
        {
            servedSegments.Clear();
            var reached = new bool[pipes.Count];
            var queue = new Queue<int>();
            foreach (Building b in buildings.Values)
            {
                if (b.Shape != source) { continue; }
                for (int i = 0; i < pipes.Count; i++)
                {
                    if (!reached[i] && PipeTouches(pipes[i], b)) { reached[i] = true; queue.Enqueue(i); }
                }
            }

            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                foreach (int j in next[i])
                {
                    if (!reached[j]) { reached[j] = true; queue.Enqueue(j); }
                }
            }

            for (int i = 0; i < pipes.Count; i++)
            {
                if (!reached[i]) { continue; }
                foreach (RoadSegment road in Roads.SegmentsNear(pipes[i].Bounds, RoadSpecs.MaxHalfWidth + PipeReach))
                {
                    if (road.HasLots && DistanceToRoad(pipes[i].A, pipes[i].B, road) <= road.HalfWidth + PipeReach)
                    {
                        servedSegments.Add(road.Id);
                    }
                }
            }
        }

        private void EnsureServices()
        {
            if (servicesDirty) { RecomputeServices(); }
        }

        public bool SegmentHasPower(int segmentId) { EnsureServices(); return poweredSegments.Contains(segmentId); }
        public bool SegmentHasWater(int segmentId) { EnsureServices(); return wateredSegments.Contains(segmentId); }
        public bool SegmentHasSewage(int segmentId) { EnsureServices(); return sewerSegments.Contains(segmentId); }

        public bool SegmentServed(int segmentId)
        {
            return SegmentHasPower(segmentId) && SegmentHasWater(segmentId) && SegmentHasSewage(segmentId);
        }

        /// <summary>True if the building gets power, water and sewage (always true while utilities are not required).</summary>
        public bool IsServed(Building b)
        {
            if (!UtilitiesRequired || b.IsCivic || b.Lots.Count == 0) { return true; }
            return SegmentServed(b.Lots[0].Key.SegmentId);
        }

        /// <summary>What is missing for a building, in words ("power, water"), or an empty string if it is served.</summary>
        public string MissingServices(Building b)
        {
            if (!UtilitiesRequired || b.IsCivic || b.Lots.Count == 0) { return ""; }
            int id = b.Lots[0].Key.SegmentId;
            var missing = new List<string>();
            if (!SegmentHasPower(id)) { missing.Add("power"); }
            if (!SegmentHasWater(id)) { missing.Add("water"); }
            if (!SegmentHasSewage(id)) { missing.Add("sewage"); }
            return string.Join(", ", missing.ToArray());
        }

        // ------------------------------------------------------------ fires

        /// <summary>How much of the day's fire risk applies: each day without a fire, this chance that one starts (random events must be on).</summary>
        public double FireChancePerDay = 0.15;

        public Building BurningBuilding { get; private set; }
        private int fireStartTick;

        /// <summary>Raised when a building catches fire (the traffic sends a fire engine).</summary>
        public event Action<Building> FireStarted;

        private void StartFire()
        {
            var candidates = new List<Building>();
            foreach (Building b in buildings.Values)
            {
                if (!b.IsCivic) { candidates.Add(b); }
            }

            if (candidates.Count < 6) { return; }

            BurningBuilding = candidates[rng.Next(candidates.Count)];
            fireStartTick = TickCount;
            bool station = HasFacility(BuildingShape.FireStation);
            RaiseEvent("Fire at " + Inspector.Title(BurningBuilding) + "! "
                + (station ? "The fire brigade is on its way." : "Nobody can put it out: a fire station would help (unlocks at 5,000 residents)."));
            Action<Building> handler = FireStarted;
            if (handler != null) { handler(BurningBuilding); }
        }

        private void AdvanceFire()
        {
            if (BurningBuilding == null) { return; }
            if (!buildings.ContainsKey(BurningBuilding.Id)) { BurningBuilding = null; return; }

            int age = TickCount - fireStartTick;
            if (age >= 18 && HasFacility(BuildingShape.FireStation))
            {
                RaiseEvent("The fire at " + Inspector.Title(BurningBuilding) + " is out.");
                BurningBuilding = null;
            }
            else if (age >= 72)
            {
                Building lost = BurningBuilding;
                BurningBuilding = null;
                RaiseEvent(Inspector.Title(lost) + " burned down.");
                RemoveBuilding(lost);
            }
        }
    }
}
