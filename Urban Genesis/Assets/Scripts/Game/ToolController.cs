using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Game
{
    /// <summary>
    /// Turns mouse input into commands for the simulation.
    /// Pick a tool (toolbar or keys 1 to 5), then press and drag in the world and release to apply:
    ///   Road      drags a straight line of road
    ///   Zone      drags a rectangle of zoning (only cells close to a road can be zoned)
    ///   Bulldoze  drags a rectangle to clear
    /// While dragging, a coloured preview shows what will happen. Esc cancels the drag, or drops the tool.
    /// </summary>
    public sealed class ToolController : MonoBehaviour
    {
        private const float ResultMessageSeconds = 2.5f;

        private CitySimulation sim;
        private CameraRig rig;
        private CityRenderer cityRenderer;
        private HudToolbar hud;

        private bool dragging;
        private GridCoord dragStart;
        private GridCoord dragEnd;

        // Remembers what the overlay currently shows, so it is only rebuilt when something changed.
        private bool overlayShown;
        private ToolType shownTool;
        private bool shownDragging;
        private GridCoord shownA;
        private GridCoord shownB;
        private int shownTick = -1;

        private readonly List<GridCoord> previewCells = new List<GridCoord>();
        private readonly List<Color> previewColors = new List<Color>();

        private string liveText = "";
        private string resultText = "";
        private float resultUntil;

        public ToolType CurrentTool { get; private set; }

        /// <summary>Short message for the toolbar: what the current drag would do, or what the last action did.</summary>
        public string StatusText
        {
            get { return Time.unscaledTime < resultUntil ? resultText : liveText; }
        }

        public void Init(CitySimulation simulation, CameraRig cameraRig, CityRenderer renderer, HudToolbar toolbar)
        {
            sim = simulation;
            rig = cameraRig;
            cityRenderer = renderer;
            hud = toolbar;
        }

        public void SetTool(ToolType tool)
        {
            if (dragging)
            {
                CancelDrag();
            }

            CurrentTool = tool;
            liveText = "";
            HideOverlay();
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

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (dragging)
                {
                    CancelDrag();
                }
                else
                {
                    SetTool(ToolType.None);
                }
            }

            if (!dragging)
            {
                HandleToolHotkeys(keyboard);
            }

            if (CurrentTool == ToolType.None)
            {
                return;
            }

            Vector2 pointer = mouse.position.ReadValue();
            bool overHud = hud != null && hud.ContainsScreenPoint(pointer);

            GridCoord cell;
            bool hasCell = TryGetCell(pointer, out cell);

            if (!dragging)
            {
                if (mouse.leftButton.wasPressedThisFrame && !overHud && hasCell && sim.Grid.InBounds(cell))
                {
                    dragging = true;
                    dragStart = cell;
                    dragEnd = cell;
                }
                else
                {
                    UpdateHover(overHud, hasCell, cell);
                    return;
                }
            }

            if (hasCell)
            {
                dragEnd = ClampToGrid(cell);
            }

            UpdatePreview();

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                ApplyTool();
                dragging = false;
                HideOverlay();
            }
        }

        private void HandleToolHotkeys(Keyboard keyboard)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) { SetTool(ToolType.Road); }
            else if (keyboard.digit2Key.wasPressedThisFrame) { SetTool(ToolType.ZoneResidential); }
            else if (keyboard.digit3Key.wasPressedThisFrame) { SetTool(ToolType.ZoneCommercial); }
            else if (keyboard.digit4Key.wasPressedThisFrame) { SetTool(ToolType.ZoneIndustrial); }
            else if (keyboard.digit5Key.wasPressedThisFrame) { SetTool(ToolType.Bulldoze); }
        }

        // ------------------------------------------------------------ picking

        private bool TryGetCell(Vector2 screenPoint, out GridCoord cell)
        {
            Ray ray = rig.Camera.ScreenPointToRay(screenPoint);
            var ground = new Plane(Vector3.up, Vector3.zero);

            float distance;
            if (ground.Raycast(ray, out distance))
            {
                cell = CityMetrics.WorldToCell(ray.GetPoint(distance));
                return true;
            }

            cell = new GridCoord(0, 0);
            return false;
        }

        private GridCoord ClampToGrid(GridCoord c)
        {
            return new GridCoord(
                Mathf.Clamp(c.X, 0, sim.Grid.Width - 1),
                Mathf.Clamp(c.Y, 0, sim.Grid.Height - 1));
        }

        // ------------------------------------------------------------ preview

        private void UpdateHover(bool overHud, bool hasCell, GridCoord cell)
        {
            if (overHud || !hasCell || !sim.Grid.InBounds(cell))
            {
                liveText = "";
                HideOverlay();
                return;
            }

            liveText = ToolLabel(CurrentTool) + ": click and drag";

            if (overlayShown && !shownDragging && shownTool == CurrentTool && shownA == cell && shownTick == sim.TickCount)
            {
                return;
            }

            previewCells.Clear();
            previewColors.Clear();
            AddPreviewCell(cell);
            ShowOverlay(false, cell, cell);
        }

        private void UpdatePreview()
        {
            if (overlayShown && shownDragging && shownTool == CurrentTool
                && shownA == dragStart && shownB == dragEnd && shownTick == sim.TickCount)
            {
                return;
            }

            previewCells.Clear();
            previewColors.Clear();

            int affected = 0;
            int blocked = 0;

            if (CurrentTool == ToolType.Road)
            {
                foreach (GridCoord c in GridShapes.StraightLine(dragStart, dragEnd))
                {
                    if (AddPreviewCell(c) == PreviewKind.Valid) { affected++; }
                    else if (!sim.IsRoad(c)) { blocked++; }
                }
            }
            else
            {
                foreach (GridCoord c in GridRect.FromCorners(dragStart, dragEnd).Cells())
                {
                    PreviewKind kind = AddPreviewCell(c);
                    if (kind == PreviewKind.Valid) { affected++; }
                    else if (kind == PreviewKind.Blocked) { blocked++; }
                }
            }

            liveText = DescribeDrag(affected, blocked);
            ShowOverlay(true, dragStart, dragEnd);
        }

        private enum PreviewKind
        {
            Valid,      // the tool would change this cell
            Neutral,    // nothing to do here (shown faintly, or not at all)
            Blocked     // the tool cannot be used here
        }

        // Adds one cell (and its colour) to the preview lists and says what kind of cell it was.
        private PreviewKind AddPreviewCell(GridCoord c)
        {
            switch (CurrentTool)
            {
                case ToolType.Road:
                    if (sim.CanPlaceRoad(c)) { return Add(c, CityPalette.PreviewValid, PreviewKind.Valid); }
                    if (sim.IsRoad(c)) { return Add(c, CityPalette.PreviewNeutral, PreviewKind.Neutral); }
                    return Add(c, CityPalette.PreviewInvalid, PreviewKind.Blocked);

                case ToolType.Bulldoze:
                    if (sim.IsRoad(c) || sim.Grid.GetZone(c) != ZoneType.None)
                    {
                        return Add(c, CityPalette.PreviewBulldoze, PreviewKind.Valid);
                    }

                    return Add(c, CityPalette.PreviewNeutral, PreviewKind.Neutral);

                default:
                    ZoneType zone = ZoneForTool(CurrentTool);
                    if (sim.IsRoad(c)) { return PreviewKind.Neutral; }
                    if (!sim.CanZone(c)) { return Add(c, CityPalette.PreviewInvalid, PreviewKind.Blocked); }
                    if (sim.Grid.GetZone(c) == zone) { return Add(c, CityPalette.PreviewNeutral, PreviewKind.Neutral); }
                    return Add(c, CityPalette.ZonePreview(zone), PreviewKind.Valid);
            }
        }

        private PreviewKind Add(GridCoord c, Color color, PreviewKind kind)
        {
            previewCells.Add(c);
            previewColors.Add(color);
            return kind;
        }

        private string DescribeDrag(int affected, int blocked)
        {
            string label = ToolLabel(CurrentTool);
            switch (CurrentTool)
            {
                case ToolType.Road:
                    return blocked > 0
                        ? label + ": " + affected + " cells (" + blocked + " blocked by buildings or the edge)"
                        : label + ": " + affected + " cells";
                case ToolType.Bulldoze:
                    return label + ": " + affected + " cells";
                default:
                    return blocked > 0
                        ? label + " zone: " + affected + " cells (" + blocked + " too far from a road)"
                        : label + " zone: " + affected + " cells";
            }
        }

        private void ShowOverlay(bool isDragging, GridCoord a, GridCoord b)
        {
            cityRenderer.SetOverlay(previewCells, previewColors);
            overlayShown = true;
            shownTool = CurrentTool;
            shownDragging = isDragging;
            shownA = a;
            shownB = b;
            shownTick = sim.TickCount;
        }

        private void HideOverlay()
        {
            if (overlayShown)
            {
                cityRenderer.ClearOverlay();
                overlayShown = false;
            }
        }

        // ------------------------------------------------------------ applying

        private void ApplyTool()
        {
            string label = ToolLabel(CurrentTool);
            int changed;

            switch (CurrentTool)
            {
                case ToolType.Road:
                    changed = sim.PlaceRoad(GridShapes.StraightLine(dragStart, dragEnd));
                    ShowResult(changed == 0 ? "Nothing was built" : "Built " + changed + " road cells");
                    break;

                case ToolType.Bulldoze:
                    changed = sim.Bulldoze(GridRect.FromCorners(dragStart, dragEnd));
                    ShowResult(changed == 0 ? "Nothing to clear" : "Cleared " + changed + " cells");
                    break;

                default:
                    changed = sim.PaintZone(GridRect.FromCorners(dragStart, dragEnd), ZoneForTool(CurrentTool));
                    ShowResult(changed == 0 ? "No cells changed (zones must be near a road)" : "Zoned " + changed + " cells " + label.ToLowerInvariant());
                    break;
            }
        }

        private void ShowResult(string text)
        {
            resultText = text;
            resultUntil = Time.unscaledTime + ResultMessageSeconds;
        }

        private void CancelDrag()
        {
            dragging = false;
            liveText = "";
            HideOverlay();
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
