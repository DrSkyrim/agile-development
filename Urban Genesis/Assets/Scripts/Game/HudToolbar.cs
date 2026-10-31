using UnityEngine;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Game
{
    /// <summary>
    /// The on-screen toolbar, drawn with Unity's built-in immediate-mode GUI (OnGUI), so it needs no
    /// canvas, prefabs or scene wiring. Top-left: tool buttons and speed buttons. Bottom-left: status and city numbers.
    /// This is a placeholder; it can be swapped for a proper UI later without touching the simulation.
    /// </summary>
    public sealed class HudToolbar : MonoBehaviour
    {
        private const float ButtonWidth = 112f;
        private const float ButtonHeight = 30f;
        private const float Gap = 6f;
        private const float Margin = 10f;
        private const float StatsRefreshSeconds = 0.25f;

        private static readonly ToolType[] ToolButtons =
        {
            ToolType.Road,
            ToolType.ZoneResidential,
            ToolType.ZoneCommercial,
            ToolType.ZoneIndustrial,
            ToolType.Bulldoze
        };

        private static readonly string[] SpeedLabels = { "Pause", "1x", "2x", "3x" };

        private CitySimulation sim;
        private TickClock clock;
        private ToolController tools;

        private float scale = 1f;
        private Rect topPanel;
        private Rect bottomPanel;

        private string statsText = "";
        private float statsUpdatedAt = -100f;

        public void Init(CitySimulation simulation, TickClock tickClock, ToolController toolController)
        {
            sim = simulation;
            clock = tickClock;
            tools = toolController;
        }

        /// <summary>True if a screen point (pixels, origin bottom-left, like the mouse position) is over the toolbar.</summary>
        public bool ContainsScreenPoint(Vector2 screenPoint)
        {
            ComputeLayout();
            var guiPoint = new Vector2(screenPoint.x / scale, (Screen.height - screenPoint.y) / scale);
            return topPanel.Contains(guiPoint) || bottomPanel.Contains(guiPoint);
        }

        private void ComputeLayout()
        {
            scale = Mathf.Clamp(Screen.height / 800f, 1f, 3f);
            float guiHeight = Screen.height / scale;

            float topWidth = Margin * 2f + ToolButtons.Length * ButtonWidth + (ToolButtons.Length - 1) * Gap;
            topPanel = new Rect(Margin, Margin, topWidth, 122f);
            bottomPanel = new Rect(Margin, guiHeight - 78f - Margin, topWidth + 120f, 78f);
        }

        private void OnGUI()
        {
            if (sim == null || clock == null || tools == null)
            {
                return;
            }

            ComputeLayout();
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            DrawTopPanel();
            DrawBottomPanel();
        }

        private void DrawTopPanel()
        {
            GUI.Box(topPanel, GUIContent.none);

            Color normal = GUI.backgroundColor;
            Color active = new Color(0.45f, 0.85f, 1f, 1f);

            // Row 1: tools
            for (int i = 0; i < ToolButtons.Length; i++)
            {
                ToolType tool = ToolButtons[i];
                var rect = new Rect(
                    topPanel.x + Margin + i * (ButtonWidth + Gap),
                    topPanel.y + Margin,
                    ButtonWidth,
                    ButtonHeight);

                GUI.backgroundColor = tools.CurrentTool == tool ? active : normal;
                string label = (i + 1) + "  " + ToolController.ToolLabel(tool);
                if (GUI.Button(rect, label))
                {
                    tools.SetTool(tools.CurrentTool == tool ? ToolType.None : tool);
                }
            }

            // Row 2: road style and brush size
            float optionsY = topPanel.y + Margin + ButtonHeight + Gap;
            bool curved = tools.RoadMode == RoadMode.Curved;
            GUI.backgroundColor = normal;
            if (GUI.Button(new Rect(topPanel.x + Margin, optionsY, ButtonWidth * 1.5f, ButtonHeight - 4f),
                curved ? "Road: Curved (C)" : "Road: Straight (C)"))
            {
                tools.SetRoadMode(curved ? RoadMode.Straight : RoadMode.Curved);
            }

            GUI.backgroundColor = tools.FillMode ? active : normal;
            if (GUI.Button(new Rect(topPanel.x + Margin + ButtonWidth * 1.5f + Gap, optionsY, ButtonWidth * 0.9f, ButtonHeight - 4f),
                tools.FillMode ? "Fill: On (F)" : "Fill: Off (F)"))
            {
                tools.SetFillMode(!tools.FillMode);
            }

            GUI.backgroundColor = normal;
            float brushX = topPanel.x + Margin + ButtonWidth * 2.4f + Gap * 3f;
            GUI.Label(new Rect(brushX, optionsY, 120f, ButtonHeight - 4f), "Brush " + Mathf.RoundToInt(tools.BrushRadius * 2f) + " m");
            if (GUI.Button(new Rect(brushX + 100f, optionsY, 30f, ButtonHeight - 4f), "-"))
            {
                tools.ChangeBrush(-1);
            }

            if (GUI.Button(new Rect(brushX + 100f + 30f + Gap, optionsY, 30f, ButtonHeight - 4f), "+"))
            {
                tools.ChangeBrush(1);
            }

            // Row 3: speed
            float speedY = optionsY + ButtonHeight + Gap - 2f;
            for (int level = 0; level < SpeedLabels.Length; level++)
            {
                var rect = new Rect(topPanel.x + Margin + level * (68f + Gap), speedY, 68f, ButtonHeight - 4f);
                GUI.backgroundColor = clock.SpeedLevel == level ? active : normal;
                if (GUI.Button(rect, SpeedLabels[level]))
                {
                    clock.SetSpeedLevel(level);
                }
            }

            GUI.backgroundColor = normal;

            var tickRect = new Rect(topPanel.x + Margin + 4 * (68f + Gap) + 8f, speedY, 200f, ButtonHeight - 4f);
            GUI.Label(tickRect, clock.IsPaused ? "Paused  |  Tick " + sim.TickCount : "Tick " + sim.TickCount);
        }

        private void DrawBottomPanel()
        {
            GUI.Box(bottomPanel, GUIContent.none);

            if (Time.unscaledTime - statsUpdatedAt > StatsRefreshSeconds)
            {
                statsUpdatedAt = Time.unscaledTime;
                statsText = "Roads " + sim.Roads.SegmentCount
                    + "   Buildings " + sim.BuildingCount
                    + "   (Residential " + sim.CountBuildings(ZoneType.Residential)
                    + ", Commercial " + sim.CountBuildings(ZoneType.Commercial)
                    + ", Industrial " + sim.CountBuildings(ZoneType.Industrial) + ")";
            }

            string status = tools.StatusText;
            if (string.IsNullOrEmpty(status))
            {
                status = tools.CurrentTool == ToolType.None
                    ? "Pick a tool above (or press 1-5). Roads: drag (or press C for curved: click start, bend, end)"
                    : ToolController.ToolLabel(tools.CurrentTool) + " tool selected";
            }

            float x = bottomPanel.x + Margin;
            float width = bottomPanel.width - Margin * 2f;
            GUI.Label(new Rect(x, bottomPanel.y + 6f, width, 22f), status);
            GUI.Label(new Rect(x, bottomPanel.y + 28f, width, 22f), statsText);
            GUI.Label(new Rect(x, bottomPanel.y + 50f, width, 22f),
                "WASD move   Q/E or right-drag rotate   middle-drag pan   wheel zoom   [ ] brush   F fill   Space pause   Esc cancel");
        }
    }
}
