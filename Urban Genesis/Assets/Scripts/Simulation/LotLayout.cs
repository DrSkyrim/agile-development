using System;
using System.Collections.Generic;

namespace UrbanGenesis.Simulation
{
    /// <summary>
    /// The lots around the roads. Every road segment gets a strip of lot cells on each side: each cell is
    /// <see cref="RoadNetwork.LotSize"/> metres along the road and there are <see cref="Rows"/> rows going outwards.
    /// Cells follow the curve of the road. A cell only exists where it is not covered by a road and is a sensible shape.
    /// Cells are created, kept and removed as roads change; a cell that survives a change keeps its zoning.
    /// </summary>
    public sealed class LotLayout
    {
        /// <summary>How many rows of lots there are on each side of a road (four rows is 32 m).</summary>
        public const int Rows = 4;

        /// <summary>Lots may overlap a neighbouring road by this much (metres); curves make the edges slightly inexact.</summary>
        public const double RoadOverlapTolerance = 0.8;

        /// <summary>A lot whose road-side edge is shorter than this is too squashed to use (inside of a tight bend).</summary>
        private const double MinFrontEdge = RoadNetwork.LotSize * 0.8;

        private const double HashCell = 32.0;

        private readonly RoadNetwork roads;
        private readonly Dictionary<LotKey, Lot> lots = new Dictionary<LotKey, Lot>();
        private readonly Dictionary<long, List<Lot>> hash = new Dictionary<long, List<Lot>>();

        public event Action<Lot> LotAdded;
        public event Action<Lot> LotRemoved;

        public LotLayout(RoadNetwork roads)
        {
            this.roads = roads;
        }

        public int Count { get { return lots.Count; } }
        public IEnumerable<Lot> Lots { get { return lots.Values; } }

        public Lot Get(LotKey key)
        {
            Lot lot;
            lots.TryGetValue(key, out lot);
            return lot;
        }

        /// <summary>How many full columns of lots fit along a segment.</summary>
        public static int Cols(RoadSegment segment)
        {
            return (int)Math.Floor((segment.Length + 0.05) / RoadNetwork.LotSize);
        }

        /// <summary>The four corners of a lot, anticlockwise. Side 0 is left of the road's direction, side 1 is right.</summary>
        public static Vec2[] Corners(RoadSegment segment, int side, int col, int row)
        {
            double s0 = col * RoadNetwork.LotSize;
            double s1 = s0 + RoadNetwork.LotSize;
            double sign = side == 0 ? 1.0 : -1.0;
            double o0 = sign * (RoadNetwork.HalfWidth + row * RoadNetwork.LotSize);
            double o1 = sign * (RoadNetwork.HalfWidth + (row + 1) * RoadNetwork.LotSize);

            RoadCurve c = segment.Curve;
            Vec2 a = c.OffsetPointAtLength(s0, o0);
            Vec2 b = c.OffsetPointAtLength(s1, o0);
            Vec2 d = c.OffsetPointAtLength(s1, o1);
            Vec2 e = c.OffsetPointAtLength(s0, o1);

            return side == 0 ? new[] { a, b, d, e } : new[] { e, d, b, a };
        }

        // ---------------------------------------------------------------- building and updating the layout

        /// <summary>
        /// Makes the segment's lots match the current roads: missing lots that are valid are added, lots that are no
        /// longer valid are removed. Returns the lots that were removed (so the caller can clear what stood on them).
        /// </summary>
        public List<Lot> Refresh(RoadSegment segment)
        {
            var removed = new List<Lot>();
            int cols = Cols(segment);

            // Existing lots first, so surviving lots keep their data.
            var existing = new List<Lot>();
            foreach (Lot lot in lots.Values)
            {
                if (lot.Key.SegmentId == segment.Id)
                {
                    existing.Add(lot);
                }
            }

            for (int i = 0; i < existing.Count; i++)
            {
                Lot lot = existing[i];
                Vec2[] corners = null;
                bool keep = lot.Key.Col < cols;
                if (keep)
                {
                    corners = Corners(segment, lot.Key.Side, lot.Key.Col, lot.Key.Row);
                    keep = IsValid(segment, corners, lot.Key.Side);
                }

                if (!keep)
                {
                    Remove(lot);
                    removed.Add(lot);
                }
                else
                {
                    Unhash(lot);
                    UpdateGeometry(lot, segment, corners);
                    Hash(lot);
                }
            }

            for (int side = 0; side < 2; side++)
            {
                for (int col = 0; col < cols; col++)
                {
                    for (int row = 0; row < Rows; row++)
                    {
                        var key = new LotKey(segment.Id, side, col, row);
                        if (lots.ContainsKey(key))
                        {
                            continue;
                        }

                        Vec2[] corners = Corners(segment, side, col, row);
                        if (!IsValid(segment, corners, side))
                        {
                            continue;
                        }

                        var lot = new Lot(key);
                        UpdateGeometry(lot, segment, corners);
                        Add(lot);
                    }
                }
            }

            return removed;
        }

        /// <summary>Removes every lot of a segment that no longer exists. Returns the removed lots.</summary>
        public List<Lot> RemoveSegment(int segmentId)
        {
            var removed = new List<Lot>();
            var doomed = new List<Lot>();
            foreach (Lot lot in lots.Values)
            {
                if (lot.Key.SegmentId == segmentId)
                {
                    doomed.Add(lot);
                }
            }

            for (int i = 0; i < doomed.Count; i++)
            {
                Remove(doomed[i]);
                removed.Add(doomed[i]);
            }

            return removed;
        }

        /// <summary>
        /// A segment was cut in two. The first <c>splitLength / LotSize</c> columns go to the first piece and the rest
        /// to the second, keeping their zoning and buildings. Lots are not re-checked here; call <see cref="Refresh"/>
        /// on both pieces afterwards.
        /// </summary>
        public void Remap(RoadSegment old, RoadSegment first, RoadSegment second, double splitLength)
        {
            int firstCols = (int)Math.Round(splitLength / RoadNetwork.LotSize);
            var moving = new List<Lot>();
            foreach (Lot lot in lots.Values)
            {
                if (lot.Key.SegmentId == old.Id)
                {
                    moving.Add(lot);
                }
            }

            for (int i = 0; i < moving.Count; i++)
            {
                Lot lot = moving[i];
                Unhash(lot);
                lots.Remove(lot.Key);

                LotKey k = lot.Key;
                lot.Key = k.Col < firstCols
                    ? new LotKey(first.Id, k.Side, k.Col, k.Row)
                    : new LotKey(second.Id, k.Side, k.Col - firstCols, k.Row);
                lots[lot.Key] = lot;
                Hash(lot);
            }
        }

        // ---------------------------------------------------------------- queries

        /// <summary>Lots whose centre is within <paramref name="radius"/> metres of a point.</summary>
        public List<Lot> LotsInCircle(Vec2 center, double radius)
        {
            var result = new List<Lot>();
            var seen = new HashSet<Lot>();
            int x0 = CellOf(center.X - radius);
            int x1 = CellOf(center.X + radius);
            int y0 = CellOf(center.Y - radius);
            int y1 = CellOf(center.Y + radius);
            double r2 = radius * radius;

            for (int cx = x0; cx <= x1; cx++)
            {
                for (int cy = y0; cy <= y1; cy++)
                {
                    List<Lot> bucket;
                    if (!hash.TryGetValue(HashKey(cx, cy), out bucket))
                    {
                        continue;
                    }

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        Lot lot = bucket[i];
                        if ((lot.Center - center).LengthSquared <= r2 && seen.Add(lot))
                        {
                            result.Add(lot);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>Lots whose box overlaps the given box.</summary>
        public List<Lot> LotsNear(Aabb box)
        {
            var result = new List<Lot>();
            var seen = new HashSet<Lot>();
            for (int cx = CellOf(box.MinX); cx <= CellOf(box.MaxX); cx++)
            {
                for (int cy = CellOf(box.MinY); cy <= CellOf(box.MaxY); cy++)
                {
                    List<Lot> bucket;
                    if (!hash.TryGetValue(HashKey(cx, cy), out bucket))
                    {
                        continue;
                    }

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        if (bucket[i].Bounds.Overlaps(box) && seen.Add(bucket[i]))
                        {
                            result.Add(bucket[i]);
                        }
                    }
                }
            }

            return result;
        }

        // ---------------------------------------------------------------- validity

        private bool IsValid(RoadSegment segment, Vec2[] corners, int side)
        {
            if (!Geometry.IsConvexAnticlockwise(corners))
            {
                return false;
            }

            // Corners are anticlockwise: left side runs a, b, d, e (front edge first); right side is reversed (front edge last).
            double front = side == 0 ? Vec2.Distance(corners[0], corners[1]) : Vec2.Distance(corners[2], corners[3]);
            if (front < MinFrontEdge)
            {
                return false;
            }

            Aabb inner = roads.WorldBounds.Expanded(-RoadNetwork.LotSize * 2.0);
            for (int i = 0; i < corners.Length; i++)
            {
                if (!inner.Contains(corners[i]))
                {
                    return false;
                }
            }

            return !OverlapsOtherRoads(segment, corners);
        }

        private bool OverlapsOtherRoads(RoadSegment own, Vec2[] corners)
        {
            Aabb box = Aabb.Of(corners);
            foreach (RoadSegment other in roads.SegmentsNear(box, RoadNetwork.HalfWidth))
            {
                if (other.Id == own.Id)
                {
                    continue;
                }

                IList<Vec2[]> surface = other.Surface;
                for (int i = 0; i < surface.Count; i++)
                {
                    if (Aabb.Of(surface[i]).Overlaps(box) && Geometry.ConvexOverlap(corners, surface[i], RoadOverlapTolerance))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void UpdateGeometry(Lot lot, RoadSegment segment, Vec2[] corners)
        {
            lot.Corners = corners;
            lot.Bounds = Aabb.Of(corners);
            lot.Center = (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25;

            if (lot.Key.Side == 0)
            {
                lot.FrontStart = corners[0];
                lot.FrontEnd = corners[1];
            }
            else
            {
                lot.FrontStart = corners[3];
                lot.FrontEnd = corners[2];
            }
        }

        // ---------------------------------------------------------------- storage

        private void Add(Lot lot)
        {
            lots[lot.Key] = lot;
            Hash(lot);
            Action<Lot> handler = LotAdded;
            if (handler != null)
            {
                handler(lot);
            }
        }

        private void Remove(Lot lot)
        {
            lots.Remove(lot.Key);
            Unhash(lot);
            Action<Lot> handler = LotRemoved;
            if (handler != null)
            {
                handler(lot);
            }
        }

        private void Hash(Lot lot)
        {
            for (int cx = CellOf(lot.Bounds.MinX); cx <= CellOf(lot.Bounds.MaxX); cx++)
            {
                for (int cy = CellOf(lot.Bounds.MinY); cy <= CellOf(lot.Bounds.MaxY); cy++)
                {
                    long key = HashKey(cx, cy);
                    List<Lot> bucket;
                    if (!hash.TryGetValue(key, out bucket))
                    {
                        bucket = new List<Lot>();
                        hash[key] = bucket;
                    }

                    bucket.Add(lot);
                }
            }
        }

        private void Unhash(Lot lot)
        {
            for (int cx = CellOf(lot.Bounds.MinX); cx <= CellOf(lot.Bounds.MaxX); cx++)
            {
                for (int cy = CellOf(lot.Bounds.MinY); cy <= CellOf(lot.Bounds.MaxY); cy++)
                {
                    List<Lot> bucket;
                    if (hash.TryGetValue(HashKey(cx, cy), out bucket))
                    {
                        bucket.Remove(lot);
                    }
                }
            }
        }

        private static int CellOf(double v) { return (int)Math.Floor(v / HashCell); }
        private static long HashKey(int cx, int cy) { return ((long)cx << 32) ^ (uint)cy; }
    }
}
