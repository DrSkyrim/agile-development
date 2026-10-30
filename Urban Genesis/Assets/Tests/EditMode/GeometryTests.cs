using NUnit.Framework;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Tests
{
    [TestFixture]
    public class GeometryTests
    {
        private static Vec2[] Square(double x, double y, double size)
        {
            return new[] { new Vec2(x, y), new Vec2(x + size, y), new Vec2(x + size, y + size), new Vec2(x, y + size) };
        }

        [Test]
        public void ConvexOverlap_OverlappingSquares_IsTrue()
        {
            Assert.IsTrue(Geometry.ConvexOverlap(Square(0, 0, 8), Square(4, 4, 8), 0.01));
        }

        [Test]
        public void ConvexOverlap_SquaresThatOnlyTouch_IsFalse()
        {
            Assert.IsFalse(Geometry.ConvexOverlap(Square(0, 0, 8), Square(8, 0, 8), 0.01));
        }

        [Test]
        public void ConvexOverlap_SliverWithinTolerance_IsFalse()
        {
            Assert.IsFalse(Geometry.ConvexOverlap(Square(0, 0, 8), Square(7.5, 0, 8), 0.8));
            Assert.IsTrue(Geometry.ConvexOverlap(Square(0, 0, 8), Square(6.5, 0, 8), 0.8));
        }

        [Test]
        public void ConvexOverlap_RotatedBoxes_AreHandled()
        {
            var diamond = new[] { new Vec2(10, 0), new Vec2(20, 10), new Vec2(10, 20), new Vec2(0, 10) };
            Assert.IsTrue(Geometry.ConvexOverlap(diamond, Square(8, 8, 4), 0.01));
            Assert.IsFalse(Geometry.ConvexOverlap(diamond, Square(0, 0, 3), 0.01));
        }

        [Test]
        public void SignedArea_IsPositiveForAnticlockwiseAndNegativeForClockwise()
        {
            Vec2[] square = Square(0, 0, 8);
            Assert.AreEqual(64.0, Geometry.SignedArea(square), 1e-9);
            Assert.AreEqual(-64.0, Geometry.SignedArea(new[] { square[3], square[2], square[1], square[0] }), 1e-9);
            Assert.IsTrue(Geometry.IsConvexAnticlockwise(Geometry.Anticlockwise(new[] { square[3], square[2], square[1], square[0] })));
        }

        [Test]
        public void PointInConvex_InsideEdgeAndOutside()
        {
            Vec2[] square = Square(0, 0, 8);
            Assert.IsTrue(Geometry.PointInConvex(new Vec2(4, 4), square));
            Assert.IsTrue(Geometry.PointInConvex(new Vec2(8, 4), square));
            Assert.IsFalse(Geometry.PointInConvex(new Vec2(9, 4), square));
        }

        [Test]
        public void DistanceToSegment_ClampsToTheEnds()
        {
            Assert.AreEqual(5.0, Geometry.DistanceToSegment(new Vec2(5, 5), new Vec2(0, 0), new Vec2(10, 0)), 1e-9);
            Assert.AreEqual(5.0, Geometry.DistanceToSegment(new Vec2(-3, 4), new Vec2(0, 0), new Vec2(10, 0)), 1e-9);
        }
    }

    [TestFixture]
    public class RoadCurveTests
    {
        [Test]
        public void Straight_HasTheChordLengthAndAHugeRadius()
        {
            RoadCurve c = RoadCurve.Straight(new Vec2(0, 0), new Vec2(100, 0));
            Assert.AreEqual(100.0, c.Length, 1e-6);
            Assert.IsTrue(c.IsStraight);
            Assert.Greater(c.MinRadius(), 1e6);
        }

        [Test]
        public void Curved_IsLongerThanItsChordAndHasTheExpectedRadius()
        {
            var c = new RoadCurve(new Vec2(0, 0), new Vec2(50, 50), new Vec2(100, 0));
            Assert.Greater(c.Length, 110.0);
            Assert.IsFalse(c.IsStraight);
            Assert.AreEqual(50.0, c.MinRadius(), 5.0);
        }

        [Test]
        public void OffsetPoint_PositiveIsLeftOfTheDirectionOfTravel()
        {
            RoadCurve c = RoadCurve.Straight(new Vec2(0, 0), new Vec2(100, 0));
            Vec2 left = c.OffsetPointAtLength(50.0, 8.0);
            Vec2 right = c.OffsetPointAtLength(50.0, -8.0);
            Assert.AreEqual(8.0, left.Y, 1e-6);
            Assert.AreEqual(-8.0, right.Y, 1e-6);
        }

        [Test]
        public void Split_PiecesAddUpAndMeetAtTheSplitPoint()
        {
            var c = new RoadCurve(new Vec2(0, 0), new Vec2(50, 50), new Vec2(100, 0));
            RoadCurve a;
            RoadCurve b;
            c.SplitAtLength(c.Length / 2.0, out a, out b);

            Assert.AreEqual(c.Length, a.Length + b.Length, 0.05);
            Assert.AreEqual(0.0, Vec2.Distance(a.P2, b.P0), 1e-9);
            Assert.AreEqual(0.0, Vec2.Distance(a.P2, c.PointAtLength(c.Length / 2.0)), 0.05);
        }

        [Test]
        public void ClosestLength_FindsTheNearestPoint()
        {
            var c = new RoadCurve(new Vec2(0, 0), new Vec2(50, 50), new Vec2(100, 0));
            double distance;
            double length = c.ClosestLength(new Vec2(50, 30), out distance);
            Assert.AreEqual(c.Length / 2.0, length, 0.5);
            Assert.AreEqual(5.0, distance, 0.5);
        }

        [Test]
        public void ArcLength_IsEvenlySpacedAlongACurve()
        {
            var c = new RoadCurve(new Vec2(0, 0), new Vec2(10, 120), new Vec2(100, 0));
            double step = c.Length / 20.0;
            for (int i = 0; i < 20; i++)
            {
                double d = Vec2.Distance(c.PointAtLength(i * step), c.PointAtLength((i + 1) * step));
                Assert.AreEqual(step, d, step * 0.12, "pieces " + i);
            }
        }

        [Test]
        public void MinRadius_OfAFoldedCurve_IsTiny()
        {
            Assert.Less(new RoadCurve(new Vec2(0, 0), new Vec2(100, 0), new Vec2(0, 1)).MinRadius(), 5.0);
        }
    }
}
