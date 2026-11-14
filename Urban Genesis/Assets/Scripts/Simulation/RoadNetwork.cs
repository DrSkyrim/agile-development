using System;
using System.Collections.Generic;

namespace UrbanGenesis.Simulation
{
    /// <summary>How a road end attaches to the network.</summary>
    public enum AnchorKind
    {
        /// <summary>A free end in empty ground.</summary>
        New,

        /// <summary>Joins an existing node.</summary>
        Node,

        /// <summary>Joins the middle of an existing segment, splitting it in two.</summary>
        Split
    }

    public struct RoadAnchor
    {
        public AnchorKind Kind;
        public Vec2 Position;
        public int NodeId;
        public int SegmentId;
        public double SplitLength;
    }

    /// <summary>The result of checking a road before building it. If valid, hand it to <see cref="RoadNetwork.AddRoad"/>.</summary>
    /// <summary>A place where a new highway crosses an existing one.</summary>
    internal sealed class RoadCrossing
    {
        public int SegmentId;
        public double HostLength;
        public double NewLength;
        public Vec2 Point;
        public double Zone;
    }

    public sealed class RoadPlan
    {
        public RoadPlan()
        {
            Demolitions = new Building[0];
        }

        public bool IsValid { get; internal set; }

        /// <summary>Why the road cannot be built (empty when valid).</summary>
        public string Error { get; internal set; }

        public RoadCurve Curve { get; internal set; }
        public RoadType Type { get; internal set; }
        public RoadAnchor Start { get; internal set; }
        public RoadAnchor End { get; internal set; }

        /// <summary>Where this road crosses other highways (a flat four-way junction at each), in order along the road.</summary>
        internal List<RoadCrossing> Crossings = new List<RoadCrossing>();

        /// <summary>
        /// Buildings the road would run over. Filled in by <see cref="CitySimulation.PlanRoad"/> (the road network alone
        /// does not know about buildings). The road can still be built; the buildings must then be demolished.
        /// </summary>
        public IList<Building> Demolitions { get; internal set; }

        /// <summary>What building the road costs (zero while money is off). Filled in by <see cref="CitySimulation.PlanRoad"/>.</summary>
        public double Cost { get; internal set; }

        internal static RoadPlan Fail(string error)
        {
            return new RoadPlan { IsValid = false, Error = error };
        }
    }

    /// <summary>
    /// The road graph: nodes joined by curved segments, each <see cref="RoadWidth"/> metres wide.
    /// Roads can start and end in open ground, join an existing node, or join the middle of an existing road
    /// (which is split there). Roads cannot cross other roads: build a junction and join them instead.
    /// </summary>
    public sealed class RoadNetwork
    {
        public const float DefaultCapacity = 1f;

        /// <summary>Road width: two of the old 8 m grid tiles.</summary>
        public const double RoadWidth = 16.0;
        public const double HalfWidth = RoadWidth / 2.0;

        /// <summary>Size of one lot cell along the road. Junction splits land on multiples of this.</summary>
        public const double LotSize = 8.0;

        /// <summary>A click this close to an existing node joins that node.</summary>
        public const double NodeSnapRadius = 12.0;

        /// <summary>A click this close to a road's centre line (that is, on the road) joins that road.</summary>
        public const double SegmentJoinRadius = HalfWidth;

        /// <summary>Shortest road you can draw.</summary>
        public const double MinRoadLength = 40.0;

        /// <summary>Shortest piece a road may be split into; joins closer than this to a junction are refused.</summary>
        public const double MinPieceLength = 16.0;

        /// <summary>Tightest bend allowed. Four rows of lots (32 m) fit inside it without folding over.</summary>
        public const double MinCurveRadius = 48.0;

        /// <summary>Two roads that are not joined must keep their centre lines at least this far apart.</summary>
        public const double MinClearance = RoadWidth * 0.9;

        /// <summary>Near a junction, roads naturally run close together; this much of the road is exempt from the clearance rule.</summary>
        public const double JunctionZone = 20.0;

        /// <summary>Smallest angle allowed between two roads leaving the same junction.</summary>
        public const double MinJunctionAngleDegrees = 30.0;

        private readonly Dictionary<int, RoadNode> nodes = new Dictionary<int, RoadNode>();
        private readonly Dictionary<int, RoadSegment> segments = new Dictionary<int, RoadSegment>();
        private readonly Aabb bounds;
        private int nextNodeId = 1;
        private int nextSegmentId = 1;

        public event Action<RoadSegment> SegmentAdded;
        public event Action<RoadSegment> SegmentRemoved;

        /// <summary>Raised when a road is rebuilt as another type (upgraded) without moving.</summary>
        public event Action<RoadSegment> SegmentRetyped;

        /// <summary>Raised when a segment is cut in two by a new junction: (old segment, first part, second part, length of the first part).</summary>
        public event Action<RoadSegment, RoadSegment, RoadSegment, double> SegmentSplit;

        public RoadNetwork(Aabb worldBounds)
        {
            bounds = worldBounds;
        }

        /// <summary>The area roads must stay inside.</summary>
        public Aabb WorldBounds { get { return bounds; } }

        public int NodeCount { get { return nodes.Count; } }
        public int SegmentCount { get { return segments.Count; } }
        public IEnumerable<RoadNode> Nodes { get { return nodes.Values; } }
        public IEnumerable<RoadSegment> Segments { get { return segments.Values; } }

        public RoadNode GetNode(int id)
        {
            RoadNode node;
            nodes.TryGetValue(id, out node);
            return node;
        }

        public RoadSegment GetSegment(int id)
        {
            RoadSegment segment;
            segments.TryGetValue(id, out segment);
            return segment;
        }

        // ---------------------------------------------------------------- queries

        /// <summary>The segments whose centre line comes within <paramref name="margin"/> metres of the box.</summary>
        public List<RoadSegment> SegmentsNear(Aabb box, double margin)
        {
            var result = new List<RoadSegment>();
            Aabb expanded = box.Expanded(margin);
            foreach (RoadSegment segment in segments.Values)
            {
                if (segment.Curve.Bounds.Overlaps(expanded))
                {
                    result.Add(segment);
                }
            }

            return result;
        }

        // ---------------------------------------------------------------- upgrading

        /// <summary>Why a road cannot be rebuilt as <paramref name="type"/>, or null if it can.</summary>
        public string CheckUpgrade(RoadSegment segment, RoadType type)
        {
            string name = RoadSpecs.Name(type).ToLowerInvariant();
            if (segment.IsOutsideConnection)
            {
                return "The road to the outside world cannot be rebuilt.";
            }

            if (segment.Type == type)
            {
                return "This is already a " + name + ".";
            }

            if (segment.Length < RoadSpecs.MinLength(type))
            {
                return "Too short for a " + name + " (at least " + (int)RoadSpecs.MinLength(type) + " m).";
            }

            if (segment.Curve.MinRadius() < RoadSpecs.MinRadius(type))
            {
                return "Too sharp a bend for a " + name + ".";
            }

            RoadNode[] ends = { segment.A, segment.B };
            for (int e = 0; e < 2; e++)
            {
                string join = CheckUpgradeJoin(segment, ends[e], type);
                if (join != null)
                {
                    return join;
                }
            }

            double newHalf = RoadSpecs.HalfWidth(type);
            List<Vec2> samples = segment.Curve.Sample(2.0);
            foreach (RoadSegment other in SegmentsNear(segment.Curve.Bounds, RoadSpecs.MaxWidth))
            {
                if (other == segment)
                {
                    continue;
                }

                bool nearA = other.A == segment.A || other.B == segment.A;
                bool nearB = other.A == segment.B || other.B == segment.B;
                double need = 0.9 * (newHalf + other.HalfWidth);
                double zone = JunctionZone * Math.Max(1.0, Math.Max(RoadSpecs.Width(type), other.Width) / RoadSpecs.Width(RoadType.Street));
                for (int i = 0; i < samples.Count; i++)
                {
                    Vec2 p = samples[i];
                    if ((nearA && Vec2.Distance(p, segment.A.Position) < zone) || (nearB && Vec2.Distance(p, segment.B.Position) < zone))
                    {
                        continue;
                    }

                    if (other.Curve.DistanceTo(p) < need)
                    {
                        return "A " + name + " would be too close to another road.";
                    }
                }
            }

            return null;
        }

        // At a junction the joining road must be allowed to join the road it meets (see RoadSpecs.CanJoinMidway).
        private static string CheckUpgradeJoin(RoadSegment segment, RoadNode node, RoadType type)
        {
            if (node.Degree < 3)
            {
                return null;
            }

            string name = RoadSpecs.Name(type).ToLowerInvariant();
            if (node.Degree == 3)
            {
                // The two roads that run most nearly straight on are the through road; the third one joins it.
                RoadSegment h1 = null, h2 = null, joiner = null;
                double straightest = 2.0;
                for (int i = 0; i < 3; i++)
                {
                    RoadSegment x = node.Segments[i], y = node.Segments[(i + 1) % 3], z = node.Segments[(i + 2) % 3];
                    double dot = Vec2.Dot(Away(x, node), Away(y, node));
                    if (dot < straightest) { straightest = dot; h1 = x; h2 = y; joiner = z; }
                }

                RoadType joinerType = joiner == segment ? type : joiner.Type;
                RoadType h1Type = h1 == segment ? type : h1.Type;
                RoadType h2Type = h2 == segment ? type : h2.Type;
                if (!RoadSpecs.CanJoinMidway(joinerType, h1Type) || !RoadSpecs.CanJoinMidway(joinerType, h2Type))
                {
                    return "A " + name + " there would not fit the roads that meet it.";
                }

                return null;
            }

            for (int i = 0; i < node.Segments.Count; i++)
            {
                RoadSegment other = node.Segments[i];
                if (other == segment) { continue; }
                if (!RoadSpecs.CanJoinMidway(type, other.Type) || !RoadSpecs.CanJoinMidway(other.Type, type))
                {
                    return "A " + name + " cannot cross the " + RoadSpecs.Name(other.Type).ToLowerInvariant() + " that meets it.";
                }
            }

            return null;
        }

        private static Vec2 Away(RoadSegment segment, RoadNode node)
        {
            return segment.A == node ? segment.Curve.TangentAtLength(0.0) : segment.Curve.TangentAtLength(segment.Length) * -1.0;
        }

        /// <summary>Rebuilds the road as another type. Call <see cref="CheckUpgrade"/> first. Lane rules of the road are dropped when the lanes change.</summary>
        internal void Retype(RoadSegment segment, RoadType type)
        {
            bool lanesChange = RoadSpecs.LanesPerDirection(segment.Type) != RoadSpecs.LanesPerDirection(type)
                || RoadSpecs.IsOneWay(segment.Type) != RoadSpecs.IsOneWay(type);
            segment.SetType(type);
            if (lanesChange)
            {
                segment.A.ForgetSegmentInTurns(segment.Id);
                segment.B.ForgetSegmentInTurns(segment.Id);
            }

            Action<RoadSegment> changed = SegmentRetyped;
            if (changed != null)
            {
                changed(segment);
            }
        }

        public RoadNode FindNodeNear(Vec2 p, double radius)
        {
            RoadNode best = null;
            double bestDistance = radius;
            foreach (RoadNode node in nodes.Values)
            {
                double d = Vec2.Distance(node.Position, p);
                if (d <= bestDistance)
                {
                    bestDistance = d;
                    best = node;
                }
            }

            return best;
        }

        /// <summary>The road whose centre line is nearest to the point, if within <paramref name="radius"/>; <paramref name="length"/> is how far along it the nearest point is.</summary>
        public RoadSegment FindSegmentNear(Vec2 p, double radius, out double length)
        {
            RoadSegment best = null;
            double bestDistance = radius;
            length = 0.0;
            foreach (RoadSegment segment in segments.Values)
            {
                if (!segment.Curve.Bounds.Expanded(radius).Contains(p))
                {
                    continue;
                }

                double distance;
                double s = segment.Curve.ClosestLength(p, out distance);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = segment;
                    length = s;
                }
            }

            return best;
        }

        /// <summary>True if the point is on a road surface.</summary>
        public bool IsOnRoad(Vec2 p)
        {
            double length;
            RoadSegment segment = FindSegmentNear(p, RoadSpecs.MaxHalfWidth, out length);
            return segment != null && segment.Curve.DistanceTo(p) <= segment.HalfWidth;
        }

        // ---------------------------------------------------------------- planning

        /// <summary>Control point that makes the curve pass through <paramref name="through"/> halfway along.</summary>
        public static Vec2 ControlThrough(Vec2 start, Vec2 through, Vec2 end)
        {
            return through * 2.0 - (start + end) * 0.5;
        }

        /// <summary>Checks a straight road, snapping both ends to nearby nodes and roads.</summary>
        public RoadPlan PlanStraight(Vec2 start, Vec2 end)
        {
            return Plan(start, null, end);
        }

        /// <summary>
        /// Checks a road and works out how its ends join the network. <paramref name="control"/> is the curve's
        /// control point (null for a straight road). Nothing is changed.
        /// </summary>
        public RoadPlan Plan(Vec2 start, Vec2? control, Vec2 end, RoadType type = RoadType.Street)
        {
            RoadAnchor a;
            RoadAnchor b;
            string error;
            if (!ResolveAnchor(start, type, out a, out error) || !ResolveAnchor(end, type, out b, out error))
            {
                return RoadPlan.Fail(error);
            }

            Vec2 control2 = control.HasValue ? control.Value : Vec2.Lerp(a.Position, b.Position, 0.5);
            var curve = new RoadCurve(a.Position, control2, b.Position);

            if (curve.Length < RoadSpecs.MinLength(type))
            {
                return RoadPlan.Fail("Too short: a " + RoadSpecs.Name(type).ToLowerInvariant() + " must be at least " + (int)RoadSpecs.MinLength(type) + " m long.");
            }

            if (curve.MinRadius() < RoadSpecs.MinRadius(type))
            {
                return RoadPlan.Fail("Too sharp a bend: the tightest curve for a " + RoadSpecs.Name(type).ToLowerInvariant() + " is " + (int)RoadSpecs.MinRadius(type) + " m radius.");
            }

            if (SameJunction(a, b))
            {
                return RoadPlan.Fail("Both ends join the same spot.");
            }

            List<Vec2> samples = curve.Sample(4.0);
            Aabb inner = bounds.Expanded(-LotSize * 5.0);
            for (int i = 0; i < samples.Count; i++)
            {
                if (!inner.Contains(samples[i]))
                {
                    return RoadPlan.Fail("Too close to the edge of the map.");
                }
            }

            string angleError = CheckAngle(a, curve.Tangent(0.0)) ?? CheckAngle(b, -curve.Tangent(1.0));
            if (angleError != null)
            {
                return RoadPlan.Fail(angleError);
            }

            List<RoadCrossing> crossings;
            string crossingError = FindCrossings(curve, a, b, type, out crossings);
            if (crossingError != null)
            {
                return RoadPlan.Fail(crossingError);
            }

            string clearanceError = CheckClearance(curve, samples, a, b, type, crossings);
            if (clearanceError != null)
            {
                return RoadPlan.Fail(clearanceError);
            }

            return new RoadPlan { IsValid = true, Error = string.Empty, Curve = curve, Type = type, Start = a, End = b, Crossings = crossings };
        }

        private static bool SameJunction(RoadAnchor a, RoadAnchor b)
        {
            if (a.Kind == AnchorKind.Node && b.Kind == AnchorKind.Node)
            {
                return a.NodeId == b.NodeId;
            }

            return false;
        }

        private bool ResolveAnchor(Vec2 p, RoadType type, out RoadAnchor anchor, out string error)
        {
            error = null;
            anchor = new RoadAnchor { Kind = AnchorKind.New, Position = p };

            RoadNode node = FindNodeNear(p, NodeSnapRadius);
            if (node != null)
            {
                anchor = new RoadAnchor { Kind = AnchorKind.Node, Position = node.Position, NodeId = node.Id };
                return true;
            }

            double length;
            RoadSegment segment = FindSegmentNear(p, SegmentJoinRadius, out length);
            if (segment != null)
            {
                if (!RoadSpecs.CanJoinMidway(type, segment.Type))
                {
                    if (!RoadSpecs.AllowsJunctionsAlong(type))
                    {
                        error = type == RoadType.OneWayHighway
                            ? "A one-way highway can branch off a highway, but elsewhere it connects only at junctions or in open ground."
                            : "A " + RoadSpecs.Name(type).ToLowerInvariant() + " only connects at junctions or in open ground, not in the middle of a road.";
                    }
                    else
                    {
                        error = "Roads cannot join a " + RoadSpecs.Name(segment.Type).ToLowerInvariant() + " in the middle: connect to its end, or use a one-way highway as a turn-off.";
                    }
                    return false;
                }

                double snapped = Math.Round(length / LotSize) * LotSize;
                if (snapped < MinPieceLength || snapped > segment.Length - MinPieceLength)
                {
                    error = "Too close to a junction: join the junction itself, or a spot at least " + (int)MinPieceLength + " m away.";
                    return false;
                }

                anchor = new RoadAnchor
                {
                    Kind = AnchorKind.Split,
                    Position = segment.Curve.PointAtLength(snapped),
                    SegmentId = segment.Id,
                    SplitLength = snapped
                };
                return true;
            }

            return true;
        }

        /// <summary>Directions in which existing roads leave the anchor, as unit vectors.</summary>
        private List<Vec2> ExistingDirections(RoadAnchor anchor)
        {
            var result = new List<Vec2>();
            if (anchor.Kind == AnchorKind.Node)
            {
                RoadNode node = nodes[anchor.NodeId];
                for (int i = 0; i < node.Segments.Count; i++)
                {
                    RoadSegment s = node.Segments[i];
                    result.Add(s.A == node ? s.Curve.Tangent(0.0) : -s.Curve.Tangent(1.0));
                }
            }
            else if (anchor.Kind == AnchorKind.Split)
            {
                RoadSegment s = segments[anchor.SegmentId];
                Vec2 t = s.Curve.TangentAtLength(anchor.SplitLength);
                result.Add(t);
                result.Add(-t);
            }

            return result;
        }

        private string CheckAngle(RoadAnchor anchor, Vec2 newDirection)
        {
            double cosLimit = Math.Cos(MinJunctionAngleDegrees * Math.PI / 180.0);
            foreach (Vec2 existing in ExistingDirections(anchor))
            {
                if (Vec2.Dot(existing, newDirection) > cosLimit)
                {
                    return "Too shallow an angle where it joins the other road (at least " + (int)MinJunctionAngleDegrees + " degrees).";
                }
            }

            return null;
        }

        private string CheckClearance(RoadCurve curve, List<Vec2> samples, RoadAnchor a, RoadAnchor b, RoadType type, List<RoadCrossing> crossings)
        {
            foreach (RoadSegment other in SegmentsNear(curve.Bounds, RoadSpecs.MaxWidth))
            {
                bool nearA = Touches(a, other);
                bool nearB = Touches(b, other);

                // A wide road takes longer to get clear of: scale the zone around the junction with its width (a street is the baseline).
                double zone = JunctionZone * Math.Max(1.0, other.Width / RoadSpecs.Width(RoadType.Street));

                RoadCrossing crossing = null;
                for (int c = 0; c < crossings.Count; c++)
                {
                    if (crossings[c].SegmentId == other.Id) { crossing = crossings[c]; }
                }

                for (int i = 0; i < samples.Count; i++)
                {
                    Vec2 p = samples[i];
                    if (crossing != null && Vec2.Distance(p, crossing.Point) < crossing.Zone)
                    {
                        continue;
                    }

                    if ((nearA && Vec2.Distance(p, a.Position) < zone) ||
                        (nearB && Vec2.Distance(p, b.Position) < zone))
                    {
                        continue;
                    }

                    if (other.Curve.DistanceTo(p) < (RoadSpecs.Width(type) + other.Width) * 0.45)
                    {
                        return "Too close to, or crossing, another road. Roads can only meet at a junction: end this road on the other one.";
                    }
                }
            }

            return null;
        }

        private const double CrossingMargin = 30.0;
        private const double MinCrossingSine = 0.57; // about 35 degrees

        /// <summary>
        /// Finds where a new limited-access road (a highway or ramp) crosses existing ones. Each crossing becomes a flat four-way junction.
        /// Returns an error message if the crossing is not acceptable (too shallow, too close to an end, crossing the same road twice).
        /// </summary>
        private string FindCrossings(RoadCurve curve, RoadAnchor a, RoadAnchor b, RoadType type, out List<RoadCrossing> crossings)
        {
            crossings = new List<RoadCrossing>();
            if (RoadSpecs.HasLots(type))
            {
                return null;
            }

            List<Vec2> mine = curve.Sample(2.0);
            foreach (RoadSegment other in SegmentsNear(curve.Bounds, RoadSpecs.MaxWidth))
            {
                if (RoadSpecs.HasLots(other.Type) || Touches(a, other) || Touches(b, other))
                {
                    continue;
                }

                List<Vec2> theirs = other.Curve.Sample(2.0);
                bool found = false;
                Vec2 first = Vec2.Zero;
                for (int i = 0; i + 1 < mine.Count; i++)
                {
                    for (int j = 0; j + 1 < theirs.Count; j++)
                    {
                        Vec2 hit;
                        if (!SegmentsIntersect(mine[i], mine[i + 1], theirs[j], theirs[j + 1], out hit))
                        {
                            continue;
                        }

                        if (!found)
                        {
                            found = true;
                            first = hit;
                        }
                        else if (Vec2.Distance(hit, first) > 6.0)
                        {
                            return "A highway can cross another road only once.";
                        }
                    }
                }

                if (!found)
                {
                    continue;
                }

                // Home in on the exact crossing point on both curves.
                double d;
                double sOther = other.Curve.ClosestLength(first, out d);
                double sNew = curve.ClosestLength(first, out d);
                for (int k = 0; k < 8; k++)
                {
                    sNew = curve.ClosestLength(other.Curve.PointAtLength(sOther), out d);
                    sOther = other.Curve.ClosestLength(curve.PointAtLength(sNew), out d);
                }

                Vec2 tNew = curve.TangentAtLength(sNew);
                Vec2 tOther = other.Curve.TangentAtLength(sOther);
                double sine = Math.Abs(tNew.X * tOther.Y - tNew.Y * tOther.X);
                if (sine < MinCrossingSine)
                {
                    return "Cross the other road at a steeper angle (at least about 35 degrees).";
                }

                if (sOther < CrossingMargin || sOther > other.Length - CrossingMargin || sNew < CrossingMargin || sNew > curve.Length - CrossingMargin)
                {
                    return "Too close to the end of a road to cross here: cross at least " + (int)CrossingMargin + " m from a junction or an end.";
                }

                crossings.Add(new RoadCrossing
                {
                    SegmentId = other.Id,
                    HostLength = sOther,
                    NewLength = sNew,
                    Point = other.Curve.PointAtLength(sOther),
                    Zone = (RoadSpecs.Width(type) + other.Width) * 0.5 / sine + 3.0
                });
            }

            crossings.Sort((x, y) => x.NewLength.CompareTo(y.NewLength));
            for (int i = 1; i < crossings.Count; i++)
            {
                if (crossings[i].NewLength - crossings[i - 1].NewLength < CrossingMargin)
                {
                    return "Two crossings are too close together.";
                }
            }

            return null;
        }

        private static bool SegmentsIntersect(Vec2 p, Vec2 p2, Vec2 q, Vec2 q2, out Vec2 hit)
        {
            hit = Vec2.Zero;
            double rx = p2.X - p.X, ry = p2.Y - p.Y;
            double sx = q2.X - q.X, sy = q2.Y - q.Y;
            double denominator = rx * sy - ry * sx;
            if (Math.Abs(denominator) < 1e-12)
            {
                return false;
            }

            double t = ((q.X - p.X) * sy - (q.Y - p.Y) * sx) / denominator;
            double u = ((q.X - p.X) * ry - (q.Y - p.Y) * rx) / denominator;
            if (t < 0.0 || t > 1.0 || u < 0.0 || u > 1.0)
            {
                return false;
            }

            hit = new Vec2(p.X + t * rx, p.Y + t * ry);
            return true;
        }

        /// <summary>True if the anchor joins this segment (or a node at one of its ends).</summary>
        private bool Touches(RoadAnchor anchor, RoadSegment segment)
        {
            if (anchor.Kind == AnchorKind.Node)
            {
                return segment.A.Id == anchor.NodeId || segment.B.Id == anchor.NodeId;
            }

            return anchor.Kind == AnchorKind.Split && anchor.SegmentId == segment.Id;
        }

        // ---------------------------------------------------------------- changing the network

        /// <summary>Builds a valid plan. Returns the new segment, or null if the plan was not valid.</summary>
        public RoadSegment AddRoad(RoadPlan plan)
        {
            if (plan == null || !plan.IsValid)
            {
                return null;
            }

            RoadAnchor start = plan.Start;
            RoadAnchor end = plan.End;
            RoadNode startNode;
            RoadNode endNode;

            if (start.Kind == AnchorKind.Split && end.Kind == AnchorKind.Split && start.SegmentId == end.SegmentId)
            {
                // Both ends land on the same road. Split at the larger length first; the smaller
                // length then lies inside the first of the two new pieces, with the same length value.
                bool startLarger = start.SplitLength > end.SplitLength;
                RoadAnchor larger = startLarger ? start : end;
                RoadAnchor smaller = startLarger ? end : start;
                RoadSegment firstPiece;
                RoadNode largerNode = SplitAt(larger.SegmentId, larger.SplitLength, out firstPiece);
                RoadSegment ignored;
                RoadNode smallerNode = SplitAt(firstPiece.Id, smaller.SplitLength, out ignored);
                startNode = startLarger ? largerNode : smallerNode;
                endNode = startLarger ? smallerNode : largerNode;
            }
            else
            {
                startNode = ResolveNode(start);
                endNode = ResolveNode(end);
            }

            if (plan.Crossings.Count == 0)
            {
                return AddPiece(startNode, endNode, plan.Curve, plan.Type);
            }

            // The road crosses other highways: split each of them at the crossing, then lay the new road in pieces between the new junctions.
            RoadCurve rest = plan.Curve;
            RoadNode previous = startNode;
            double consumed = 0.0;
            RoadSegment firstPieceAdded = null;
            foreach (RoadCrossing crossing in plan.Crossings)
            {
                RoadSegment ignoredPiece;
                RoadNode junction = SplitAt(crossing.SegmentId, crossing.HostLength, out ignoredPiece);

                RoadCurve before;
                RoadCurve after;
                rest.SplitAtLength(crossing.NewLength - consumed, out before, out after);
                RoadSegment added = AddPiece(previous, junction, new RoadCurve(previous.Position, before.P1, junction.Position), plan.Type);
                if (firstPieceAdded == null)
                {
                    firstPieceAdded = added;
                }

                rest = after;
                consumed = crossing.NewLength;
                previous = junction;
            }

            AddPiece(previous, endNode, new RoadCurve(previous.Position, rest.P1, endNode.Position), plan.Type);
            return firstPieceAdded;
        }

        private RoadSegment AddPiece(RoadNode from, RoadNode to, RoadCurve curve, RoadType type)
        {
            var segment = new RoadSegment(nextSegmentId++, from, to, curve, RoadSpecs.BaseCapacity(type), type);
            segments[segment.Id] = segment;
            from.Attach(segment);
            to.Attach(segment);
            Raise(SegmentAdded, segment);
            return segment;
        }

        /// <summary>The nearest intersection (a junction of three or more roads) within <paramref name="radius"/> metres, or null.</summary>
        public RoadNode FindIntersectionNear(Vec2 p, double radius)
        {
            RoadNode best = null;
            double bestDistance = radius;
            foreach (RoadNode node in nodes.Values)
            {
                if (node.Degree < 3)
                {
                    continue;
                }

                double d = Vec2.Distance(node.Position, p);
                if (d <= bestDistance)
                {
                    bestDistance = d;
                    best = node;
                }
            }

            return best;
        }

        // ---------------------------------------------------------------- loading a saved network

        internal int NextNodeId { get { return nextNodeId; } set { nextNodeId = Math.Max(nextNodeId, value); } }
        internal int NextSegmentId { get { return nextSegmentId; } set { nextSegmentId = Math.Max(nextSegmentId, value); } }

        /// <summary>Recreates a saved junction with its saved id. No checks: the data came from a valid network.</summary>
        internal RoadNode RestoreNode(int id, Vec2 position)
        {
            var node = new RoadNode(id, position);
            nodes[id] = node;
            nextNodeId = Math.Max(nextNodeId, id + 1);
            return node;
        }

        /// <summary>Recreates a saved segment (its nodes must already exist) with its saved id.</summary>
        internal RoadSegment RestoreSegment(int id, int nodeA, int nodeB, RoadCurve curve, RoadType type, float openFraction, bool outsideConnection)
        {
            RoadNode a = nodes[nodeA];
            RoadNode b = nodes[nodeB];
            var segment = new RoadSegment(id, a, b, curve, RoadSpecs.BaseCapacity(type), type);
            segment.OpenFraction = openFraction;
            segment.IsOutsideConnection = outsideConnection;
            segments[id] = segment;
            nextSegmentId = Math.Max(nextSegmentId, id + 1);
            a.Attach(segment);
            b.Attach(segment);
            Raise(SegmentAdded, segment);
            return segment;
        }

        private RoadNode ResolveNode(RoadAnchor anchor)
        {
            switch (anchor.Kind)
            {
                case AnchorKind.Node:
                    return nodes[anchor.NodeId];
                case AnchorKind.Split:
                    RoadSegment unused;
                    return SplitAt(anchor.SegmentId, anchor.SplitLength, out unused);
                default:
                    return CreateNode(anchor.Position);
            }
        }

        private RoadNode CreateNode(Vec2 position)
        {
            var node = new RoadNode(nextNodeId++, position);
            nodes[node.Id] = node;
            return node;
        }

        /// <summary>Cuts a segment in two at the given length and returns the new junction node.</summary>
        private RoadNode SplitAt(int segmentId, double length, out RoadSegment firstPiece)
        {
            RoadSegment old = segments[segmentId];
            RoadCurve firstCurve;
            RoadCurve secondCurve;
            old.Curve.SplitAtLength(length, out firstCurve, out secondCurve);

            RoadNode middle = CreateNode(firstCurve.P2);

            segments.Remove(old.Id);
            old.A.Detach(old);
            old.B.Detach(old);

            var first = new RoadSegment(nextSegmentId++, old.A, middle, firstCurve, old.BaseCapacity, old.Type);
            var second = new RoadSegment(nextSegmentId++, middle, old.B, secondCurve, old.BaseCapacity, old.Type);
            first.IsOutsideConnection = old.IsOutsideConnection;
            second.IsOutsideConnection = old.IsOutsideConnection;
            first.OpenFraction = old.OpenFraction;
            second.OpenFraction = old.OpenFraction;

            segments[first.Id] = first;
            segments[second.Id] = second;
            old.A.ReplaceSegmentInTurns(old.Id, first.Id);
            old.B.ReplaceSegmentInTurns(old.Id, second.Id);
            old.A.Attach(first);
            middle.Attach(first);
            middle.Attach(second);
            old.B.Attach(second);

            firstPiece = first;
            Raise(SegmentSplit, old, first, second, length);
            return middle;
        }

        /// <summary>Removes a segment. Nodes left with no roads are removed too. Returns false if it was not in the network.</summary>
        public bool RemoveSegment(RoadSegment segment)
        {
            if (segment == null || !segments.ContainsKey(segment.Id))
            {
                return false;
            }

            segments.Remove(segment.Id);
            segment.A.Detach(segment);
            segment.B.Detach(segment);
            segment.A.ForgetSegmentInTurns(segment.Id);
            segment.B.ForgetSegmentInTurns(segment.Id);
            if (segment.A.Degree == 0)
            {
                nodes.Remove(segment.A.Id);
            }

            if (segment.B.Degree == 0)
            {
                nodes.Remove(segment.B.Id);
            }

            Raise(SegmentRemoved, segment);
            return true;
        }

        private static void Raise(Action<RoadSegment> handler, RoadSegment segment)
        {
            if (handler != null)
            {
                handler(segment);
            }
        }

        private void Raise(Action<RoadSegment, RoadSegment, RoadSegment, double> handler, RoadSegment old, RoadSegment first, RoadSegment second, double length)
        {
            if (handler != null)
            {
                handler(old, first, second, length);
            }
        }
    }
}
