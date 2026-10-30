using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Game
{
    /// <summary>How the road tool draws.</summary>
    public enum RoadMode
    {
        /// <summary>Press where the road starts, drag, release where it ends.</summary>
        Straight,

        /// <summary>Click the start, click a point the road should bend through, click the end.</summary>
        Curved
    }

    /// <summary>
    /// Turns mouse input into commands for the simulation. Pick a tool (toolbar or keys 1 to 5):
    ///   Road      Straight: press, drag, release.  Curved (toggle with C): click start, click a point to bend through, click end.
    ///             A road can start or end in open ground, on a junction, or on another road (it joins there).
    ///   Zone      Paint with a round brush; only lots beside roads exist, up to four rows deep.
    ///   Bulldoze  Paint to clear buildings and zoning; click a road to remove it.
    /// [ and ] change the brush size. While you aim, a coloured preview shows what will happen.
    /// Esc (or right-click) cancels a road in progress, or drops the tool.
    /// </summary>
    public sealed class ToolController : MonoBehaviour
    {
        public const float MinBrush = 4f;
        public const float MaxBrush = 48f;
        private const float BrushStep = 4f;
        private const float ResultMessageSeconds = 3f;

        private static readonly Color BulldozeHighlight = new Color(1.00f, 0.45f, 0.20f, 0.60f);

        private CitySimulation sim;
        private CameraRig rig;
        private CityRenderer cityRenderer;
        private HudToolbar hud;

        // Road drawing
        private int roadStage;            // 0 = nothing placed, 1 = start placed, 2 = start and bend placed (curved only)
        private Vec2 roadStart;
        private Vec2 roadBend;

        // Painting (zone and bulldoze)
        private bool painting;
        private int paintedCount;

        // What the preview currently shows, so it is only rebuilt when something changed.
        private bool previewShown;
        private Vec2 shownPoint;
        private int shownRevision = -1;
        private int shownStage = -1;
        private ToolType shownTool;
        private float shownBrush;
        private RoadMode shownMode;

        private string liveText = "";
        private string resultText = "";
        private float resultUntil;

        public ToolType CurrentTool { get; private set; }
        public RoadMode RoadMode { get; private set; }
        public float BrushRadius { get; private set; }

        /// <summary>Short message for the toolbar: what the current aim would do, or what the last action did.</summary>
        public string StatusText
        {
            get { return Time.unscaledTime < resultUntil ? resultText : liveText; }
        }

        public bool IsBrushTool
        {
            get { return CurrentTool == ToolType.Bulldoze || ZoneForTool(CurrentTool) != ZoneType.None; }
        }

        public void Init(CitySimulation simulation, CameraRig cameraRig, CityRenderer renderer, HudToolbar toolbar)
        {
            sim = simulation;
            rig = cameraRig;
            cityRenderer = renderer;
            hud = toolbar;
            BrushRadius = 12f;
        }

        public void SetTool(ToolType tool)
        {
            CancelRoad();
            painting = false;
            CurrentTool = tool;
            liveText = "";
            HidePreview();
            cityRenderer.SetLotGridVisible(IsBrushTool);
        }

        public void SetRoadMode(RoadMode mode)
        {
            CancelRoad();
            RoadMode = mode;
            HidePreview();
        }

        public void ChangeBrush(int direction)
        {
            BrushRadius = Mathf.Clamp(BrushRadius + direction * BrushStep, MinBrush, MaxBrush);
            shownBrush = -1f;
        }

        public static string ToolLabel(ToolType tool)
        {
            switch (tool)
            {
                case ToolType.Road: return "Road";
                case ToolType.ZoneResidential: return "Residential";
                case ToolType.ZoneCommercial: return "Commercial";
                case ToolType.ZoneIndustrial: return "Industrial";
                case ToolType.Bulldoze: return "Bulldoze";
                default: return "None";
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (sim == null || rig == null || rig.Camera == null || keyboard == null || mouse == null)
            {
                return;
            }

            bool roadBusy = roadStage > 0;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (roadBusy || painting)
                {
                    CancelRoad();
                    painting = false;
                    liveText = "";
                    HidePreview();
                }
                else
                {
                    SetTool(ToolType.None);
                }
            }

            if (mouse.rightButton.wasPressedThisFrame && roadBusy)
            {
                CancelRoad();
                HidePreview();
            }

            if (!roadBusy && !painting)
            {
                HandleHotkeys(keyboard);
            }

            if (CurrentTool == ToolType.None)
            {
                return;
            }

            Vector2 pointer = mouse.position.ReadValue();
            bool overHud = hud != null && hud.ContainsScreenPoint(pointer);

            Vec2 ground;
            bool picked = TryPick(pointer, out ground);
            bool onMap = picked && !overHud && sim.Roads.WorldBounds.Contains(ground);

            if (CurrentTool == ToolType.Road)
            {
                UpdateRoad(mouse, onMap, ground);
            }
            else
            {
                UpdateBrush(mouse, onMap, ground);
            }
        }

        private void HandleHotkeys(Keyboard keyboard)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) { SetTool(ToolType.Road); }
            else if (keyboard.digit2Key.wasPressedThisFrame) { SetTool(ToolType.ZoneResidential); }
            else if (keyboard.digit3Key.wasPressedThisFrame) { SetTool(ToolType.ZoneCommercial); }
            else if (keyboard.digit4Key.wasPressedThisFrame) { SetTool(ToolType.ZoneIndustrial); }
            else if (keyboard.digit5Key.wasPressedThisFrame) { SetTool(ToolType.Bulldoze); }

            if (keyboard.cKey.wasPressedThisFrame)
            {
                SetRoadMode(RoadMode == RoadMode.Straight ? RoadMode.Curved : RoadMode.Straight);
            }

            if (keyboard.leftBracketKey.wasPressedThisFrame) { ChangeBrush(-1); }
            if (keyboard.rightBracketKey.wasPressedThisFrame) { ChangeBrush(1); }
        }

        // ------------------------------------------------------------ picking

        private bool TryPick(Vector2 screenPoint, out Vec2 ground)
        {
            Ray ray = rig.Camera.ScreenPointToRay(screenPoint);
            var plane = new Plane(Vector3.up, Vector3.zero);

            float distance;
            if (plane.Raycast(ray, out distance))
            {
                ground = CityMetrics.ToGround(ray.GetPoint(distance));
                return true;
            }

            ground = Vec2.Zero;
            return false;
        }

        // ------------------------------------------------------------ roads

        private void UpdateRoad(Mouse mouse, bool onMap, Vec2 p)
        {
            // A straight road is drawn while the button is held. If it was let go somewhere we could not react (off the map, over the toolbar), drop it.
            if (RoadMode == RoadMode.Straight && roadStage == 1 && !mouse.leftButton.isPressed && !mouse.leftButton.wasPressedThisFrame && !(onMap && mouse.leftButton.wasReleasedThisFrame))
            {
                CancelRoad();
                HidePreview();
                return;
            }

            if (!onMap)
            {
                if (roadStage == 0)
                {
                    liveText = "";
                    HidePreview();
                }

                return;
            }

            bool curved = RoadMode == RoadMode.Curved;
            bool pressed = mouse.leftButton.wasPressedThisFrame;
            bool released = mouse.leftButton.wasReleasedThisFrame;

            if (roadStage == 0)
            {
                if (pressed)
                {
                    roadStart = p;
                    roadStage = 1;
                }
            }
            else if (curved)
            {
                if (pressed)
                {
                    if (roadStage == 1)
                    {
                        roadBend = p;
                        roadStage = 2;
                    }
                    else
                    {
                        BuildRoad(p);
                        return;
                    }
                }
            }
            else if (released)
            {
                BuildRoad(p);
                return;
            }

            if (PreviewUpToDate(p))
            {
                return;
            }

            cityRenderer.BeginPreview();
            if (roadStage == 0)
            {
                DrawAnchorHint(p);
                liveText = curved ? "Road: click where it starts" : "Road: press where it starts, drag, release where it ends";
            }
            else
            {
                RoadPlan plan = PlanCurrent(p);
                DrawRoadPlan(plan, p);
                liveText = DescribePlan(plan);
            }

            cityRenderer.EndPreview();
            MarkPreviewShown(p);
        }

        private RoadPlan PlanCurrent(Vec2 end)
        {
            if (RoadMode == RoadMode.Curved && roadStage == 2)
            {
                return sim.PlanRoad(roadStart, RoadNetwork.ControlThrough(roadStart, roadBend, end), end);
            }

            return sim.PlanRoad(roadStart, null, end);
        }

        private void BuildRoad(Vec2 end)
        {
            RoadPlan plan = PlanCurrent(end);
            CancelRoad();
            HidePreview();

            if (!plan.IsValid)
            {
                ShowResult(plan.Error);
                return;
            }

            RoadSegment segment = sim.PlaceRoad(plan);
            ShowResult(segment == null ? "The road could not be built" : "Built a road, " + Mathf.RoundToInt((float)plan.Curve.Length) + " m");
        }

        private void DrawRoadPlan(RoadPlan plan, Vec2 pointer)
        {
            if (plan.IsValid)
            {
                cityRenderer.PreviewRibbon(plan.Curve.Sample(4.0), RoadNetwork.HalfWidth, CityPalette.PreviewValid);
                DrawAnchor(plan.Start);
                DrawAnchor(plan.End);
            }
            else
            {
                Vec2 start = roadStart;
                RoadCurve raw = RoadMode == RoadMode.Curved && roadStage == 2
                    ? new RoadCurve(start, RoadNetwork.ControlThrough(start, roadBend, pointer), pointer)
                    : RoadCurve.Straight(start, pointer);
                cityRenderer.PreviewRibbon(raw.Sample(4.0), RoadNetwork.HalfWidth, CityPalette.PreviewInvalid);
            }

            if (RoadMode == RoadMode.Curved && roadStage == 2)
            {
                cityRenderer.PreviewDisc(roadBend, 2.5, CityPalette.PreviewHover);
            }
        }

        private void DrawAnchor(RoadAnchor anchor)
        {
            if (anchor.Kind != AnchorKind.New)
            {
                cityRenderer.PreviewRing(anchor.Position, anchor.Kind == AnchorKind.Node ? 11.0 : 9.0, 0.8, Color.white);
            }
        }

        /// <summary>Before the road starts: shows where a click would attach (a junction, a road, or open ground).</summary>
        private void DrawAnchorHint(Vec2 p)
        {
            RoadNode node = sim.Roads.FindNodeNear(p, RoadNetwork.NodeSnapRadius);
            if (node != null)
            {
                cityRenderer.PreviewRing(node.Position, 11.0, 0.8, Color.white);
                return;
            }

            double length;
            RoadSegment segment = sim.Roads.FindSegmentNear(p, RoadNetwork.SegmentJoinRadius, out length);
            if (segment != null)
            {
                double snapped = Math.Round(length / RoadNetwork.LotSize) * RoadNetwork.LotSize;
                cityRenderer.PreviewRing(segment.Curve.PointAtLength(snapped), 9.0, 0.8, Color.white);
                return;
            }

            cityRenderer.PreviewDisc(p, 3.0, CityPalette.PreviewHover);
        }

        private string DescribePlan(RoadPlan plan)
        {
            if (!plan.IsValid)
            {
                return plan.Error;
            }

            string text = (RoadMode == RoadMode.Curved ? "Curved road" : "Road") + ": " + Mathf.RoundToInt((float)plan.Curve.Length) + " m";
            if (plan.Start.Kind != AnchorKind.New || plan.End.Kind != AnchorKind.New)
            {
                text += " (joins the road network)";
            }

            if (RoadMode == RoadMode.Curved && roadStage == 1)
            {
                text += "  -  click a point for it to bend through";
            }
            else if (RoadMode == RoadMode.Curved)
            {
                text += "  -  click to build";
            }

            return text;
        }

        private void CancelRoad()
        {
            roadStage = 0;
        }

        // ------------------------------------------------------------ zone and bulldoze brush

        private void UpdateBrush(Mouse mouse, bool onMap, Vec2 p)
        {
            ZoneType zone = ZoneForTool(CurrentTool);
            bool bulldoze = CurrentTool == ToolType.Bulldoze;

            if (!onMap)
            {
                if (!painting)
                {
                    liveText = "";
                    HidePreview();
                }
            }
            else
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    painting = true;
                    paintedCount = 0;
                    if (bulldoze)
                    {
                        paintedCount += sim.Bulldoze(p, BrushRadius, true);
                    }
                    else
                    {
                        paintedCount += sim.PaintZone(p, BrushRadius, zone);
                    }
                }
                else if (painting && mouse.leftButton.isPressed)
                {
                    paintedCount += bulldoze ? sim.Bulldoze(p, BrushRadius, false) : sim.PaintZone(p, BrushRadius, zone);
                }
            }

            if (painting && (mouse.leftButton.wasReleasedThisFrame || !mouse.leftButton.isPressed))
            {
                painting = false;
                if (bulldoze)
                {
                    ShowResult(paintedCount == 0 ? "Nothing to clear" : "Cleared " + paintedCount + " things");
                }
                else
                {
                    ShowResult(paintedCount == 0
                        ? "Nothing zoned (zones go on lots beside roads)"
                        : "Zoned " + paintedCount + " lots " + ToolLabel(CurrentTool).ToLowerInvariant());
                }
            }

            if (!onMap || PreviewUpToDate(p))
            {
                return;
            }

            cityRenderer.BeginPreview();
            DrawBrushPreview(p, zone, bulldoze);
            cityRenderer.EndPreview();
            MarkPreviewShown(p);
        }

        private void DrawBrushPreview(Vec2 p, ZoneType zone, bool bulldoze)
        {
            double radius = BrushRadius;
            List<Lot> lots = sim.Lots.LotsInCircle(p, radius);

            if (bulldoze)
            {
                double length;
                RoadSegment road = sim.Roads.FindSegmentNear(p, RoadNetwork.HalfWidth, out length);
                if (road != null)
                {
                    cityRenderer.PreviewRibbon(road.Curve.Sample(4.0), RoadNetwork.HalfWidth, BulldozeHighlight);
                    liveText = "Bulldoze: click to remove this road (and everything beside it)";
                }
                else
                {
                    liveText = lots.Count == 0 ? "Bulldoze: nothing here" : "Bulldoze: press and drag to clear buildings and zoning";
                }

                foreach (Lot lot in lots)
                {
                    if (lot.Zone != ZoneType.None)
                    {
                        cityRenderer.PreviewQuad(lot.Corners, BulldozeHighlight);
                    }
                }

                foreach (Building b in sim.Buildings)
                {
                    if (b.Bounds.Overlaps(new Aabb(p.X - radius, p.Y - radius, p.X + radius, p.Y + radius)) && DistanceToFootprint(p, b) <= radius)
                    {
                        cityRenderer.PreviewQuad(b.Footprint, BulldozeHighlight);
                    }
                }

                cityRenderer.PreviewRing(p, radius, 0.7, BulldozeHighlight);
                return;
            }

            int changing = 0;
            foreach (Lot lot in lots)
            {
                if (lot.Zone == zone)
                {
                    cityRenderer.PreviewQuad(lot.Corners, CityPalette.PreviewNeutral);
                }
                else
                {
                    cityRenderer.PreviewQuad(lot.Corners, CityPalette.ZonePreview(zone));
                    changing++;
                }
            }

            cityRenderer.PreviewRing(p, radius, 0.7, CityPalette.ZonePreview(zone));
            liveText = lots.Count == 0
                ? ToolLabel(CurrentTool) + ": no lots here (zones go beside roads)"
                : ToolLabel(CurrentTool) + ": " + changing + " lots to zone  -  press and drag to paint, [ ] changes the brush";
        }

        private static double DistanceToFootprint(Vec2 p, Building b)
        {
            if (Geometry.PointInConvex(p, b.Footprint))
            {
                return 0.0;
            }

            double best = double.MaxValue;
            for (int i = 0; i < b.Footprint.Length; i++)
            {
                best = Math.Min(best, Geometry.DistanceToSegment(p, b.Footprint[i], b.Footprint[(i + 1) % b.Footprint.Length]));
            }

            return best;
        }

        // ------------------------------------------------------------ preview bookkeeping

        private bool PreviewUpToDate(Vec2 p)
        {
            return previewShown
                && shownTool == CurrentTool
                && shownStage == roadStage
                && shownMode == RoadMode
                && shownRevision == sim.Revision
                && Mathf.Approximately(shownBrush, BrushRadius)
                && Vec2.Distance(shownPoint, p) < 0.25;
        }

        private void MarkPreviewShown(Vec2 p)
        {
            previewShown = true;
            shownPoint = p;
            shownTool = CurrentTool;
            shownStage = roadStage;
            shownMode = RoadMode;
            shownRevision = sim.Revision;
            shownBrush = BrushRadius;
        }

        private void HidePreview()
        {
            if (previewShown)
            {
                cityRenderer.ClearPreview();
                previewShown = false;
            }
        }

        private void ShowResult(string text)
        {
            resultText = text;
            resultUntil = Time.unscaledTime + ResultMessageSeconds;
        }

        private static ZoneType ZoneForTool(ToolType tool)
        {
            switch (tool)
            {
                case ToolType.ZoneResidential: return ZoneType.Residential;
                case ToolType.ZoneCommercial: return ZoneType.Commercial;
                case ToolType.ZoneIndustrial: return ZoneType.Industrial;
                default: return ZoneType.None;
            }
        }
    }
}
