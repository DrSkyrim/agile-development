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
        public RoadAnchor Start { get; internal set; }
        public RoadAnchor End { get; internal set; }

        /// <summary>
        /// Buildings the road would run over. Filled in by <see cref="CitySimulation.PlanRoad"/> (the road network alone
        /// does not know about buildings). The road can still be built; the buildings must then be demolished.
        /// </summary>
        public IList<Building> Demolitions { get; internal set; }

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
            return FindSegmentNear(p, HalfWidth, out length) != null;
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
        public RoadPlan Plan(Vec2 start, Vec2? control, Vec2 end)
        {
            RoadAnchor a;
            RoadAnchor b;
            string error;
            if (!ResolveAnchor(start, out a, out error) || !ResolveAnchor(end, out b, out error))
            {
                return RoadPlan.Fail(error);
            }

            Vec2 control2 = control.HasValue ? control.Value : Vec2.Lerp(a.Position, b.Position, 0.5);
            var curve = new RoadCurve(a.Position, control2, b.Position);

            if (curve.Length < MinRoadLength)
            {
                return RoadPlan.Fail("Too short: roads must be at least " + (int)MinRoadLength + " m long.");
            }

            if (curve.MinRadius() < MinCurveRadius)
            {
                return RoadPlan.Fail("Too sharp a bend: the tightest curve is " + (int)MinCurveRadius + " m radius.");
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

            string clearanceError = CheckClearance(curve, samples, a, b);
            if (clearanceError != null)
            {
                return RoadPlan.Fail(clearanceError);
            }

            return new RoadPlan { IsValid = true, Error = string.Empty, Curve = curve, Start = a, End = b };
        }

        private static bool SameJunction(RoadAnchor a, RoadAnchor b)
        {
            if (a.Kind == AnchorKind.Node && b.Kind == AnchorKind.Node)
            {
                return a.NodeId == b.NodeId;
            }

            return false;
        }

        private bool ResolveAnchor(Vec2 p, out RoadAnchor anchor, out string error)
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

        private string CheckClearance(RoadCurve curve, List<Vec2> samples, RoadAnchor a, RoadAnchor b)
        {
            foreach (RoadSegment other in SegmentsNear(curve.Bounds, RoadWidth))
            {
                bool nearA = Touches(a, other);
                bool nearB = Touches(b, other);

                for (int i = 0; i < samples.Count; i++)
                {
                    Vec2 p = samples[i];
                    if ((nearA && Vec2.Distance(p, a.Position) < JunctionZone) ||
                        (nearB && Vec2.Distance(p, b.Position) < JunctionZone))
                    {
                        continue;
                    }

                    if (other.Curve.DistanceTo(p) < MinClearance)
                    {
                        return "Too close to, or crossing, another road. Roads can only meet at a junction: end this road on the other one.";
                    }
                }
            }

            return null;
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

            var segment = new RoadSegment(nextSegmentId++, startNode, endNode, plan.Curve, DefaultCapacity);
            segments[segment.Id] = segment;
            startNode.Attach(segment);
            endNode.Attach(segment);
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

            var first = new RoadSegment(nextSegmentId++, old.A, middle, firstCurve, old.BaseCapacity);
            var second = new RoadSegment(nextSegmentId++, middle, old.B, secondCurve, old.BaseCapacity);
            first.OpenFraction = old.OpenFraction;
            second.OpenFraction = old.OpenFraction;

            segments[first.Id] = first;
            segments[second.Id] = second;
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
