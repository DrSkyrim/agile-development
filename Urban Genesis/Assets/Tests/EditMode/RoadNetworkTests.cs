using System.Collections.Generic;
using NUnit.Framework;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Tests
{
    [TestFixture]
    public class RoadNetworkTests
    {
        private static RoadNetwork NewNetwork()
        {
            return new RoadNetwork(new Aabb(-512, -512, 512, 512));
        }

        private static RoadSegment Build(RoadNetwork net, Vec2 a, Vec2 b)
        {
            RoadPlan plan = net.PlanStraight(a, b);
            Assert.IsTrue(plan.IsValid, plan.Error);
            return net.AddRoad(plan);
        }

        [Test]
        public void AStraightRoad_CreatesOneSegmentAndTwoNodes()
        {
            RoadNetwork net = NewNetwork();
            RoadSegment s = Build(net, new Vec2(-200, 0), new Vec2(200, 0));

            Assert.AreEqual(1, net.SegmentCount);
            Assert.AreEqual(2, net.NodeCount);
            Assert.AreEqual(400.0, s.Length, 0.01);
            Assert.AreEqual(1f, s.OpenFraction);
        }

        [Test]
        public void RoadWidth_IsTwoOldGridTiles()
        {
            Assert.AreEqual(16.0, RoadNetwork.RoadWidth);
        }

        [Test]
        public void ATooShortRoad_IsRefused()
        {
            RoadPlan plan = NewNetwork().PlanStraight(new Vec2(0, 0), new Vec2(20, 0));
            Assert.IsFalse(plan.IsValid);
            StringAssert.Contains("short", plan.Error);
        }

        [Test]
        public void ATooSharpBend_IsRefused()
        {
            RoadPlan plan = NewNetwork().Plan(new Vec2(0, 0), new Vec2(0, 1), new Vec2(60, 0));
            Assert.IsFalse(plan.IsValid);
            StringAssert.Contains("bend", plan.Error);
        }

        [Test]
        public void AGentleCurve_IsAccepted_AndPassesThroughTheClickedPoint()
        {
            RoadNetwork net = NewNetwork();
            Vec2 start = new Vec2(-100, 0);
            Vec2 through = new Vec2(0, 60);
            Vec2 end = new Vec2(100, 0);
            RoadPlan plan = net.Plan(start, RoadNetwork.ControlThrough(start, through, end), end);

            Assert.IsTrue(plan.IsValid, plan.Error);
            Assert.AreEqual(0.0, Vec2.Distance(plan.Curve.Point(0.5), through), 1e-6);
        }

        [Test]
        public void ARoadOffTheMap_IsRefused()
        {
            Assert.IsFalse(NewNetwork().PlanStraight(new Vec2(0, 0), new Vec2(600, 0)).IsValid);
        }

        [Test]
        public void JoiningTheMiddleOfARoad_SplitsItOnTheLotLattice()
        {
            RoadNetwork net = NewNetwork();
            Build(net, new Vec2(-200, 0), new Vec2(200, 0));

            var splits = new List<double>();
            net.SegmentSplit += (old, first, second, length) => splits.Add(length);

            RoadPlan plan = net.PlanStraight(new Vec2(3, -150), new Vec2(2, 3));
            Assert.IsTrue(plan.IsValid, plan.Error);
            Assert.AreEqual(AnchorKind.Split, plan.End.Kind);
            net.AddRoad(plan);

            Assert.AreEqual(1, splits.Count);
            Assert.AreEqual(0.0, splits[0] % RoadNetwork.LotSize, 1e-9, "split must land on a multiple of the lot size");
            Assert.AreEqual(3, net.SegmentCount);
            Assert.AreEqual(4, net.NodeCount);

            RoadNode junction = net.FindNodeNear(plan.End.Position, 1.0);
            Assert.IsNotNull(junction);
            Assert.AreEqual(3, junction.Degree);
        }

        [Test]
        public void ClickingJustBeyondTheNodeSnapRadius_SplitsTheRoadAtTheNearestAllowedSpot()
        {
            RoadNetwork net = NewNetwork();
            Build(net, new Vec2(-200, 0), new Vec2(200, 0));

            RoadPlan plan = net.PlanStraight(new Vec2(-180, -100), new Vec2(-186, 3));
            Assert.IsTrue(plan.IsValid, plan.Error);
            Assert.AreEqual(AnchorKind.Split, plan.End.Kind);
            Assert.AreEqual(16.0, plan.End.SplitLength, 1e-9, "never closer than the minimum piece length");
        }

        [Test]
        public void ClickingNearAnExistingNode_JoinsThatNode()
        {
            RoadNetwork net = NewNetwork();
            Build(net, new Vec2(-200, 0), new Vec2(0, 0));
            RoadPlan plan = net.PlanStraight(new Vec2(5, 4), new Vec2(120, 80));

            Assert.IsTrue(plan.IsValid, plan.Error);
            Assert.AreEqual(AnchorKind.Node, plan.Start.Kind);
            net.AddRoad(plan);
            Assert.AreEqual(3, net.NodeCount);
            Assert.AreEqual(2, net.SegmentCount);
        }

        [Test]
        public void ARoadCrossingAnother_IsRefused()
        {
            RoadNetwork net = NewNetwork();
            Build(net, new Vec2(-200, 0), new Vec2(200, 0));

            RoadPlan plan = net.PlanStraight(new Vec2(-100, -100), new Vec2(-100, 100));
            Assert.IsFalse(plan.IsValid);
            StringAssert.Contains("crossing", plan.Error);
        }

        [Test]
        public void ARoadRunningTooCloseAlongsideAnother_IsRefused_ButFarAwayIsFine()
        {
            RoadNetwork net = NewNetwork();
            Build(net, new Vec2(-200, 0), new Vec2(200, 0));

            Assert.IsFalse(net.PlanStraight(new Vec2(-150, -10), new Vec2(-60, -10)).IsValid);
            Assert.IsTrue(net.PlanStraight(new Vec2(-150, -60), new Vec2(-60, -60)).IsValid);
        }

        [Test]
        public void ARoadLeavingAJunctionAtAShallowAngle_IsRefused()
        {
            RoadNetwork net = NewNetwork();
            Build(net, new Vec2(-200, 0), new Vec2(0, 0));

            Assert.IsFalse(net.PlanStraight(new Vec2(0, 0), new Vec2(-110, 40)).IsValid);
            Assert.IsTrue(net.PlanStraight(new Vec2(0, 0), new Vec2(100, 80)).IsValid);
            Assert.IsTrue(net.PlanStraight(new Vec2(0, 0), new Vec2(200, 0)).IsValid, "continuing straight on is fine");
        }

        [Test]
        public void BothEndsOnTheSameRoad_SplitsItTwice()
        {
            RoadNetwork net = NewNetwork();
            Build(net, new Vec2(-200, 0), new Vec2(200, 0));

            Vec2 a = new Vec2(-80, 2);
            Vec2 b = new Vec2(80, 2);
            RoadPlan plan = net.Plan(a, RoadNetwork.ControlThrough(a, new Vec2(0, 60), b), b);
            Assert.IsTrue(plan.IsValid, plan.Error);

            Assert.IsNotNull(net.AddRoad(plan));
            Assert.AreEqual(4, net.SegmentCount);
            Assert.AreEqual(4, net.NodeCount);
        }

        [Test]
        public void RemovingASegment_RemovesNodesThatHaveNoRoadsLeft()
        {
            RoadNetwork net = NewNetwork();
            RoadSegment s = Build(net, new Vec2(-100, 0), new Vec2(100, 0));
            RoadSegment removed = null;
            net.SegmentRemoved += seg => removed = seg;

            Assert.IsTrue(net.RemoveSegment(s));
            Assert.AreSame(s, removed);
            Assert.AreEqual(0, net.SegmentCount);
            Assert.AreEqual(0, net.NodeCount);
            Assert.IsFalse(net.RemoveSegment(s));
        }

        [Test]
        public void ASplitKeepsTheClosureState()
        {
            RoadNetwork net = NewNetwork();
            RoadSegment s = Build(net, new Vec2(-200, 0), new Vec2(200, 0));
            s.OpenFraction = 0.25f;
            Build(net, new Vec2(0, -150), new Vec2(0, 3));

            foreach (RoadSegment seg in net.Segments)
            {
                if (seg.A.Position.Y == 0.0 && seg.B.Position.Y == 0.0)
                {
                    Assert.AreEqual(0.25f, seg.OpenFraction, "both halves keep the closure");
                }
            }
        }

        [Test]
        public void OpenFraction_IsClampedAndDrivesCapacity()
        {
            RoadNetwork net = NewNetwork();
            RoadSegment s = Build(net, new Vec2(-100, 0), new Vec2(100, 0));

            s.OpenFraction = 2f;
            Assert.AreEqual(1f, s.OpenFraction);
            s.OpenFraction = 0.5f;
            Assert.AreEqual(s.BaseCapacity * 0.5f, s.EffectiveCapacity, 1e-6f);
            s.OpenFraction = -1f;
            Assert.IsTrue(s.IsClosed);
        }

        [Test]
        public void IsOnRoad_IsTrueOnlyOnTheSurface()
        {
            RoadNetwork net = NewNetwork();
            Build(net, new Vec2(-100, 0), new Vec2(100, 0));

            Assert.IsTrue(net.IsOnRoad(new Vec2(0, 7)));
            Assert.IsFalse(net.IsOnRoad(new Vec2(0, 9)));
        }
    }
}
