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

        /// <summary>Where the front of the vehicle is.</summary>
        public Vec2 Position { get { return PositionAt(S, 0.0); } }

        /// <summary>Position of a point <paramref name="back"/> metres behind the front.</summary>
        public Vec2 PositionAt(double s, double back)
        {
            RouteStep step = Step;
            double along = s - back;
            double length = step.Segment.Length;
            if (along < 0.0)
            {
                // Behind the start of this step: follow the previous one if there is one.
                if (StepIndex > 0)
                {
                    RouteStep prev = Route[StepIndex - 1];
                    return Point(prev, prev.Segment.Length + along);
                }

                along = 0.0;
            }

            return Point(step, System.Math.Min(along, length));
        }

        private Vec2 Point(RouteStep step, double along)
        {
            RoadSegment seg = step.Segment;
            double offset = RoadSpecs.LaneOffset(seg.Type, Lane);
            if (step.Forward)
            {
                return seg.Curve.OffsetPointAtLength(along, -offset);
            }

            return seg.Curve.OffsetPointAtLength(seg.Length - along, offset);
        }

        /// <summary>Unit vector the vehicle is heading in.</summary>
        public Vec2 Heading
        {
            get
            {
                RouteStep step = Step;
                double along = System.Math.Max(0.0, System.Math.Min(S, step.Segment.Length));
                Vec2 t = step.Segment.Curve.TangentAtLength(step.Forward ? along : step.Segment.Length - along);
                return step.Forward ? t : t * -1.0;
            }
        }
    }
}
