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
                case BuildingShape.WaterTower: return "Supplies water to roads with a pipe connected to it. Must stand near the river.";
                case BuildingShape.SewageOutlet: return "Takes the sewage away through the same pipes. Must stand near the river.";
                case BuildingShape.Hospital: return "Ambulances drive out to the homes.";
                case BuildingShape.PoliceStation: return "Police cars patrol the streets.";
                case BuildingShape.FireStation: return "Puts out fires. Without one, burning buildings are lost.";
                case BuildingShape.School: return "School buses collect the children.";
                default: return "";
            }
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

            if (CivicInfo.NeedsRiver(shape))
            {
                if (River == null)
                {
                    plan.Error = "This map has no river.";
                    return plan;
                }

                if (River.DistanceTo(center) > River.IntakeRadius)
                {
                    plan.Error = "Too far from the river: a " + CivicInfo.Name(shape).ToLowerInvariant() + " must stand within " + (int)River.IntakeRadius + " m of it.";
                    return plan;
                }
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

        // ------------------------------------------------------------ cables and pipes

        public const int UtilityWorkDays = 3;

        private readonly Dictionary<int, int> worksUntil = new Dictionary<int, int>();

        public bool HasRoadworks(RoadSegment segment)
        {
            return worksUntil.ContainsKey(segment.Id);
        }

        public static bool HasLine(RoadSegment segment, UtilityKind kind)
        {
            return kind == UtilityKind.Power ? segment.HasPower : segment.HasWater;
        }

        public UtilityPlan PlanUtility(RoadSegment segment, UtilityKind kind)
        {
            var plan = new UtilityPlan { Segment = segment, Kind = kind };
            string what = kind == UtilityKind.Power ? "power cable" : "water and sewage pipe";
            if (segment.IsOutsideConnection || segment.HasLots == false)
            {
                plan.Error = "Highways carry no " + what + "s: lay them along ordinary roads.";
                return plan;
            }

            if (HasLine(segment, kind))
            {
                plan.Error = "This road already has a " + what + ".";
                return plan;
            }

            if (segment.IsClosed)
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

        /// <summary>Lays the line and closes the road for <see cref="UtilityWorkDays"/> days while the crews dig. Returns an error message, or null.</summary>
        public string LayUtility(RoadSegment segment, UtilityKind kind)
        {
            UtilityPlan plan = PlanUtility(segment, kind);
            if (!plan.IsValid) { return plan.Error; }

            Treasury.Charge(plan.Cost);
            if (kind == UtilityKind.Power) { segment.HasPower = true; } else { segment.HasWater = true; }
            segment.OpenFraction = 0f;
            worksUntil[segment.Id] = TickCount + UtilityWorkDays * GameTime.TicksPerDay;
            servicesDirty = true;
            Revision++;
            return null;
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
            Flood(UtilityKind.Power, BuildingShape.PowerPlant, poweredSegments);
            Flood(UtilityKind.Water, BuildingShape.WaterTower, wateredSegments);
            Flood(UtilityKind.Water, BuildingShape.SewageOutlet, sewerSegments);
        }

        private void Flood(UtilityKind kind, BuildingShape source, HashSet<int> result)
        {
            result.Clear();
            var queue = new Queue<RoadSegment>();
            foreach (Building b in buildings.Values)
            {
                if (b.Shape != source || b.Lots.Count == 0) { continue; }
                RoadSegment host = Roads.GetSegment(b.Lots[0].Key.SegmentId);
                if (host != null && HasLine(host, kind) && result.Add(host.Id)) { queue.Enqueue(host); }
            }

            while (queue.Count > 0)
            {
                RoadSegment s = queue.Dequeue();
                RoadNode[] ends = { s.A, s.B };
                for (int e = 0; e < 2; e++)
                {
                    foreach (RoadSegment other in ends[e].Segments)
                    {
                        if (HasLine(other, kind) && result.Add(other.Id)) { queue.Enqueue(other); }
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
