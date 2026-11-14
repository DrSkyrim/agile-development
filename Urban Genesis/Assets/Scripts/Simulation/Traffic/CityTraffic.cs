using System;
using System.Collections.Generic;

namespace UrbanGenesis.Simulation.Traffic
{
    /// <summary>
    /// Vehicles driving on the road network. The economy says who travels where (<see cref="TripFlow"/>); this turns those
    /// trips into individual cars and trucks that follow the quickest route, queue behind each other, obey the traffic lights
    /// and lane rules, and take turns at junctions. Time here is plain seconds: the game passes real time multiplied by the speed setting.
    /// </summary>
    public sealed class CityTraffic
    {
        /// <summary>A car's free-flow speed on a street, metres per second (about 40 km/h). Faster roads multiply it.</summary>
        public const double StreetSpeed = 11.0;

        private const double MinGap = 2.0;
        private const double TimeHeadway = 1.2;
        private const double ComfortBraking = 3.0;
        private const double MaxStuckSeconds = 90.0;
        private const double MaxSubstep = 0.1;

        private struct Request
        {
            public VehicleType Type;
            public int FromSegmentId;
            public int ToSegmentId;
            public double FromS;
            public double ToS;
            public int Attempts;
        }

        private readonly CitySimulation city;
        private readonly Random rng;
        private readonly RoutePlanner planner;
        private readonly List<Vehicle> vehicles = new List<Vehicle>();
        private readonly Dictionary<int, List<Vehicle>> lanes = new Dictionary<int, List<Vehicle>>();
        private readonly Dictionary<int, Vehicle> tokens = new Dictionary<int, Vehicle>();
        private readonly Dictionary<long, double> credits = new Dictionary<long, double>();
        private readonly List<Request> pending = new List<Request>();
        private readonly List<Vehicle> scratch = new List<Vehicle>();
        private int nextVehicleId = 1;

        public CityTraffic(CitySimulation city, int seed)
        {
            this.city = city;
            rng = new Random(seed);
            planner = new RoutePlanner(city.Roads);
            Signals = new TrafficSignals(city);
            MaxVehicles = 300;
            TripsPerVolume = 0.006;
            OutsideTripsPerTick = 0.12;
        }

        public TrafficSignals Signals { get; private set; }

        /// <summary>Seconds of traffic time that have passed (pauses with the game). Drives the traffic lights.</summary>
        public double Time { get; private set; }

        public int MaxVehicles { get; set; }

        /// <summary>How many vehicles one unit of trip volume from the economy puts on the road.</summary>
        public double TripsPerVolume { get; set; }

        /// <summary>Vehicles per game tick that come in from (or leave for) each outside connection, once the city has some buildings.</summary>
        public double OutsideTripsPerTick { get; set; }

        public IList<Vehicle> Vehicles { get { return vehicles; } }
        public int Count { get { return vehicles.Count; } }
        public int PendingCount { get { return pending.Count; } }
        public int Arrivals { get; private set; }
        public int GridlockRemovals { get; private set; }

        public event Action<Vehicle> VehicleAdded;
        public event Action<Vehicle> VehicleRemoved;

        // ------------------------------------------------------------------ spawning

        /// <summary>Call once per simulation tick: turns the economy's trips into vehicles waiting to enter the roads.</summary>
        public void OnTick()
        {
            double perTick = 1.0 / Math.Max(1, city.Economy.UpdateIntervalTicks);
            IList<TripFlow> flows = city.Economy.Flows;
            for (int i = 0; i < flows.Count; i++)
            {
                TripFlow flow = flows[i];
                long key = ((long)(int)flow.Kind << 56) ^ ((long)flow.FromSegmentId << 28) ^ flow.ToSegmentId;
                double credit;
                credits.TryGetValue(key, out credit);
                credit = Math.Min(3.0, credit + flow.Volume * TripsPerVolume * perTick);
                while (credit >= 1.0)
                {
                    credit -= 1.0;
                    Enqueue(ChooseType(flow), flow.FromSegmentId, flow.ToSegmentId);
                }

                credits[key] = credit;
            }

            if (credits.Count > 2000)
            {
                credits.Clear();
            }

            SpawnOutsideTraffic();
            RebuildLanes();
            DrainPending();
        }

        private VehicleType ChooseType(TripFlow flow)
        {
            if (flow.Kind != FlowKind.Goods)
            {
                return VehicleType.Car;
            }

            return rng.NextDouble() < (flow.Volume > 8.0 || flow.Distance > 700.0 ? 0.55 : 0.2) ? VehicleType.LongTruck : VehicleType.ShortTruck;
        }

        private void SpawnOutsideTraffic()
        {
            if (city.OutsideConnections.Count == 0 || city.BuildingCount < 4)
            {
                return;
            }

            double chance = OutsideTripsPerTick * Math.Min(1.0, city.BuildingCount / 30.0);
            foreach (CitySimulation.OutsideConnection connection in city.OutsideConnections)
            {
                if (connection.Segment == null || rng.NextDouble() >= chance)
                {
                    continue;
                }

                int target = RandomBuildingSegment();
                if (target < 0)
                {
                    continue;
                }

                double roll = rng.NextDouble();
                VehicleType type = roll < 0.55 ? VehicleType.Car : roll < 0.85 ? VehicleType.ShortTruck : VehicleType.LongTruck;
                if (rng.NextDouble() < 0.5)
                {
                    Enqueue(type, connection.Segment.Id, target);
                }
                else
                {
                    Enqueue(type, target, connection.Segment.Id);
                }
            }
        }

        private int RandomBuildingSegment()
        {
            int count = city.BuildingCount;
            if (count == 0)
            {
                return -1;
            }

            int pick = rng.Next(count);
            foreach (Building b in city.Buildings)
            {
                if (pick-- == 0)
                {
                    return b.Lots.Count > 0 ? b.Lots[0].Key.SegmentId : -1;
                }
            }

            return -1;
        }

        private void Enqueue(VehicleType type, int fromSegment, int toSegment)
        {
            if (pending.Count >= 150)
            {
                return;
            }

            RoadSegment from = city.Roads.GetSegment(fromSegment);
            RoadSegment to = city.Roads.GetSegment(toSegment);
            if (from == null || to == null)
            {
                return;
            }

            pending.Add(new Request
            {
                Type = type,
                FromSegmentId = fromSegment,
                ToSegmentId = toSegment,
                FromS = PickSpot(from),
                ToS = PickSpot(to)
            });
        }

        // A spot beside the road to start or stop at: away from the junctions at either end where it can be.
        private double PickSpot(RoadSegment segment)
        {
            if (segment.IsOutsideConnection)
            {
                return rng.NextDouble() < 0.5 ? 6.0 : segment.Length - 6.0;
            }

            double margin = Math.Min(segment.Length * 0.4, 30.0);
            return margin + rng.NextDouble() * (segment.Length - 2.0 * margin);
        }

        private void DrainPending()
        {
            int tries = Math.Min(pending.Count, 25);
            for (int i = 0; i < tries && pending.Count > 0; i++)
            {
                if (vehicles.Count >= MaxVehicles)
                {
                    return;
                }

                Request r = pending[0];
                pending.RemoveAt(0);
                if (!TrySpawn(r) && ++r.Attempts < 40)
                {
                    pending.Add(r);
                }
            }
        }

        /// <summary>Puts a vehicle on the road now, or says false if there is no room yet. A trip with no route is dropped (returns true).</summary>
        private bool TrySpawn(Request r)
        {
            RoadSegment from = city.Roads.GetSegment(r.FromSegmentId);
            RoadSegment to = city.Roads.GetSegment(r.ToSegmentId);
            if (from == null || to == null)
            {
                return true;
            }

            List<RouteStep> route = planner.Plan(from, r.FromS, to, r.ToS);
            if (route == null || route.Count == 0)
            {
                return true;
            }

            var v = new Vehicle { Id = nextVehicleId, Type = r.Type, Route = route, StepIndex = 0 };
            RouteStep first = route[0];
            v.S = first.Forward ? r.FromS : first.Segment.Length - r.FromS;
            if (route.Count == 1)
            {
                v.FinalS = first.Forward ? r.ToS : first.Segment.Length - r.ToS;
            }
            else
            {
                RouteStep last = route[route.Count - 1];
                v.FinalS = last.Forward ? r.ToS : last.Segment.Length - r.ToS;
            }

            v.Variant = rng.Next(1000);
            v.SpeedBias = 0.92 + rng.NextDouble() * 0.16;
            v.Lane = PickLane(v, 0, rng.Next(RoadSpecs.LanesPerDirection(first.Segment.Type)));
            v.PreviousLane = v.Lane;
            v.NextLane = v.Lane;

            List<Vehicle> list = LaneList(first.Segment, first.Forward, v.Lane);
            for (int i = 0; i < list.Count; i++)
            {
                Vehicle o = list[i];
                if (Math.Abs(o.S - v.S) < o.Length + v.Length + 3.0)
                {
                    return false;
                }
            }

            nextVehicleId++;
            vehicles.Add(v);
            list.Add(v);
            Action<Vehicle> added = VehicleAdded;
            if (added != null) { added(v); }
            return true;
        }

        /// <summary>Puts a vehicle on the road right now between two chosen spots (used by tests and tools). False if there is no route or room.</summary>
        public bool TestSpawn(VehicleType type, RoadSegment from, double fromS, RoadSegment to, double toS)
        {
            RebuildLanes();
            return TrySpawn(new Request { Type = type, FromSegmentId = from.Id, ToSegmentId = to.Id, FromS = fromS, ToS = toS }) && vehicles.Count > 0;
        }

        // ------------------------------------------------------------------ lanes

        private static int LaneKey(RoadSegment segment, bool forward, int lane)
        {
            return (segment.Id * 2 + (forward ? 0 : 1)) * 4 + lane;
        }

        private List<Vehicle> LaneList(RoadSegment segment, bool forward, int lane)
        {
            int key = LaneKey(segment, forward, lane);
            List<Vehicle> list;
            if (!lanes.TryGetValue(key, out list))
            {
                list = new List<Vehicle>();
                lanes[key] = list;
            }

            return list;
        }

        private void RebuildLanes()
        {
            foreach (KeyValuePair<int, List<Vehicle>> pair in lanes)
            {
                pair.Value.Clear();
            }

            for (int i = 0; i < vehicles.Count; i++)
            {
                Vehicle v = vehicles[i];
                RouteStep step = v.Step;
                LaneList(step.Segment, step.Forward, v.Lane).Add(v);
            }

            foreach (KeyValuePair<int, List<Vehicle>> pair in lanes)
            {
                pair.Value.Sort((a, b) => b.S.CompareTo(a.S));
            }
        }

        /// <summary>The lane to use on a step, given what the next turn needs (banned turns per lane) and how busy each lane is.</summary>
        private int PickLane(Vehicle v, int stepIndex, int preferred)
        {
            RouteStep step = v.Route[stepIndex];
            int count = RoadSpecs.LanesPerDirection(step.Segment.Type);
            preferred = Math.Max(0, Math.Min(count - 1, preferred));
            if (count == 1 || stepIndex >= v.Route.Count - 1)
            {
                return preferred;
            }

            RoadNode node = step.Forward ? step.Segment.B : step.Segment.A;
            RoadSegment next = v.Route[stepIndex + 1].Segment;
            if (node.Degree < 3 || next == step.Segment)
            {
                return preferred;
            }

            int chosen = preferred;
            double bestScore = double.MaxValue;
            for (int l = 0; l < count; l++)
            {
                if (!node.IsTurnAllowed(step.Segment, l, next))
                {
                    continue;
                }

                List<Vehicle> list;
                int busy = lanes.TryGetValue(LaneKey(step.Segment, step.Forward, l), out list) ? list.Count : 0;
                double score = busy * 3.0 + Math.Abs(l - preferred);
                if (score < bestScore) { bestScore = score; chosen = l; }
            }

            return chosen;
        }

        // ------------------------------------------------------------------ movement

        /// <summary>Moves every vehicle on by <paramref name="seconds"/> of traffic time (0 when the game is paused).</summary>
        public void Advance(double seconds)
        {
            if (seconds <= 0.0)
            {
                return;
            }

            int slices = (int)Math.Ceiling(seconds / MaxSubstep);
            slices = Math.Min(slices, 60);
            double dt = seconds / slices;
            for (int i = 0; i < slices; i++)
            {
                Substep(dt);
            }
        }

        private void Substep(double dt)
        {
            Time += dt;
            RemoveInvalid();
            if (vehicles.Count == 0)
            {
                return;
            }

            RebuildLanes();

            // Who may cross a junction: whoever has waited longest gets the first claim.
            scratch.Clear();
            scratch.AddRange(vehicles);
            scratch.Sort((a, b) => b.WaitSeconds.CompareTo(a.WaitSeconds));
            for (int i = 0; i < scratch.Count; i++)
            {
                DecideCrossing(scratch[i]);
            }

            foreach (KeyValuePair<int, List<Vehicle>> pair in lanes)
            {
                List<Vehicle> list = pair.Value;
                for (int k = 0; k < list.Count; k++)
                {
                    Drive(list[k], k > 0 ? list[k - 1] : null, dt);
                }
            }

            for (int i = vehicles.Count - 1; i >= 0; i--)
            {
                Vehicle v = vehicles[i];
                Cross(v);
                v.Age += dt;
                v.StuckSeconds = v.Speed < 0.2 ? v.StuckSeconds + dt : 0.0;
                v.WaitSeconds = v.Speed < 0.5 ? v.WaitSeconds + dt : 0.0;
                ReleaseToken(v, dt);
                if (v.Arrived)
                {
                    Arrivals++;
                    Remove(i);
                }
                else if (v.StuckSeconds > MaxStuckSeconds)
                {
                    GridlockRemovals++;
                    Remove(i);
                }
            }
        }

        private void RemoveInvalid()
        {
            for (int i = vehicles.Count - 1; i >= 0; i--)
            {
                Vehicle v = vehicles[i];
                bool ok = true;
                for (int s = v.StepIndex; s < v.Route.Count && ok; s++)
                {
                    RoadSegment seg = v.Route[s].Segment;
                    ok = city.Roads.GetSegment(seg.Id) == seg && !seg.IsClosed
                        && (v.Route[s].Forward || !RoadSpecs.IsOneWay(seg.Type))
                        && (s != v.StepIndex || v.Lane < RoadSpecs.LanesPerDirection(seg.Type));
                }

                if (!ok)
                {
                    Remove(i);
                }
            }
        }

        private void Remove(int index)
        {
            Vehicle v = vehicles[index];
            vehicles.RemoveAt(index);
            stopGap.Remove(v);
            plannedLane.Remove(v);
            if (v.HeldNode != null)
            {
                Vehicle owner;
                if (tokens.TryGetValue(v.HeldNode.Id, out owner) && owner == v) { tokens.Remove(v.HeldNode.Id); }
                v.HeldNode = null;
            }

            Action<Vehicle> removed = VehicleRemoved;
            if (removed != null) { removed(v); }
        }

        // Per-vehicle data worked out in DecideCrossing and used by Drive.
        private readonly Dictionary<Vehicle, double> stopGap = new Dictionary<Vehicle, double>();
        private readonly Dictionary<Vehicle, int> plannedLane = new Dictionary<Vehicle, int>();

        private double StopLine(Vehicle v, RoadNode node)
        {
            RouteStep step = v.Step;
            double radius = node.Degree >= 3 ? LaneGeometry.JunctionRadius(node) + 0.5 : 0.0;
            return step.Segment.Length - radius - 0.5;
        }

        private void DecideCrossing(Vehicle v)
        {
            stopGap[v] = double.MaxValue;
            if (v.IsLastStep)
            {
                return;
            }

            RoadNode node = v.NextNode;
            RouteStep step = v.Step;
            RouteStep next = v.Route[v.StepIndex + 1];
            int nextLane = PickLane(v, v.StepIndex + 1, v.Lane);
            plannedLane[v] = nextLane;
            v.NextLane = nextLane;

            if (node.Degree < 3 || v.HeldNode == node)
            {
                return;
            }

            double dist = StopLine(v, node) - v.S;
            if (dist < -1.0)
            {
                return; // already past the line (the road was changed under it): let it through
            }

            SignalState state = Signals.StateFor(node, step.Segment, Time);
            double stopping = v.Speed * v.Speed / (2.0 * 3.5);
            bool signalOk = state == SignalState.Green || (state == SignalState.Amber && stopping > dist);

            Vehicle owner;
            bool tokenFree = node.HasTrafficLight || !tokens.TryGetValue(node.Id, out owner) || owner == v || owner.HeldNode != node || owner.Arrived;

            List<Vehicle> exit;
            bool room = true;
            if (lanes.TryGetValue(LaneKey(next.Segment, next.Forward, nextLane), out exit) && exit.Count > 0)
            {
                Vehicle tail = exit[exit.Count - 1];
                room = tail.S - tail.Length > 3.0 || tail.Speed > 4.0 && tail.S - tail.Length > 1.0;
            }

            bool permitted = signalOk && tokenFree && room;
            if (permitted && dist < 3.0 + v.Speed * 0.8)
            {
                if (!node.HasTrafficLight) { tokens[node.Id] = v; }
                v.HeldNode = node;
                v.HeldSeconds = 0.0;
                return;
            }

            if (!permitted)
            {
                // Not allowed yet: treat the stop line as a wall. (Allowed but still far away: just keep approaching.)
                stopGap[v] = Math.Max(0.0, dist) + MinGap;
            }
        }

        private void Drive(Vehicle v, Vehicle leader, double dt)
        {
            RouteStep step = v.Step;
            RoadSegment seg = step.Segment;
            double v0 = StreetSpeed * RoadSpecs.SpeedFactor(seg.Type) * VehicleSpecs.SpeedFactor(v.Type) * v.SpeedBias;

            double gap = double.MaxValue;
            double leaderSpeed = 0.0;
            if (leader != null)
            {
                gap = leader.S - leader.Length - v.S;
                leaderSpeed = leader.Speed;
            }
            else if (!v.IsLastStep)
            {
                RouteStep next = v.Route[v.StepIndex + 1];
                int lane;
                if (!plannedLane.TryGetValue(v, out lane)) { lane = v.Lane; }
                List<Vehicle> list;
                if (lanes.TryGetValue(LaneKey(next.Segment, next.Forward, lane), out list) && list.Count > 0)
                {
                    Vehicle tail = list[list.Count - 1];
                    gap = (seg.Length - v.S) + tail.S - tail.Length;
                    leaderSpeed = tail.Speed;
                }
            }

            double wall = double.MaxValue;
            double distToWall = double.MaxValue;
            double wallGap;
            if (stopGap.TryGetValue(v, out wallGap) && wallGap < double.MaxValue)
            {
                wall = wallGap;
                distToWall = Math.Max(0.0, wallGap - MinGap);
            }

            if (v.IsLastStep)
            {
                double left = v.FinalS - v.S;
                if (left < 14.0) { v0 = Math.Min(v0, 4.0); }
            }
            else if (v.NextNode.Degree >= 3 && seg.Length - v.S < 25.0)
            {
                v0 = Math.Min(v0, 8.0);
            }

            double a = VehicleSpecs.Acceleration(v.Type);
            double speed = v.Speed;
            double free = 1.0 - Math.Pow(speed / Math.Max(v0, 0.1), 4.0);
            double interaction = 0.0;
            if (gap < double.MaxValue)
            {
                double sStar = MinGap + Math.Max(0.0, speed * TimeHeadway + speed * (speed - leaderSpeed) / (2.0 * Math.Sqrt(a * ComfortBraking)));
                double r = sStar / Math.Max(gap, 0.1);
                interaction = Math.Max(interaction, r * r);
            }

            if (wall < double.MaxValue)
            {
                double sStar = MinGap + Math.Max(0.0, speed * TimeHeadway + speed * speed / (2.0 * Math.Sqrt(a * ComfortBraking)));
                double r = sStar / Math.Max(wall, 0.1);
                interaction = Math.Max(interaction, r * r);
            }

            double acc = Math.Max(-9.0, a * (free - interaction));
            speed = Math.Max(0.0, speed + acc * dt);

            double move = speed * dt;
            if (gap < double.MaxValue) { move = Math.Min(move, Math.Max(0.0, gap - 0.4)); }
            if (distToWall < double.MaxValue) { move = Math.Min(move, distToWall); }
            if (move < speed * dt) { speed = Math.Min(speed, move / dt); }

            v.Speed = speed;
            v.S += move;
        }

        // Moves a vehicle that has reached the end of its road onto the next one, or finishes its trip.
        private void Cross(Vehicle v)
        {
            while (!v.Arrived)
            {
                if (v.IsLastStep)
                {
                    if (v.S >= v.FinalS) { v.Arrived = true; }
                    return;
                }

                double length = v.Step.Segment.Length;
                if (v.S < length)
                {
                    return;
                }

                v.S -= length;
                v.PreviousLane = v.Lane;
                v.StepIndex++;
                int lane;
                if (!plannedLane.TryGetValue(v, out lane)) { lane = v.Lane; }
                v.Lane = Math.Max(0, Math.Min(RoadSpecs.LanesPerDirection(v.Step.Segment.Type) - 1, lane));
            }
        }

        private void ReleaseToken(Vehicle v, double dt)
        {
            if (v.HeldNode == null)
            {
                return;
            }

            v.HeldSeconds += dt;
            bool crossed = !v.Arrived && v.NextNode != v.HeldNode;
            bool clear = crossed && v.S >= LaneGeometry.JunctionRadius(v.HeldNode) + v.Length;
            if (clear || v.HeldSeconds > 25.0 || v.Arrived)
            {
                Vehicle owner;
                if (tokens.TryGetValue(v.HeldNode.Id, out owner) && owner == v) { tokens.Remove(v.HeldNode.Id); }
                v.HeldNode = null;
            }
        }
    }
}
