using System;

namespace UrbanGenesis.Simulation
{
    /// <summary>
    /// Names one lot cell: which road segment, which side of it (0 = left of its direction, 1 = right),
    /// how far along (column, each <see cref="RoadNetwork.LotSize"/> metres) and how far out (row 0 touches the road).
    /// </summary>
    public struct LotKey : IEquatable<LotKey>
    {
        public readonly int SegmentId;
        public readonly int Side;
        public readonly int Col;
        public readonly int Row;

        public LotKey(int segmentId, int side, int col, int row)
        {
            SegmentId = segmentId;
            Side = side;
            Col = col;
            Row = row;
        }

        public bool Equals(LotKey other)
        {
            return SegmentId == other.SegmentId && Side == other.Side && Col == other.Col && Row == other.Row;
        }

        public override bool Equals(object obj) { return obj is LotKey && Equals((LotKey)obj); }

        public override int GetHashCode()
        {
            unchecked
            {
                int h = SegmentId;
                h = h * 31 + Side;
                h = h * 31 + Col;
                h = h * 31 + Row;
                return h;
            }
        }

        public override string ToString()
        {
            return "Lot(seg " + SegmentId + ", " + (Side == 0 ? "left" : "right") + ", col " + Col + ", row " + Row + ")";
        }
    }

    /// <summary>One cell of buildable land beside a road. Zoning is stored here, and buildings stand on groups of lots.</summary>
    public sealed class Lot
    {
        public LotKey Key { get; internal set; }

        /// <summary>The four corners, anticlockwise.</summary>
        public Vec2[] Corners { get; internal set; }

        public Vec2 Center { get; internal set; }
        public Aabb Bounds { get; internal set; }

        /// <summary>The two corners on the road side, in order of increasing distance along the road.</summary>
        public Vec2 FrontStart { get; internal set; }
        public Vec2 FrontEnd { get; internal set; }

        public ZoneType Zone { get; internal set; }
        public Building Building { get; internal set; }

        public bool IsFree { get { return Building == null; } }
        public bool IsRoadside { get { return Key.Row == 0; } }

        internal Lot(LotKey key)
        {
            Key = key;
        }
    }
}
