using System.Collections.Generic;

namespace UrbanGenesis.Simulation
{
    /// <summary>A junction or dead end where road segments meet.</summary>
    public sealed class RoadNode
    {
        private readonly List<RoadSegment> segments = new List<RoadSegment>(4);

        public int Id { get; private set; }
        public Vec2 Position { get; private set; }

        /// <summary>The segments that start or end here.</summary>
        public IList<RoadSegment> Segments { get { return segments; } }

        public int Degree { get { return segments.Count; } }

        /// <summary>Three or more roads meet here (a T-junction, a four-way crossing, ...).</summary>
        public bool IsIntersection { get { return segments.Count >= 3; } }

        /// <summary>The player has switched the traffic lights off here. They are on by default.</summary>
        public bool TrafficLightsOff { get; internal set; }

        /// <summary>Traffic lights stand here: it is an intersection and they have not been switched off.</summary>
        public bool HasTrafficLight { get { return CanHaveTrafficLights && !TrafficLightsOff; } }

        /// <summary>
        /// Whether lights make sense here: an intersection where at least one road has buildings beside it.
        /// A junction made only of highways and ramps is free-flowing and never has lights.
        /// </summary>
        public bool CanHaveTrafficLights
        {
            get
            {
                if (!IsIntersection)
                {
                    return false;
                }

                for (int i = 0; i < segments.Count; i++)
                {
                    if (segments[i].HasLots)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public RoadNode(int id, Vec2 position)
        {
            Id = id;
            Position = position;
        }

        // ------------------------------------------------------------ lane connections

        /// <summary>One lane's connection into one lane of another road that the player has forbidden. Everything not listed is allowed.</summary>
        public struct BlockedTurn
        {
            public int From;
            public int Lane;
            public int To;

            /// <summary>The lane of the road being entered (0 = outermost). -1 only while loading old saves: every lane of the road.</summary>
            public int ToLane;
        }

        private readonly List<BlockedTurn> blocked = new List<BlockedTurn>();

        public IList<BlockedTurn> BlockedTurns { get { return blocked; } }

        /// <summary>Whether vehicles can arrive at this junction along the segment (false for a one-way road that leads away from here).</summary>
        public bool CanArriveVia(RoadSegment segment)
        {
            return !(RoadSpecs.IsOneWay(segment.Type) && segment.A == this);
        }

        /// <summary>Whether vehicles can leave this junction along the segment (false for a one-way road that leads into here).</summary>
        public bool CanLeaveVia(RoadSegment segment)
        {
            return !(RoadSpecs.IsOneWay(segment.Type) && segment.B == this);
        }

        /// <summary>How many lanes arrive here along the segment (0 if none can).</summary>
        public int ArrivingLanes(RoadSegment segment)
        {
            return CanArriveVia(segment) ? RoadSpecs.LanesPerDirection(segment.Type) : 0;
        }

        /// <summary>Whether a turn from one road to another is possible at all (ignoring what the player has forbidden): different roads, and the directions allow it.</summary>
        public bool IsTurnPossible(RoadSegment from, RoadSegment to)
        {
            return from != to && CanArriveVia(from) && CanLeaveVia(to);
        }

        /// <summary>How many lanes leave this junction along the segment (0 if none can).</summary>
        public int LeavingLanes(RoadSegment segment)
        {
            return CanLeaveVia(segment) ? RoadSpecs.LanesPerDirection(segment.Type) : 0;
        }

        /// <summary>Whether lane <paramref name="lane"/> (0 = outermost) of <paramref name="from"/> is joined to lane <paramref name="toLane"/> of <paramref name="to"/>.</summary>
        public bool IsLaneConnected(RoadSegment from, int lane, RoadSegment to, int toLane)
        {
            if (!IsTurnPossible(from, to) || lane < 0 || lane >= ArrivingLanes(from) || toLane < 0 || toLane >= LeavingLanes(to))
            {
                return false;
            }

            return IndexOfBlocked(from.Id, lane, to.Id, toLane) < 0;
        }

        /// <summary>Whether a vehicle in lane <paramref name="lane"/> (0 = outermost) of <paramref name="from"/> may go on into <paramref name="to"/> (by any lane of it).</summary>
        public bool IsTurnAllowed(RoadSegment from, int lane, RoadSegment to)
        {
            int lanes = LeavingLanes(to);
            for (int l = 0; l < lanes; l++)
            {
                if (IsLaneConnected(from, lane, to, l))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>How many roads the lane may continue into.</summary>
        public int CountExits(RoadSegment from, int lane)
        {
            int count = 0;
            for (int i = 0; i < segments.Count; i++)
            {
                if (IsTurnAllowed(from, lane, segments[i]))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>How many lanes (of any road) the lane is joined to.</summary>
        public int CountLaneExits(RoadSegment from, int lane)
        {
            int count = 0;
            for (int i = 0; i < segments.Count; i++)
            {
                int lanes = LeavingLanes(segments[i]);
                for (int l = 0; l < lanes; l++)
                {
                    if (IsLaneConnected(from, lane, segments[i], l))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        internal void SetTurn(int from, int lane, int to, int toLane, bool allowed)
        {
            int index = IndexOfBlocked(from, lane, to, toLane);
            if (allowed && index >= 0)
            {
                blocked.RemoveAt(index);
            }
            else if (!allowed && index < 0)
            {
                blocked.Add(new BlockedTurn { From = from, Lane = lane, To = to, ToLane = toLane });
            }
        }

        /// <summary>Old saves blocked a whole road; spell that out lane by lane now that the roads are known.</summary>
        internal void ExpandWildcardTurns()
        {
            for (int i = blocked.Count - 1; i >= 0; i--)
            {
                BlockedTurn t = blocked[i];
                if (t.ToLane >= 0) { continue; }
                blocked.RemoveAt(i);
                for (int k = 0; k < segments.Count; k++)
                {
                    if (segments[k].Id != t.To) { continue; }
                    int lanes = RoadSpecs.LanesPerDirection(segments[k].Type);
                    for (int l = 0; l < lanes; l++) { SetTurn(t.From, t.Lane, t.To, l, false); }
                }
            }
        }

        internal void ClearTurns() { blocked.Clear(); }

        /// <summary>A segment was split: the piece that now touches this junction keeps the old one's rules.</summary>
        internal void ReplaceSegmentInTurns(int oldId, int newId)
        {
            for (int i = 0; i < blocked.Count; i++)
            {
                BlockedTurn t = blocked[i];
                if (t.From == oldId) { t.From = newId; }
                if (t.To == oldId) { t.To = newId; }
                blocked[i] = t;
            }
        }

        internal void ForgetSegmentInTurns(int segmentId)
        {
            blocked.RemoveAll(t => t.From == segmentId || t.To == segmentId);
        }

        private int IndexOfBlocked(int from, int lane, int to, int toLane)
        {
            for (int i = 0; i < blocked.Count; i++)
            {
                if (blocked[i].From == from && blocked[i].Lane == lane && blocked[i].To == to && blocked[i].ToLane == toLane)
                {
                    return i;
                }
            }

            return -1;
        }

        internal void Attach(RoadSegment segment) { segments.Add(segment); }
        internal void Detach(RoadSegment segment) { segments.Remove(segment); }
    }
}
