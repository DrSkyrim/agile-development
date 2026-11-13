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
    ///             Roads never cross. If buildings are in the way the preview turns orange and the road is only built
    ///             (demolishing them) if Shift is held when you finish it.
    ///   Zone      Paint with a round brush; only lots beside roads exist, up to four rows deep.
    ///             Fill (toggle with F): click a lot to zone that whole side of the road, all rows, up to the next junction.
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
        private bool shownFill;
        private bool shownShift;

        private string liveText = "";
        private string resultText = "";
        private float resultUntil;

        public ToolType CurrentTool { get; private set; }
        public RoadMode RoadMode { get; private set; }

        /// <summary>While true (a menu is open) the tools ignore the keyboard and mouse.</summary>
        public bool InputBlocked { get; set; }

        /// <summary>The frame in which Esc was used to cancel something here, so the pause menu does not also open.</summary>
        public int EscapeUsedFrame { get; private set; } = -1;

        /// <summary>Street or highway: what the road tool builds next.</summary>
        public RoadType RoadType { get; private set; }

        /// <summary>When on, the zone tools fill a whole side of a road with one click instead of painting with the brush.</summary>
        public bool FillMode { get; private set; }

        public bool IsZoneTool
        {
            get { return ZoneForTool(CurrentTool) != ZoneType.None; }
        }
        public float BrushRadius { get; private set; }

        /// <summary>Short message for the toolbar: what the current aim would do, or what the last action did.</summary>
        public string StatusText
        {
            get { return Time.unscaledTime < resultUntil ? resultText : liveText; }
        }

        /// <summary>
        /// Size of the road being drawn, as text for a tag beside the cursor (empty when no road is being drawn).
        /// First line: length in metres. Second line: the same length in lots, so it can be compared with the buildings.
        /// </summary>
        public string MeasureText { get; private set; }

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
            if (tool != ToolType.Junction)
            {
                SelectedJunction = null;
            }

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

        public void SetRoadType(RoadType type)
        {
            CancelRoad();
            RoadType = type;
            HidePreview();
        }

        public void SetFillMode(bool on)
        {
            FillMode = on;
            painting = false;
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
                case ToolType.Junction: return "Junctions";
                default: return "None";
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (sim == null || rig == null || rig.Camera == null || keyboard == null || mouse == null || InputBlocked)
            {
                return;
            }

            bool roadBusy = roadStage > 0;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (roadBusy || painting || CurrentTool != ToolType.None)
                {
                    EscapeUsedFrame = Time.frameCount;
                }

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
            else if (CurrentTool == ToolType.Junction)
            {
                UpdateJunction(mouse, onMap, ground);
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

            if (keyboard.digit6Key.wasPressedThisFrame || keyboard.jKey.wasPressedThisFrame) { SetTool(ToolType.Junction); }

            if (keyboard.hKey.wasPressedThisFrame)
            {
                // Cycle through the road types.
                int next = (System.Array.IndexOf(RoadSpecs.All, RoadType) + 1) % RoadSpecs.All.Length;
                SetRoadType(RoadSpecs.All[next]);
            }

            if (keyboard.tKey.wasPressedThisFrame && CurrentTool == ToolType.Junction)
            {
                ToggleSelectedLights();
            }

            if (keyboard.fKey.wasPressedThisFrame)
            {
                SetFillMode(!FillMode);
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

        // ------------------------------------------------------------ junctions

        private const double JunctionPickRadius = 26.0;
        private static readonly Color JunctionHover = new Color(1f, 1f, 1f, 0.45f);
        private static readonly Color JunctionSelected = new Color(0.44f, 0.72f, 1f, 0.9f);

        /// <summary>The intersection picked with the Junctions tool (null if none).</summary>
        public RoadNode SelectedJunction { get; private set; }

        /// <summary>Switches the selected intersection's traffic lights on or off.</summary>
        public void ToggleSelectedLights()
        {
            if (SelectedJunction == null || !SelectedJunction.IsIntersection)
            {
                return;
            }

            if (!SelectedJunction.CanHaveTrafficLights)
            {
                ShowResult("Highway ramps flow freely: no traffic lights here");
                return;
            }

            bool turnOn = SelectedJunction.TrafficLightsOff;
            sim.SetTrafficLights(SelectedJunction, turnOn);
            ShowResult(turnOn ? "Traffic lights switched on" : "Traffic lights switched off");
        }

        private const double LaneMarkerPickRadius = 4.0;
        private const double LaneMarkerDrawRadius = 1.8;
        private static readonly Color LaneIdle = new Color(1f, 1f, 1f, 0.55f);
        private static readonly Color LaneAllowed = new Color(0.30f, 0.90f, 0.50f, 0.85f);
        private static readonly Color LaneBlocked = new Color(0.95f, 0.30f, 0.28f, 0.85f);

        /// <summary>The road whose lane is being edited at the selected junction (null if no lane is picked).</summary>
        public RoadSegment SelectedLaneRoad { get; private set; }

        /// <summary>Which lane of <see cref="SelectedLaneRoad"/> (0 = the outermost, beside the kerb).</summary>
        public int SelectedLane { get; private set; }

        /// <summary>Lets every lane at the selected junction go everywhere again.</summary>
        public void ResetSelectedLanes()
        {
            if (SelectedJunction == null)
            {
                return;
            }

            sim.ResetLaneTurns(SelectedJunction);
            ShowResult("Lane connections reset");
        }

        private double LaneMarkerDistance(RoadNode node)
        {
            return JunctionRadius(node) + 7.0;
        }

        private void SelectJunction(RoadNode node)
        {
            if (node != SelectedJunction)
            {
                SelectedLaneRoad = null;
            }

            SelectedJunction = node;
        }

        private void UpdateJunction(Mouse mouse, bool onMap, Vec2 p)
        {
            if (SelectedJunction != null && !SelectedJunction.IsIntersection)
            {
                SelectJunction(null); // a road was bulldozed and it is no longer an intersection
            }

            if (SelectedLaneRoad != null && (SelectedJunction == null || !SelectedJunction.Segments.Contains(SelectedLaneRoad)))
            {
                SelectedLaneRoad = null;
            }

            RoadNode hover = onMap ? sim.Roads.FindIntersectionNear(p, JunctionPickRadius) : null;
            if (onMap && mouse.leftButton.wasPressedThisFrame)
            {
                HandleJunctionClick(p, hover);
            }

            cityRenderer.BeginPreview();
            if (hover != null && hover != SelectedJunction)
            {
                cityRenderer.PreviewRing(hover.Position, JunctionRadius(hover) + 3.0, 1.5, JunctionHover);
            }

            if (SelectedJunction != null)
            {
                cityRenderer.PreviewRing(SelectedJunction.Position, JunctionRadius(SelectedJunction) + 3.0, 2.2, JunctionSelected);
                DrawLaneMarkers(SelectedJunction);
            }

            cityRenderer.EndPreview();
            previewShown = true;
            shownTool = ToolType.None; // so the cached-preview check never treats this as an up-to-date road/brush preview

            if (SelectedJunction == null)
            {
                liveText = "Junctions: click an intersection to select it";
            }
            else if (SelectedLaneRoad == null)
            {
                liveText = "Junction: T switches the lights. Click a lane marker (white dot) to choose where that lane can go";
            }
            else
            {
                liveText = "Lane " + (SelectedLane + 1) + " of " + RoadSpecs.Name(SelectedLaneRoad.Type).ToLowerInvariant() + ": click a green or red marker to allow or block that turn";
            }
        }

        private void HandleJunctionClick(Vec2 p, RoadNode hover)
        {
            RoadNode node = SelectedJunction;
            if (node != null)
            {
                double distance = LaneMarkerDistance(node);

                // 1. An exit marker of the picked lane: allow or block that turn.
                if (SelectedLaneRoad != null)
                {
                    foreach (RoadSegment to in node.Segments)
                    {
                        if (!node.IsTurnPossible(SelectedLaneRoad, to))
                        {
                            continue;
                        }

                        if (Vec2.Distance(LaneGeometry.LeavingSide(to, node, distance), p) <= LaneMarkerPickRadius)
                        {
                            string error;
                            bool allow = !node.IsTurnAllowed(SelectedLaneRoad, SelectedLane, to);
                            if (!sim.SetLaneTurn(node, SelectedLaneRoad, SelectedLane, to, allow, out error))
                            {
                                ShowResult(error);
                            }

                            return;
                        }
                    }
                }

                // 2. A lane marker: pick that lane (click it again to put it down).
                foreach (RoadSegment arm in node.Segments)
                {
                    int lanes = node.ArrivingLanes(arm);
                    for (int lane = 0; lane < lanes; lane++)
                    {
                        if (Vec2.Distance(LaneGeometry.ArrivingLane(arm, node, lane, distance), p) <= LaneMarkerPickRadius)
                        {
                            if (SelectedLaneRoad == arm && SelectedLane == lane)
                            {
                                SelectedLaneRoad = null;
                            }
                            else
                            {
                                SelectedLaneRoad = arm;
                                SelectedLane = lane;
                            }

                            return;
                        }
                    }
                }
            }

            // 3. Anything else: pick that intersection, or nothing.
            SelectJunction(hover);
        }

        private void DrawLaneMarkers(RoadNode node)
        {
            double distance = LaneMarkerDistance(node);
            foreach (RoadSegment arm in node.Segments)
            {
                int lanes = node.ArrivingLanes(arm);
                for (int lane = 0; lane < lanes; lane++)
                {
                    bool picked = arm == SelectedLaneRoad && lane == SelectedLane;
                    cityRenderer.PreviewDisc(LaneGeometry.ArrivingLane(arm, node, lane, distance), picked ? LaneMarkerDrawRadius * 1.35 : LaneMarkerDrawRadius, picked ? JunctionSelected : LaneIdle);
                }
            }

            if (SelectedLaneRoad == null)
            {
                return;
            }

            Vec2 from = LaneGeometry.ArrivingLane(SelectedLaneRoad, node, SelectedLane, distance);
            foreach (RoadSegment to in node.Segments)
            {
                if (!node.IsTurnPossible(SelectedLaneRoad, to))
                {
                    continue;
                }

                Vec2 exit = LaneGeometry.LeavingSide(to, node, distance);
                Color color = node.IsTurnAllowed(SelectedLaneRoad, SelectedLane, to) ? LaneAllowed : LaneBlocked;
                var path = new RoadCurve(from, node.Position, exit);
                cityRenderer.PreviewRibbon(path.Sample(2.0), 0.45, color);
                cityRenderer.PreviewDisc(exit, LaneMarkerDrawRadius * 1.25, color);
            }
        }

        private static double JunctionRadius(RoadNode node)
        {
            double widest = 0.0;
            for (int i = 0; i < node.Segments.Count; i++)
            {
                widest = System.Math.Max(widest, node.Segments[i].HalfWidth);
            }

            return widest;
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
                MeasureText = "";
                liveText = curved ? "Road: click where it starts" : "Road: press where it starts, drag, release where it ends";
            }
            else
            {
                RoadPlan plan = PlanCurrent(p);
                DrawRoadPlan(plan, p);
                liveText = DescribePlan(plan);
                MeasureText = DescribeLength(plan, p);
            }

            cityRenderer.EndPreview();
            MarkPreviewShown(p);
        }

        private static bool ShiftHeld()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        }

        private RoadPlan PlanCurrent(Vec2 end)
        {
            if (RoadMode == RoadMode.Curved && roadStage == 2)
            {
                return sim.PlanRoad(roadStart, RoadNetwork.ControlThrough(roadStart, roadBend, end), end, RoadType);
            }

            return sim.PlanRoad(roadStart, null, end, RoadType);
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

            int inTheWay = plan.Demolitions.Count;
            if (inTheWay > 0 && !ShiftHeld())
            {
                ShowResult(inTheWay + (inTheWay == 1 ? " building is" : " buildings are") + " in the way. Hold Shift while building to demolish them.");
                return;
            }

            RoadSegment segment = sim.PlaceRoad(plan, inTheWay > 0);
            if (segment == null)
            {
                ShowResult("The road could not be built");
                return;
            }

            string length = Mathf.RoundToInt((float)plan.Curve.Length) + " m";
            ShowResult(inTheWay > 0
                ? "Built a road, " + length + ", and demolished " + inTheWay + (inTheWay == 1 ? " building" : " buildings")
                : "Built a road, " + length);
        }

        private void DrawRoadPlan(RoadPlan plan, Vec2 pointer)
        {
            if (plan.IsValid)
            {
                bool demolishing = plan.Demolitions.Count > 0;
                cityRenderer.PreviewRibbon(plan.Curve.Sample(4.0), RoadSpecs.HalfWidth(RoadType), demolishing ? BulldozeHighlight : CityPalette.PreviewValid);
                foreach (Building b in plan.Demolitions)
                {
                    cityRenderer.PreviewQuad(b.Footprint, BulldozeHighlight);
                }

                DrawAnchor(plan.Start);
                DrawAnchor(plan.End);
            }
            else
            {
                Vec2 start = roadStart;
                RoadCurve raw = RoadMode == RoadMode.Curved && roadStage == 2
                    ? new RoadCurve(start, RoadNetwork.ControlThrough(start, roadBend, pointer), pointer)
                    : RoadCurve.Straight(start, pointer);
                cityRenderer.PreviewRibbon(raw.Sample(4.0), RoadSpecs.HalfWidth(RoadType), CityPalette.PreviewInvalid);
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

        private string DescribeLength(RoadPlan plan, Vec2 pointer)
        {
            double length;
            if (plan.IsValid)
            {
                length = plan.Curve.Length;
            }
            else
            {
                Vec2 start = roadStart;
                RoadCurve raw = RoadMode == RoadMode.Curved && roadStage == 2
                    ? new RoadCurve(start, RoadNetwork.ControlThrough(start, roadBend, pointer), pointer)
                    : RoadCurve.Straight(start, pointer);
                length = raw.Length;
            }

            string text = Mathf.RoundToInt((float)length) + " m";
            if (RoadSpecs.HasLots(RoadType))
            {
                text += "\n" + (length / RoadNetwork.LotSize).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " lots";
            }
            if (sim.Treasury.IsEnabled)
            {
                text += "\n" + CitySimulation.FormatMoney(sim.Treasury.RoadCost(length, RoadType));
            }

            return text;
        }

        private string DescribePlan(RoadPlan plan)
        {
            if (!plan.IsValid)
            {
                return plan.Error;
            }

            string text = (RoadMode == RoadMode.Curved ? "Curved " + RoadSpecs.Name(RoadType).ToLowerInvariant() : RoadSpecs.Name(RoadType)) + " (" + RoadSpecs.LaneLabel(RoadType) + "): " + Mathf.RoundToInt((float)plan.Curve.Length) + " m";
            int inTheWay = plan.Demolitions.Count;
            if (inTheWay > 0)
            {
                text += ShiftHeld()
                    ? "  -  will demolish " + inTheWay + (inTheWay == 1 ? " building" : " buildings")
                    : "  -  " + inTheWay + (inTheWay == 1 ? " building" : " buildings") + " in the way: hold Shift to demolish";
            }

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
                    else if (FillMode)
                    {
                        paintedCount += sim.FillZone(p, zone);
                    }
                    else
                    {
                        paintedCount += sim.PaintZone(p, BrushRadius, zone);
                    }
                }
                else if (painting && mouse.leftButton.isPressed && !(FillMode && !bulldoze))
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
                        ? (FillMode ? "Nothing to fill: click a lot beside a road" : "Nothing zoned (zones go on lots beside roads)")
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
                RoadSegment road = sim.Roads.FindSegmentNear(p, RoadSpecs.MaxHalfWidth, out length);
                if (road != null && road.IsOutsideConnection)
                {
                    liveText = "The highway to the outside world cannot be removed";
                }
                else if (road != null)
                {
                    cityRenderer.PreviewRibbon(road.Curve.Sample(4.0), road.HalfWidth, BulldozeHighlight);
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

            if (FillMode)
            {
                List<Lot> fill = sim.LotsToFill(p);
                if (fill == null)
                {
                    liveText = ToolLabel(CurrentTool) + " fill: point at a lot beside a road";
                    return;
                }

                int fillChanging = 0;
                foreach (Lot lot in fill)
                {
                    bool already = lot.Zone == zone;
                    cityRenderer.PreviewQuad(lot.Corners, already ? CityPalette.PreviewNeutral : CityPalette.ZonePreview(zone));
                    if (!already)
                    {
                        fillChanging++;
                    }
                }

                liveText = ToolLabel(CurrentTool) + " fill: click to zone " + fillChanging + " lots along this side of the road";
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
                && shownFill == FillMode
                && shownShift == ShiftHeld()
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
            shownFill = FillMode;
            shownShift = ShiftHeld();
            shownRevision = sim.Revision;
            shownBrush = BrushRadius;
        }

        private void HidePreview()
        {
            MeasureText = "";
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
