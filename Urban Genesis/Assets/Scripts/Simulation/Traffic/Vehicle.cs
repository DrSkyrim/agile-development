using System;
using System.Collections.Generic;

namespace UrbanGenesis.Simulation.Traffic
{
    public struct RouteStep
    {
        public RoadSegment Segment;

        /// <summary>True when the step drives from segment end A to end B.</summary>
        public bool Forward;
    }

    public sealed class Vehicle
    {
        public int Id;
        public VehicleType Type;

        /// <summary>Picks the colour or model variant in the game layer.</summary>
        public int Variant;

        public List<RouteStep> Route;
        public int StepIndex;

        /// <summary>Distance driven along the current step, measured from where the step starts, to the vehicle's front.</summary>
        public double S;

        /// <summary>Where the last step ends (metres from its start); earlier steps run to the end of their road.</summary>
        public double FinalS;

        public int Lane;
        public double Speed;
        public double SpeedBias = 1.0;

        public double WaitSeconds;
        public double StuckSeconds;
        public double Age;

        public RoadNode HeldNode;
        public double HeldSeconds;
        public bool Arrived;

        public double Length { get { return VehicleSpecs.Length(Type); } }
        public RouteStep Step { get { return Route[StepIndex]; } }
        public bool IsLastStep { get { return StepIndex >= Route.Count - 1; } }

        public RoadNode NextNode
        {
            get { RouteStep s = Step; return s.Forward ? s.Segment.B : s.Segment.A; }
        }

        /// <summary>Lane used on the previous step and planned for the next one (so the path through a junction can join them).</summary>
        public int PreviousLane;
        public int NextLane;

        /// <summary>Where the front of the vehicle is.</summary>
        public Vec2 Position { get { return PositionAt(S, 0.0); } }

        /// <summary>Position of a point <paramref name="back"/> metres behind the front, following the road the vehicle came along.</summary>
        public Vec2 PositionAt(double s, double back)
        {
            int i = StepIndex;
            double along = s - back;
            while (along < 0.0 && i > 0)
            {
                i--;
                along += Route[i].Segment.Length;
            }

            return PathPoint(i, Math.Max(0.0, along));
        }

        /// <summary>Unit vector the vehicle is heading in.</summary>
        public Vec2 Heading
        {
            get
            {
                Vec2 d = PositionAt(S, 0.0) - PositionAt(S, 1.0);
                return d.LengthSquared < 1e-9 ? new Vec2(1.0, 0.0) : d.Normalized;
            }
        }

        private int LaneOf(int step)
        {
            return step == StepIndex ? Lane : step == StepIndex - 1 ? PreviousLane : step == StepIndex + 1 ? NextLane : Lane;
        }

        // The centre of the lane at a distance along a step. Near the junction at either end the path leaves the straight lane
        // and follows a smooth curve (see Connector) so vehicles turn instead of jumping sideways.
        private Vec2 PathPoint(int i, double along)
        {
            double length = Route[i].Segment.Length;
            if (i > 0)
            {
                double r = ConnectorRadius(i - 1);
                if (along < r) { return Connector(i - 1, 0.5 + 0.5 * along / r); }
            }

            if (i < Route.Count - 1)
            {
                double r = ConnectorRadius(i);
                if (along > length - r) { return Connector(i, 0.5 * (along - (length - r)) / r); }
            }

            return Straight(i, along, LaneOf(i));
        }

        private double ConnectorRadius(int i)
        {
            RouteStep step = Route[i];
            RoadNode node = step.Forward ? step.Segment.B : step.Segment.A;
            double r = node.Degree >= 3 ? LaneGeometry.JunctionRadius(node) : node.Degree == 2 ? 4.0 : 6.0;
            double limit = 0.5 * Math.Min(step.Segment.Length, Route[i + 1].Segment.Length);
            return Math.Max(0.5, Math.Min(r, limit));
        }

        private Vec2 Straight(int i, double along, int lane)
        {
            RouteStep step = Route[i];
            RoadSegment seg = step.Segment;
            along = Math.Max(0.0, Math.Min(along, seg.Length));
            double offset = RoadSpecs.LaneOffset(seg.Type, Math.Min(lane, RoadSpecs.LanesPerDirection(seg.Type) - 1));
            return step.Forward ? seg.Curve.OffsetPointAtLength(along, -offset) : seg.Curve.OffsetPointAtLength(seg.Length - along, offset);
        }

        private Vec2 Direction(int i, double along)
        {
            RouteStep step = Route[i];
            double s = Math.Max(0.0, Math.Min(along, step.Segment.Length));
            Vec2 t = step.Segment.Curve.TangentAtLength(step.Forward ? s : step.Segment.Length - s);
            return step.Forward ? t : t * -1.0;
        }

        // A smooth curve (u from 0 to 1) from the lane r metres before the junction to the lane r metres after it.
        private Vec2 Connector(int i, double u)
        {
            double r = ConnectorRadius(i);
            double length = Route[i].Segment.Length;
            Vec2 p0 = Straight(i, length - r, LaneOf(i));
            Vec2 p1 = Straight(i + 1, r, LaneOf(i + 1));
            Vec2 d0 = Direction(i, length - r);
            Vec2 d1 = Direction(i + 1, r);

            // The control point is where the incoming and outgoing lane lines meet (kept close for shallow angles and U-turns).
            Vec2 control;
            double cross = Vec2.Cross(d0, d1);
            if (Math.Abs(cross) < 0.05)
            {
                control = Vec2.Lerp(p0, p1, 0.5) + (Vec2.Dot(d0, d1) < 0.0 ? d0 * (r * 1.3) : Vec2.Zero);
            }
            else
            {
                double t = Vec2.Cross(p1 - p0, d1) / cross;
                t = Math.Max(0.0, Math.Min(t, 2.5 * r));
                control = p0 + d0 * t;
            }

            double w = 1.0 - u;
            return p0 * (w * w) + control * (2.0 * w * u) + p1 * (u * u);
        }
    }
}
