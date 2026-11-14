namespace UrbanGenesis.Simulation
{
    /// <summary>Where the lanes of a road are, near a junction. Used to draw and pick lanes.</summary>
    public static class LaneGeometry
    {
        /// <summary>The widest road that meets the junction: the distance from its middle where the roads separate.</summary>
        public static double JunctionRadius(RoadNode node)
        {
            double widest = 0.0;
            for (int i = 0; i < node.Segments.Count; i++)
            {
                if (node.Segments[i].HalfWidth > widest)
                {
                    widest = node.Segments[i].HalfWidth;
                }
            }

            return widest;
        }

        /// <summary>
        /// The middle of an arriving lane of <paramref name="segment"/>, <paramref name="distance"/> metres from the junction.
        /// A driver arriving keeps to the right, so lane 0 (the outermost) is the right-hand one.
        /// </summary>
        public static Vec2 ArrivingLane(RoadSegment segment, RoadNode node, int lane, double distance)
        {
            return OnArm(segment, node, RoadSpecs.LaneOffset(segment.Type, lane), distance, true);
        }

        /// <summary>The middle of the lanes leaving the junction along <paramref name="segment"/> (the middle of that side of the road).</summary>
        public static Vec2 LeavingSide(RoadSegment segment, RoadNode node, double distance)
        {
            int lanes = RoadSpecs.LanesPerDirection(segment.Type);
            double offset = 0.0;
            if (!RoadSpecs.IsOneWay(segment.Type))
            {
                offset = (RoadSpecs.LaneOffset(segment.Type, 0) + RoadSpecs.LaneOffset(segment.Type, lanes - 1)) * 0.5;
            }

            return OnArm(segment, node, offset, distance, false);
        }

        /// <summary>The middle of lane <paramref name="lane"/> of a road leaving the junction, <paramref name="distance"/> metres from it.</summary>
        public static Vec2 LeavingLane(RoadSegment segment, RoadNode node, int lane, double distance)
        {
            return OnArm(segment, node, RoadSpecs.LaneOffset(segment.Type, lane), distance, false);
        }

        private static Vec2 OnArm(RoadSegment segment, RoadNode node, double offset, double distance, bool arriving)
        {
            bool atStart = segment.A == node;
            double s = atStart ? distance : segment.Length - distance;

            // Offsets are positive to the left of the direction of increasing length. Arriving at the end B (heading with the
            // length) the right-hand side is negative; arriving at A (heading against it) it is positive. Leaving is the opposite.
            double sign = (atStart == arriving) ? 1.0 : -1.0;
            return segment.Curve.OffsetPointAtLength(s, sign * offset);
        }
    }
}
