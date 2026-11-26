using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Game
{
    /// <summary>
    /// The heads-up display, built with UI Toolkit to follow the Figma mock-up ("Urban Genesis UI mock UP"):
    /// city identity (top left), session controls and advisory (top right), city menu (left), camera controls (right),
    /// construction toolbar (bottom centre) and the status bar (bottom).
    /// The whole element tree is created from code and styled by <c>Resources/UI/UrbanGenesis.uss</c>, so no scene
    /// setup is needed. The class keeps its old name because the camera and tools ask it whether the pointer is over the UI.
    /// </summary>
    public sealed class HudToolbar : MonoBehaviour
    {
        private const float StatsRefreshSeconds = 0.25f;
        private const string IconFolder = "UI/Icons/";

        // One game tick is ten minutes on the clock in the status bar.
        private DateTime StartDate = DateTime.Today.AddHours(9);

        /// <summary>The in-game date and time the city started at (kept in the save).</summary>
        public DateTime GameStart { get { return StartDate; } set { StartDate = value; } }

        public static readonly int[] Milestones = CityMilestones.Targets;

        private enum Weight { Regular, Medium, SemiBold }

        private sealed class ToolEntry
        {
            public string Name;
            public VisualElement Element;
            public bool Available;
            public Func<bool> IsOn;
            public Action Click;
        }

        private CitySimulation sim;
        private TickClock clock;
        private ToolController tools;
        private CameraRig rig;

        private VisualElement root;
        private bool built;
        private readonly List<VisualElement> solidPanels = new List<VisualElement>();
        private readonly Dictionary<string, Texture2D> iconCache = new Dictionary<string, Texture2D>();
        private Font fontRegular;
        private Font fontMedium;
        private Font fontSemiBold;

        // Elements that change while playing.
        private Label dateLabel;
        private Label timeLabel;
        private Label speedLabel;
        private VisualElement pauseButton;
        private VisualElement playButton;
        private Label cityDetail;
        private Label treasuryValue;
        private const double LowMoney = 5000.0;
        private bool lowMoneyWarned, deficitWarned;
        private Label treasuryChange;
        private Label jobsValue, jobsChange, milestoneDetail;
        private Label populationValue;
        private Label populationChange;
        private readonly VisualElement[] demandFill = new VisualElement[3];
        private Label milestoneTarget;
        private VisualElement milestoneFill;
        private Label advisoryTitle;
        private Label advisoryDetail;
        private VisualElement advisoryPanel;
        private VisualElement compassArrow;
        private VisualElement optionsPanel;
        private VisualElement roadOptions;
        private VisualElement zoneOptions;
        private VisualElement brushOptions;
        private VisualElement chipStraight;
        private VisualElement chipCurved;
        private readonly VisualElement[] typeChips = new VisualElement[RoadSpecs.All.Length];
        private readonly VisualElement[] upgradeChips = new VisualElement[RoadSpecs.All.Length];
        private VisualElement upgradeOptions;
        private VisualElement junctionOptions;
        private Label junctionLabel;
        private VisualElement lightsChip;
        private Label lightsChipLabel;
        private VisualElement resetLanesChip;
        private VisualElement chipFill;
        private readonly VisualElement[] zoneChips = new VisualElement[5];
        private Label brushLabel;
        private VisualElement scaleBar;
        private Label scaleLabel;
        private VisualElement measureTag;
        private Label measureLabel;
        private float shownScaleMetres = -1f;
        private readonly List<ToolEntry> toolEntries = new List<ToolEntry>();

        private string cityName = "New city";
        private int lastRunningSpeed = 1;
        private float statsUpdatedAt = -100f;
        private int lastPopulation = -1;
        private float populationSampledAt;
        private float populationTrend;
        private ToolType lastZoneTool = ToolType.ZoneResidential;
        private string message = "";
        private float messageUntil;

        /// <summary>Raised when the player clicks the city name panel (opens the pause menu).</summary>
        public event Action MenuRequested;

        /// <summary>Raised by the save button in the clock panel.</summary>
        public event Action SaveRequested;

        /// <summary>Raised by the menu button in the clock panel (asks to go back to the main menu).</summary>
        public event Action MainMenuRequested;
        public event Action UndoRequested;
        public event Action RedoRequested;

        public void Init(CitySimulation simulation, TickClock tickClock, ToolController toolController, CameraRig cameraRig, string name)
        {
            cityName = name;
            sim = simulation;
            sim.Treasury.Changed += () => statsUpdatedAt = -100f;
            clock = tickClock;
            tools = toolController;
            rig = cameraRig;

            EnsureEventSystem();
            CreateDocument();
            TryBuild();
        }

        /// <summary>True if a screen point (pixels, origin bottom-left, like the mouse position) is over a panel of the HUD.</summary>
        public bool ContainsScreenPoint(Vector2 screenPoint)
        {
            if (!built || root == null || root.panel == null)
            {
                return false;
            }

            Vector2 p = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screenPoint.x, Screen.height - screenPoint.y));
            for (int i = 0; i < solidPanels.Count; i++)
            {
                VisualElement panel = solidPanels[i];
                if (panel.resolvedStyle.display != DisplayStyle.None && panel.worldBound.Contains(p))
                {
                    return true;
                }
            }

            return false;
        }

        // ---------------------------------------------------------------------------------------------
        // Set-up

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        private void CreateDocument()
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1440, 900);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.sortingOrder = 10;
            settings.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();

            // The document starts inactive so the panel settings are assigned before it initialises.
            var go = new GameObject("HUD");
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = settings;
            go.SetActive(true);
            root = document.rootVisualElement;
        }

        private void Update()
        {
            if (sim == null || clock == null || tools == null)
            {
                return;
            }

            if (!built)
            {
                TryBuild();
                if (!built)
                {
                    return;
                }
            }

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
            {
                ToggleHelp();
            }

            Refresh();
        }

        private VisualElement helpPanel;

        /// <summary>Shows or hides the list of controls.</summary>
        public void ToggleHelp()
        {
            if (helpPanel != null)
            {
                bool show = helpPanel.resolvedStyle.display == DisplayStyle.None;
                helpPanel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        // ------------------------------------------------------------ minimap

        private const int MinimapPixels = 160;
        private Texture2D minimapTexture;
        private Color32[] minimapPixels;
        private float minimapRefreshAt;

        private void BuildMinimap()
        {
            VisualElement panel = Panel("Minimap", false);
            Place(panel, 24, null, null, 150);
            panel.style.paddingLeft = 4; panel.style.paddingRight = 4; panel.style.paddingTop = 4; panel.style.paddingBottom = 4;
            minimapTexture = new Texture2D(MinimapPixels, MinimapPixels, TextureFormat.RGBA32, false);
            minimapTexture.filterMode = FilterMode.Point;
            minimapPixels = new Color32[MinimapPixels * MinimapPixels];

            var map = new VisualElement();
            Size(map, MinimapPixels, MinimapPixels);
            map.style.backgroundImage = new StyleBackground(minimapTexture);
            map.RegisterCallback<ClickEvent>(e =>
            {
                Aabb b = sim.Roads.WorldBounds;
                float fx = Mathf.Clamp01(e.localPosition.x / MinimapPixels);
                float fy = 1f - Mathf.Clamp01(e.localPosition.y / MinimapPixels);
                rig.FocusOn((float)(b.MinX + fx * (b.MaxX - b.MinX)), (float)(b.MinY + fy * (b.MaxY - b.MinY)));
            });
            panel.Add(map);
        }

        private void PaintMinimapPixel(int x, int y, Color32 color)
        {
            if (x >= 0 && y >= 0 && x < MinimapPixels && y < MinimapPixels)
            {
                minimapPixels[y * MinimapPixels + x] = color;
            }
        }

        private void RefreshMinimap()
        {
            if (Time.unscaledTime < minimapRefreshAt) { return; }
            minimapRefreshAt = Time.unscaledTime + 1.0f;

            Aabb b = sim.Roads.WorldBounds;
            double width = b.MaxX - b.MinX;
            double height = b.MaxY - b.MinY;
            var ground = new Color32(34, 44, 52, 255);
            for (int i = 0; i < minimapPixels.Length; i++) { minimapPixels[i] = ground; }

            if (sim.River != null)
            {
                var water = new Color32(60, 120, 210, 255);
                for (int i = 0; i + 1 < sim.River.Points.Count; i++)
                {
                    for (double t = 0.0; t < 1.0; t += 0.25)
                    {
                        Vec2 point = Vec2.Lerp(sim.River.Points[i], sim.River.Points[i + 1], t);
                        int rx = (int)((point.X - b.MinX) / width * MinimapPixels);
                        int ry = (int)((point.Y - b.MinY) / height * MinimapPixels);
                        for (int dx = 0; dx < 3; dx++) { PaintMinimapPixel(rx + dx - 1, ry, water); PaintMinimapPixel(rx, ry + dx - 1, water); }
                    }
                }
            }

            foreach (RoadSegment segment in sim.Roads.Segments)
            {
                var road = RoadSpecs.IsHighway(segment.Type) ? new Color32(235, 190, 90, 255) : new Color32(170, 180, 190, 255);
                int thick = RoadSpecs.IsHighway(segment.Type) ? 2 : 1;
                foreach (Vec2 point in segment.Curve.Sample(6.0))
                {
                    int px = (int)((point.X - b.MinX) / width * MinimapPixels);
                    int py = (int)((point.Y - b.MinY) / height * MinimapPixels);
                    for (int dx = 0; dx < thick; dx++) { for (int dy = 0; dy < thick; dy++) { PaintMinimapPixel(px + dx, py + dy, road); } }
                }
            }

            foreach (Building building in sim.Buildings)
            {
                int px = (int)((building.Center.X - b.MinX) / width * MinimapPixels);
                int py = (int)((building.Center.Y - b.MinY) / height * MinimapPixels);
                Color32 color = building.IsCivic ? new Color32(225, 120, 235, 255)
                    : building.IsResidential ? new Color32(90, 200, 120, 255)
                    : building.Zone == ZoneType.Commercial ? new Color32(90, 150, 240, 255) : new Color32(240, 200, 70, 255);
                for (int dx = 0; dx < 2; dx++) { for (int dy = 0; dy < 2; dy++) { PaintMinimapPixel(px + dx, py + dy, color); } }
            }

            // Where the camera is looking.
            Vector2 focus = rig.FocusPoint;
            int cx = (int)((focus.x - b.MinX) / width * MinimapPixels);
            int cy = (int)((focus.y - b.MinY) / height * MinimapPixels);
            var white = new Color32(255, 255, 255, 255);
            for (int d = -4; d <= 4; d++) { PaintMinimapPixel(cx + d, cy, white); PaintMinimapPixel(cx, cy + d, white); }

            minimapTexture.SetPixels32(0, 0, MinimapPixels, MinimapPixels, minimapPixels);
            minimapTexture.Apply(false, false);
        }

        private VisualElement summaryPanel;
        private Label summaryLabel;
        private float summaryHideAt;
        private bool summaryPending;

        private void BuildSummary()
        {
            summaryPanel = Panel("Day summary", false);
            Place(summaryPanel, null, 176, 24, null);
            summaryPanel.style.width = 264;
            summaryPanel.style.alignItems = Align.FlexStart;
            summaryPanel.style.paddingLeft = 14;
            summaryPanel.style.paddingRight = 14;
            summaryPanel.style.paddingTop = 10;
            summaryPanel.style.paddingBottom = 10;
            summaryPanel.style.display = DisplayStyle.None;
            summaryLabel = Text("", null, Weight.Regular);
            summaryLabel.style.whiteSpace = WhiteSpace.Normal;
            summaryPanel.Add(summaryLabel);
            summaryPanel.RegisterCallback<ClickEvent>(e => summaryPanel.style.display = DisplayStyle.None);
            sim.Treasury.DayClosed += () => summaryPending = true;
        }

        private void UpdateSummary()
        {
            if (summaryPending)
            {
                summaryPending = false;
                CityTreasury t = sim.Treasury;
                var m = new System.Text.StringBuilder();
                m.Append("End of day ").Append(t.DaysClosed).Append(" (click to close)\n");
                m.Append("Start of day: ").Append(CitySimulation.FormatMoney(t.LastDayStart)).Append('\n');
                m.Append("Tax income: +").Append(CitySimulation.FormatMoney(t.LastDayIncome)).Append('\n');
                m.Append("Road upkeep: -").Append(CitySimulation.FormatMoney(t.LastDayUpkeep)).Append('\n');
                m.Append("Building roads: -").Append(CitySimulation.FormatMoney(t.LastDaySpent)).Append('\n');
                m.Append("Demolition refunds: +").Append(CitySimulation.FormatMoney(t.LastDayRefunded)).Append('\n');
                if (t.LastDayShortfall > 0.5)
                {
                    m.Append("Unpaid upkeep: ").Append(CitySimulation.FormatMoney(t.LastDayShortfall)).Append(" (the treasury ran dry)\n");
                }

                if (t.LastDayGranted > 0.5)
                {
                    m.Append("Milestone rewards: +").Append(CitySimulation.FormatMoney(t.LastDayGranted)).Append('\n');
                }

                m.Append("End of day: ").Append(CitySimulation.FormatMoney(t.LastDayEnd));
                SetText(summaryLabel, m.ToString());
                summaryPanel.style.display = DisplayStyle.Flex;
                summaryHideAt = Time.unscaledTime + 12f;
            }
            else if (summaryPanel.resolvedStyle.display != DisplayStyle.None && Time.unscaledTime > summaryHideAt)
            {
                summaryPanel.style.display = DisplayStyle.None;
            }
        }

        private VisualElement inspectPanel;
        private Label inspectLabel;

        private void BuildInspect()
        {
            inspectPanel = Panel("Inspector", false);
            Place(inspectPanel, null, null, 24, 110);
            inspectPanel.style.width = 320;
            inspectPanel.style.alignItems = Align.FlexStart;
            inspectPanel.style.paddingLeft = 14;
            inspectPanel.style.paddingRight = 14;
            inspectPanel.style.paddingTop = 10;
            inspectPanel.style.paddingBottom = 10;
            inspectPanel.style.display = DisplayStyle.None;
            inspectLabel = Text("", null, Weight.Regular);
            inspectLabel.style.whiteSpace = WhiteSpace.Normal;
            inspectPanel.Add(inspectLabel);
        }

        private void UpdateInspect()
        {
            string text = tools.CurrentTool == ToolType.Inspect ? tools.InspectText : "";
            bool show = !string.IsNullOrEmpty(text);
            inspectPanel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show)
            {
                SetText(inspectLabel, text);
            }
        }

        private void BuildHelp()
        {
            helpPanel = Panel("Controls help", false);
            Place(helpPanel, null, 84, 300, null);
            helpPanel.style.width = 340;
            helpPanel.style.alignItems = Align.FlexStart;
            helpPanel.style.paddingLeft = 16;
            helpPanel.style.paddingRight = 16;
            helpPanel.style.paddingTop = 12;
            helpPanel.style.paddingBottom = 12;
            helpPanel.style.display = DisplayStyle.None;

            helpPanel.Add(Text("Controls (F1 closes this)", null, Weight.SemiBold));
            string[] lines =
            {
                "WASD / arrow keys: move the camera (Shift = faster)",
                "Q / E: rotate the camera",
                "I or 9: inspect a building or road",
                "O: show how full the roads are",
                "Power / Water / Services (toolbar): lay cables and pipes, place plants, towers and stations",
                "Ctrl+Z: undo, Ctrl+Y (or Ctrl+Shift+Z): redo",
                "Mouse wheel: zoom",
                "Right mouse drag: rotate and tilt",
                "Middle mouse drag: pan",
                "Left drag with no tool selected: pan",
                "Mouse at the screen edge: scroll",
                "Esc: cancel the current tool",
                "H: road tool, other tool hotkeys are shown on the toolbar",
            };
            for (int i = 0; i < lines.Length; i++)
            {
                Label line = Text(lines[i], "ug-text--soft", Weight.Regular);
                line.style.marginTop = 6;
                line.style.whiteSpace = WhiteSpace.Normal;
                helpPanel.Add(line);
            }
        }

        private void TryBuild()
        {
            if (built || root == null)
            {
                return;
            }

            fontRegular = Resources.Load<Font>("UI/Fonts/Inter-Regular");
            fontMedium = Resources.Load<Font>("UI/Fonts/Inter-Medium");
            fontSemiBold = Resources.Load<Font>("UI/Fonts/Inter-SemiBold");

            var sheet = Resources.Load<StyleSheet>("UI/UrbanGenesis");
            if (sheet != null)
            {
                root.styleSheets.Add(sheet);
            }
            else
            {
                Debug.LogWarning("HUD style sheet 'Resources/UI/UrbanGenesis.uss' not found; the HUD will be unstyled.");
            }

            root.AddToClassList("ug-root");
            root.pickingMode = PickingMode.Ignore;

            BuildIdentity();
            BuildSession();
            BuildAdvisory();
            BuildCityMenu();
            BuildCameraControls();
            BuildToolArea();
            BuildStatusBar();
            BuildScale();
            BuildMeasureTag();
            BuildHelp();
            BuildInspect();
            BuildSummary();
            BuildMinimap();

            built = true;
            Refresh();
        }

        // ---------------------------------------------------------------------------------------------
        // Element helpers

        private VisualElement Panel(string name, bool row)
        {
            var panel = new VisualElement { name = name };
            panel.AddToClassList("ug-panel");
            panel.style.flexDirection = row ? FlexDirection.Row : FlexDirection.Column;
            panel.style.alignItems = Align.Center;
            root.Add(panel);
            solidPanels.Add(panel);
            return panel;
        }

        private static void Place(VisualElement element, float? left, float? top, float? right, float? bottom)
        {
            element.style.position = Position.Absolute;
            if (left.HasValue) { element.style.left = left.Value; }
            if (top.HasValue) { element.style.top = top.Value; }
            if (right.HasValue) { element.style.right = right.Value; }
            if (bottom.HasValue) { element.style.bottom = bottom.Value; }
        }

        private static void Size(VisualElement element, float width, float height)
        {
            element.style.width = width;
            element.style.height = height;
        }

        private Label Text(string text, string extraClass, Weight weight)
        {
            var label = new Label(text);
            label.AddToClassList("ug-text");
            if (!string.IsNullOrEmpty(extraClass))
            {
                foreach (string c in extraClass.Split(' '))
                {
                    label.AddToClassList(c);
                }
            }

            Font font = weight == Weight.SemiBold ? fontSemiBold : weight == Weight.Medium ? fontMedium : fontRegular;
            if (font != null)
            {
                label.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(font));
            }

            label.pickingMode = PickingMode.Ignore;
            return label;
        }

        private VisualElement Icon(string iconName, string sizeClass)
        {
            var icon = new VisualElement();
            icon.AddToClassList("ug-icon");
            if (!string.IsNullOrEmpty(sizeClass))
            {
                icon.AddToClassList(sizeClass);
            }

            Texture2D tex = LoadIcon(iconName);
            if (tex != null)
            {
                icon.style.backgroundImage = new StyleBackground(tex);
            }

            icon.pickingMode = PickingMode.Ignore;
            return icon;
        }

        private Texture2D LoadIcon(string iconName)
        {
            Texture2D tex;
            if (!iconCache.TryGetValue(iconName, out tex))
            {
                tex = Resources.Load<Texture2D>(IconFolder + iconName);
                if (tex == null)
                {
                    Debug.LogWarning("HUD icon not found: Resources/" + IconFolder + iconName);
                }
                else
                {
                    tex.filterMode = FilterMode.Bilinear;
                }

                iconCache[iconName] = tex;
            }

            return tex;
        }

        private VisualElement TextButton(string text, Action onClick)
        {
            var button = new VisualElement();
            button.AddToClassList("ug-btn");
            button.style.justifyContent = Justify.Center;
            button.AddToClassList("ug-btn--text");
            Label label = Text(text, "ug-text--soft", Weight.Medium);
            label.style.fontSize = 11;
            button.Add(label);
            button.RegisterCallback<ClickEvent>(e => onClick());
            return button;
        }

        private VisualElement IconButton(string iconName, bool small, Action onClick)
        {
            var button = new VisualElement();
            button.AddToClassList("ug-btn");
            if (small)
            {
                button.AddToClassList("ug-btn--28");
            }

            button.Add(Icon(iconName, null));
            if (onClick != null)
            {
                button.RegisterCallback<ClickEvent>(e => onClick());
            }

            return button;
        }

        private VisualElement Divider(bool vertical)
        {
            var divider = new VisualElement();
            divider.AddToClassList("ug-divider");
            divider.AddToClassList(vertical ? "ug-divider--v" : "ug-divider--h");
            divider.pickingMode = PickingMode.Ignore;
            return divider;
        }

        private VisualElement Chip(string text, Action onClick, string dotClass)
        {
            var chip = new VisualElement();
            chip.AddToClassList("ug-chip");
            chip.style.flexDirection = FlexDirection.Row;
            if (!string.IsNullOrEmpty(dotClass))
            {
                var dot = new VisualElement();
                dot.AddToClassList("ug-dot");
                dot.AddToClassList(dotClass);
                dot.pickingMode = PickingMode.Ignore;
                chip.Add(dot);
            }

            chip.Add(Text(text, null, Weight.Medium));
            chip.RegisterCallback<ClickEvent>(e => onClick());
            return chip;
        }

        private static void SetOn(VisualElement element, string onClass, bool on)
        {
            element.EnableInClassList(onClass, on);
        }

        /// <summary>Shows a message in the advisory panel for a few seconds.</summary>
        public void Notify(string text, float seconds = 3f)
        {
            message = text;
            messageUntil = Time.unscaledTime + seconds;
        }

        // ---------------------------------------------------------------------------------------------
        // Panels

        private void BuildIdentity()
        {
            VisualElement panel = Panel("City identity", true);
            Place(panel, 24, 24, null, null);
            Size(panel, 258, 64);
            panel.RegisterCallback<ClickEvent>(e => { Action requested = MenuRequested; if (requested != null) { requested(); } });
            panel.style.paddingLeft = 14;
            panel.style.paddingRight = 14;

            var emblem = new VisualElement();
            emblem.AddToClassList("ug-emblem");
            emblem.Add(Icon("building-2", null));
            panel.Add(emblem);

            var details = new VisualElement();
            details.style.flexGrow = 1;
            details.style.marginLeft = 12;
            details.Add(Text(cityName, "ug-text--title", Weight.SemiBold));

            var milestone = new VisualElement();
            milestone.style.flexDirection = FlexDirection.Row;
            milestone.style.alignItems = Align.Center;
            milestone.style.marginTop = 5;
            var dot = new VisualElement();
            dot.AddToClassList("ug-status-dot");
            milestone.Add(dot);
            cityDetail = Text("", "ug-text--muted ug-text--soft", Weight.Regular);
            milestone.Add(cityDetail);
            details.Add(milestone);
            panel.Add(details);

            panel.Add(Icon("chevron-down", "ug-icon--xs"));
        }

        private void BuildSession()
        {
            VisualElement panel = Panel("Session controls", true);
            Place(panel, null, 24, 24, null);
            Size(panel, 264, 48);
            panel.style.paddingLeft = 6;
            panel.style.paddingRight = 6;

            // Placeholder weather: the simulation has no weather yet.
            var weather = new VisualElement();
            weather.style.flexDirection = FlexDirection.Row;
            weather.style.alignItems = Align.Center;
            weather.style.paddingLeft = 8;
            weather.style.paddingRight = 8;
            weather.Add(Icon("sun", "ug-icon--sm"));
            Label temperature = Text("18°C", "ug-text--soft", Weight.Regular);
            temperature.style.marginLeft = 8;
            weather.Add(temperature);
            panel.Add(weather);

            var divider = Divider(true);
            divider.style.height = 20;
            divider.style.marginLeft = 5;
            divider.style.marginRight = 5;
            panel.Add(divider);

            panel.Add(TextButton("Undo", () => { Action a = UndoRequested; if (a != null) { a(); } }));
            panel.Add(TextButton("Redo", () => { Action a = RedoRequested; if (a != null) { a(); } }));
            panel.Add(IconButton("route", false, () => { tools.ToggleTrafficOverlay(); Notify(tools.TrafficOverlayOn ? "Traffic overlay on: green is quiet, amber busy, red full (O toggles)." : "Traffic overlay off."); }));
            panel.Add(IconButton("bell", false, () => Notify("Notifications are not available yet.")));
            panel.Add(IconButton("save", false, () => { Action requested = SaveRequested; if (requested != null) { requested(); } }));
            var helpButton = new VisualElement();
            helpButton.AddToClassList("ug-btn");
            helpButton.style.justifyContent = Justify.Center;
            helpButton.Add(Text("?", null, Weight.SemiBold));
            helpButton.RegisterCallback<ClickEvent>(e => ToggleHelp());
            panel.Add(helpButton);
            panel.Add(IconButton("settings-2", false, () => Notify("Settings are not available yet.")));
            panel.Add(IconButton("menu", false, () => { Action requested = MainMenuRequested; if (requested != null) { requested(); } }));
        }

        private void BuildAdvisory()
        {
            advisoryPanel = Panel("City advisory", true);
            Place(advisoryPanel, null, 84, 24, null);
            advisoryPanel.style.width = 264;
            advisoryPanel.style.minHeight = 60;
            advisoryPanel.style.alignItems = Align.FlexStart;
            advisoryPanel.style.paddingLeft = 14;
            advisoryPanel.style.paddingRight = 14;
            advisoryPanel.style.paddingTop = 14;
            advisoryPanel.style.paddingBottom = 14;
            advisoryPanel.AddToClassList("ug-advisory--ok");

            advisoryPanel.Add(Icon("circle-check", "ug-icon--sm"));

            var texts = new VisualElement();
            texts.style.flexGrow = 1;
            texts.style.flexShrink = 1;
            texts.style.marginLeft = 10;
            advisoryTitle = Text("", null, Weight.Medium);
            advisoryTitle.style.whiteSpace = WhiteSpace.Normal;
            advisoryDetail = Text("", "ug-text--muted", Weight.Regular);
            advisoryDetail.style.whiteSpace = WhiteSpace.Normal;
            advisoryDetail.style.marginTop = 5;
            texts.Add(advisoryTitle);
            texts.Add(advisoryDetail);
            advisoryPanel.Add(texts);
        }

        private void BuildCityMenu()
        {
            VisualElement panel = Panel("City management", false);
            Place(panel, 24, 304, null, null);
            panel.style.paddingLeft = 6;
            panel.style.paddingRight = 6;
            panel.style.paddingTop = 6;
            panel.style.paddingBottom = 6;

            panel.Add(IconButton("layers", false, () => Notify("Map layers are not available yet.")));
            var divider = Divider(false);
            divider.style.width = 24;
            divider.style.marginTop = 4;
            divider.style.marginBottom = 4;
            panel.Add(divider);
            panel.Add(IconButton("chart-no-axes-combined", false, () => Notify("Statistics are not available yet.")));
            panel.Add(IconButton("landmark", false, () => Notify("City policies are not available yet.")));
            panel.Add(IconButton("map", false, () => Notify("The overview map is not available yet.")));
        }

        private void BuildCameraControls()
        {
            VisualElement panel = Panel("Camera controls", false);
            Place(panel, null, 520, 24, null);
            panel.style.paddingLeft = 6;
            panel.style.paddingRight = 6;
            panel.style.paddingTop = 6;
            panel.style.paddingBottom = 6;

            // North indicator: the arrow turns with the camera; click to face north again.
            var north = new VisualElement();
            Size(north, 36, 36);
            north.style.alignItems = Align.Center;
            north.style.justifyContent = Justify.Center;
            north.Add(Text("N", "ug-text", Weight.SemiBold));
            compassArrow = new VisualElement();
            Size(compassArrow, 11, 11);
            compassArrow.style.marginTop = 2;
            compassArrow.style.alignItems = Align.Center;
            compassArrow.style.justifyContent = Justify.Center;
            var chevron = new VisualElement();
            Size(chevron, 6, 6);
            chevron.style.borderTopWidth = 2;
            chevron.style.borderLeftWidth = 2;
            Color accent = new Color32(113, 183, 255, 255);
            chevron.style.borderTopColor = accent;
            chevron.style.borderLeftColor = accent;
            chevron.style.rotate = new StyleRotate(new Rotate(Angle.Degrees(45f)));
            chevron.style.marginTop = 2;
            chevron.pickingMode = PickingMode.Ignore;
            compassArrow.Add(chevron);
            compassArrow.pickingMode = PickingMode.Ignore;
            north.Add(compassArrow);
            north.RegisterCallback<ClickEvent>(e => rig.FaceNorth());
            panel.Add(north);

            var divider = Divider(false);
            divider.style.width = 36;
            divider.style.marginTop = 2;
            divider.style.marginBottom = 2;
            panel.Add(divider);

            panel.Add(IconButton("plus", false, () => rig.ZoomStep(1)));
            panel.Add(IconButton("minus", false, () => rig.ZoomStep(-1)));
            panel.Add(IconButton("locate-fixed", false, () => rig.Recenter()));
        }

        private void BuildToolArea()
        {
            // A full-width column anchored to the bottom centre. It holds the options strip and the toolbar.
            var column = new VisualElement { name = "Tool area" };
            Place(column, 0, null, 0, 96);
            column.style.alignItems = Align.Center;
            column.pickingMode = PickingMode.Ignore;
            root.Add(column);

            optionsPanel = new VisualElement { name = "Tool options" };
            optionsPanel.AddToClassList("ug-panel");
            optionsPanel.style.position = Position.Relative;
            optionsPanel.style.height = 44;
            optionsPanel.style.marginBottom = 10;
            optionsPanel.style.paddingLeft = 8;
            optionsPanel.style.paddingRight = 8;
            column.Add(optionsPanel);
            solidPanels.Add(optionsPanel);

            roadOptions = new VisualElement();
            roadOptions.style.flexDirection = FlexDirection.Row;
            roadOptions.style.alignItems = Align.Center;
            chipStraight = Chip("Straight", () => tools.SetRoadMode(RoadMode.Straight), null);
            chipCurved = Chip("Curved", () => tools.SetRoadMode(RoadMode.Curved), null);
            roadOptions.Add(chipStraight);
            roadOptions.Add(chipCurved);
            var typeDivider = new VisualElement();
            typeDivider.AddToClassList("ug-divider");
            typeDivider.AddToClassList("ug-divider--v");
            typeDivider.style.marginLeft = 6;
            typeDivider.style.marginRight = 6;
            roadOptions.Add(typeDivider);
            for (int i = 0; i < RoadSpecs.All.Length; i++)
            {
                RoadType type = RoadSpecs.All[i];
                string label = RoadSpecs.Name(type) + "  " + CitySimulation.FormatMoney(sim.Treasury.BuildCostPerMetreOf(type)) + "/m";
                typeChips[i] = Chip(label, () => tools.SetRoadType(type), null);
                roadOptions.Add(typeChips[i]);
            }

            optionsPanel.Add(roadOptions);

            upgradeOptions = new VisualElement();
            upgradeOptions.style.flexDirection = FlexDirection.Row;
            upgradeOptions.style.alignItems = Align.Center;
            Label upgradeLabel = Text("Rebuild as", "ug-text--soft", Weight.Regular);
            upgradeLabel.style.fontSize = 11;
            upgradeLabel.style.marginLeft = 8;
            upgradeLabel.style.marginRight = 8;
            upgradeOptions.Add(upgradeLabel);
            for (int i = 0; i < RoadSpecs.All.Length; i++)
            {
                RoadType type = RoadSpecs.All[i];
                upgradeChips[i] = Chip(RoadSpecs.Name(type) + "  " + CitySimulation.FormatMoney(sim.Treasury.BuildCostPerMetreOf(type)) + "/m", () => tools.SetUpgradeType(type), null);
                upgradeOptions.Add(upgradeChips[i]);
            }

            optionsPanel.Add(upgradeOptions);

            junctionOptions = new VisualElement();
            junctionOptions.style.flexDirection = FlexDirection.Row;
            junctionOptions.style.alignItems = Align.Center;
            junctionLabel = Text("Click an intersection", "ug-text--soft", Weight.Regular);
            junctionLabel.style.fontSize = 11;
            junctionLabel.style.marginLeft = 8;
            junctionLabel.style.marginRight = 10;
            junctionOptions.Add(junctionLabel);
            lightsChip = Chip("Traffic lights: on", () => tools.ToggleSelectedLights(), null);
            lightsChipLabel = (Label)lightsChip.ElementAt(0);
            junctionOptions.Add(lightsChip);
            resetLanesChip = Chip("Reset lanes", () => tools.ResetSelectedLanes(), null);
            junctionOptions.Add(resetLanesChip);
            optionsPanel.Add(junctionOptions);

            groupOptions[0] = BuildGroupRow(new[] { ("Power cable", (Action)(() => tools.SelectUtility(UtilityKind.Power)), (Func<bool>)(() => tools.CurrentTool == ToolType.Utility && tools.UtilityKind == UtilityKind.Power), (Func<string>)(() => "Power cable  " + CitySimulation.FormatMoney(sim.Treasury.PowerLinePerMetre) + "/m")),
                FacilityEntry(BuildingShape.PowerPlant) });
            groupOptions[1] = BuildGroupRow(new[] { ("Water pipe", (Action)(() => tools.SelectUtility(UtilityKind.Water)), (Func<bool>)(() => tools.CurrentTool == ToolType.Utility && tools.UtilityKind == UtilityKind.Water), (Func<string>)(() => "Water pipe  " + CitySimulation.FormatMoney(sim.Treasury.WaterPipePerMetre) + "/m")),
                FacilityEntry(BuildingShape.WaterTower), FacilityEntry(BuildingShape.SewageOutlet) });
            groupOptions[2] = BuildGroupRow(new[] { FacilityEntry(BuildingShape.Hospital), FacilityEntry(BuildingShape.PoliceStation),
                FacilityEntry(BuildingShape.FireStation), FacilityEntry(BuildingShape.School) });
            for (int g = 0; g < groupOptions.Length; g++) { optionsPanel.Add(groupOptions[g]); }

            zoneOptions = new VisualElement();
            zoneOptions.style.flexDirection = FlexDirection.Row;
            zoneOptions.style.alignItems = Align.Center;
            zoneChips[0] = Chip("Residential", () => tools.SetTool(ToolType.ZoneResidential), "ug-dot--r");
            zoneChips[1] = Chip("Commercial", () => tools.SetTool(ToolType.ZoneCommercial), "ug-dot--c");
            zoneChips[2] = Chip("Industrial", () => tools.SetTool(ToolType.ZoneIndustrial), "ug-dot--i");
            zoneOptions.Add(zoneChips[0]);
            zoneOptions.Add(zoneChips[1]);
            zoneOptions.Add(zoneChips[2]);
            zoneChips[4] = Chip("Dense residential", () =>
            {
                if (sim.IsUnlocked(Feature.DenseResidential)) { tools.SetTool(ToolType.ZoneDense); }
                else { Notify("Dense residential zones unlock at 5,000 residents.", 5f); }
            }, "ug-dot--r");
            zoneOptions.Add(zoneChips[4]);
            zoneChips[3] = Chip("Clear zoning", () => tools.SetTool(ToolType.ZoneClear), null);
            zoneOptions.Add(zoneChips[3]);
            var zoneDivider = Divider(true);
            zoneDivider.style.height = 20;
            zoneDivider.style.marginLeft = 6;
            zoneDivider.style.marginRight = 6;
            zoneOptions.Add(zoneDivider);
            chipFill = Chip("Fill side", () => tools.SetFillMode(!tools.FillMode), null);
            zoneOptions.Add(chipFill);
            optionsPanel.Add(zoneOptions);

            brushOptions = new VisualElement();
            brushOptions.style.flexDirection = FlexDirection.Row;
            brushOptions.style.alignItems = Align.Center;
            var brushDivider = Divider(true);
            brushDivider.style.height = 20;
            brushDivider.style.marginLeft = 6;
            brushDivider.style.marginRight = 6;
            brushOptions.Add(brushDivider);
            brushLabel = Text("Brush", "ug-text--soft", Weight.Regular);
            brushLabel.style.fontSize = 11;
            brushLabel.style.minWidth = 70;
            brushLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            brushOptions.Add(IconButtonSmall("minus", () => tools.ChangeBrush(-1)));
            brushOptions.Add(brushLabel);
            brushOptions.Add(IconButtonSmall("plus", () => tools.ChangeBrush(1)));
            optionsPanel.Add(brushOptions);

            // The construction toolbar.
            VisualElement bar = new VisualElement { name = "Construction toolbar" };
            bar.AddToClassList("ug-panel");
            bar.style.position = Position.Relative;
            bar.style.width = 844;
            bar.style.height = 88;
            bar.style.justifyContent = Justify.SpaceBetween;
            bar.style.paddingLeft = 12;
            bar.style.paddingRight = 12;
            column.Add(bar);
            solidPanels.Add(bar);

            AddTool(bar, "route", "Roads", true,
                () => tools.CurrentTool == ToolType.Road,
                () => tools.SetTool(tools.CurrentTool == ToolType.Road ? ToolType.None : ToolType.Road));
            AddTool(bar, "layers", "Upgrade", true,
                () => tools.CurrentTool == ToolType.Upgrade,
                () => tools.SetTool(tools.CurrentTool == ToolType.Upgrade ? ToolType.None : ToolType.Upgrade));
            AddTool(bar, "locate-fixed", "Junctions", true,
                () => tools.CurrentTool == ToolType.Junction,
                () => tools.SetTool(tools.CurrentTool == ToolType.Junction ? ToolType.None : ToolType.Junction));
            AddTool(bar, "grid-2x2", "Zoning", true,
                () => tools.IsZoneTool,
                () => tools.SetTool(tools.IsZoneTool ? ToolType.None : lastZoneTool));
            AddTool(bar, "zap", "Power", true,
                () => InGroup(0),
                () => { if (InGroup(0)) { tools.SetTool(ToolType.None); } else { tools.SelectUtility(UtilityKind.Power); } });
            AddTool(bar, "droplets", "Water", true,
                () => InGroup(1),
                () => { if (InGroup(1)) { tools.SetTool(ToolType.None); } else { tools.SelectUtility(UtilityKind.Water); } });
            AddTool(bar, "shield-plus", "Services", true,
                () => InGroup(2),
                () => { if (InGroup(2)) { tools.SetTool(ToolType.None); } else { SelectFirstService(); } });
            AddTool(bar, "tram-front", "Transit", false, null, null);
            AddTool(bar, "trees", "Parks", false, null, null);
            AddTool(bar, "mountain", "Terrain", false, null, null);
            VisualElement barDivider = Divider(true);
            barDivider.style.height = 36;
            bar.Add(barDivider);
            AddTool(bar, "locate-fixed", "Inspect", true,
                () => tools.CurrentTool == ToolType.Inspect,
                () => tools.SetTool(tools.CurrentTool == ToolType.Inspect ? ToolType.None : ToolType.Inspect));
            AddTool(bar, "pickaxe", "Demolish", true,
                () => tools.CurrentTool == ToolType.Bulldoze,
                () => tools.SetTool(tools.CurrentTool == ToolType.Bulldoze ? ToolType.None : ToolType.Bulldoze));
        }

        // ------------------------------------------------------------ power, water and service tool groups

        private readonly VisualElement[] groupOptions = new VisualElement[3];
        private readonly List<GroupChip> groupChips = new List<GroupChip>();

        private sealed class GroupChip
        {
            public VisualElement Element;
            public Label Label;
            public Func<bool> IsOn;
            public Func<string> Text;
        }

        /// <summary>True if the active tool belongs to group 0 (power), 1 (water) or 2 (services).</summary>
        private bool InGroup(int group)
        {
            if (tools.CurrentTool == ToolType.Utility)
            {
                return group == (tools.UtilityKind == UtilityKind.Power ? 0 : 1);
            }

            if (tools.CurrentTool == ToolType.Facility)
            {
                BuildingShape shape = tools.FacilityShape;
                if (shape == BuildingShape.PowerPlant) { return group == 0; }
                if (shape == BuildingShape.WaterTower || shape == BuildingShape.SewageOutlet) { return group == 1; }
                return group == 2;
            }

            return false;
        }

        private void SelectFirstService()
        {
            tools.SelectFacility(BuildingShape.Hospital);
        }

        private (string, Action, Func<bool>, Func<string>) FacilityEntry(BuildingShape shape)
        {
            return (CivicInfo.Name(shape), (Action)(() =>
                {
                    if (sim.IsUnlocked(shape)) { tools.SelectFacility(shape); }
                    else
                    {
                        Feature f = CivicInfo.RequiredFeature(shape).Value;
                        Notify("The " + CityMilestones.Name(f) + " unlocks at a population milestone (see the bar at the top).", 5f);
                    }
                }),
                (Func<bool>)(() => tools.CurrentTool == ToolType.Facility && tools.FacilityShape == shape),
                (Func<string>)(() => CivicInfo.Name(shape) + "  " + CitySimulation.FormatMoney(CivicInfo.Cost(shape)) + (sim.IsUnlocked(shape) ? "" : "  (locked)")));
        }

        private VisualElement BuildGroupRow((string, Action, Func<bool>, Func<string>)[] entries)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            foreach (var entry in entries)
            {
                VisualElement chip = Chip(entry.Item4(), entry.Item2, null);
                var chipInfo = new GroupChip { Element = chip, Label = (Label)chip.ElementAt(0), IsOn = entry.Item3, Text = entry.Item4 };
                groupChips.Add(chipInfo);
                row.Add(chip);
            }

            return row;
        }

        private void RefreshGroupChips()
        {
            for (int i = 0; i < groupChips.Count; i++)
            {
                GroupChip chip = groupChips[i];
                SetOn(chip.Element, "ug-chip--on", chip.IsOn());
                SetText(chip.Label, chip.Text());
            }
        }

        private VisualElement IconButtonSmall(string iconName, Action onClick)
        {
            VisualElement button = IconButton(iconName, true, onClick);
            return button;
        }

        private void AddTool(VisualElement bar, string iconName, string label, bool available, Func<bool> isOn, Action click)
        {
            var tool = new VisualElement();
            tool.AddToClassList("ug-tool");
            if (!available)
            {
                tool.AddToClassList("ug-tool--disabled");
            }

            tool.Add(Icon(iconName, "ug-icon--tool"));
            Label text = Text(label, null, available ? Weight.Regular : Weight.Regular);
            tool.Add(text);
            var indicator = new VisualElement();
            indicator.AddToClassList("ug-indicator");
            indicator.pickingMode = PickingMode.Ignore;
            tool.Add(indicator);

            string toolName = label;
            if (available)
            {
                tool.RegisterCallback<ClickEvent>(e => click());
            }
            else
            {
                tool.RegisterCallback<ClickEvent>(e => Notify(toolName + " is not part of this milestone yet."));
            }

            bar.Add(tool);
            toolEntries.Add(new ToolEntry { Name = label, Element = tool, Available = available, IsOn = isOn, Click = click });
        }

        private void BuildStatusBar()
        {
            VisualElement panel = Panel("Simulation status", true);
            Place(panel, 24, null, 24, 24);
            panel.style.height = 52;
            panel.style.justifyContent = Justify.SpaceBetween;
            panel.style.paddingLeft = 16;
            panel.style.paddingRight = 16;

            // Clock
            var clockGroup = new VisualElement();
            clockGroup.style.flexDirection = FlexDirection.Row;
            clockGroup.style.alignItems = Align.Center;
            var date = new VisualElement();
            dateLabel = Text("", null, Weight.Medium);
            timeLabel = Text("", "ug-text--muted", Weight.Regular);
            timeLabel.style.marginTop = 4;
            date.Add(dateLabel);
            date.Add(timeLabel);
            date.style.marginRight = 12;
            date.style.minWidth = 75;
            clockGroup.Add(date);

            pauseButton = IconButton("pause", true, () => clock.SetSpeedLevel(0));
            playButton = IconButton("play", true, () => clock.SetSpeedLevel(Mathf.Max(1, lastRunningSpeed)));
            var speed = new VisualElement();
            speed.AddToClassList("ug-btn");
            speed.AddToClassList("ug-btn--28");
            speedLabel = Text("1×", "ug-text--accent", Weight.SemiBold);
            speedLabel.style.fontSize = 11;
            speed.Add(speedLabel);
            speed.RegisterCallback<ClickEvent>(e => CycleSpeed());
            VisualElement fast = IconButton("fast-forward", true, () => clock.SetSpeedLevel(Mathf.Min(TickClock.MaxSpeedLevel, Mathf.Max(1, clock.SpeedLevel) + (clock.IsPaused ? 0 : 1))));
            pauseButton.style.marginRight = 2;
            playButton.style.marginRight = 2;
            speed.style.marginRight = 2;
            clockGroup.Add(pauseButton);
            clockGroup.Add(playButton);
            clockGroup.Add(speed);
            clockGroup.Add(fast);
            panel.Add(clockGroup);

            panel.Add(Divider(true));

            VisualElement treasury = Metric("wallet", "Treasury", out treasuryValue, out treasuryChange);
            treasuryValue.text = "—";
            treasuryChange.text = "";
            panel.Add(treasury);
            panel.Add(Metric("users", "Population", out populationValue, out populationChange));
            panel.Add(Metric("building-2", "Jobs", out jobsValue, out jobsChange));

            panel.Add(Divider(true));

            // Demand
            var demand = new VisualElement();
            demand.style.flexDirection = FlexDirection.Row;
            demand.style.alignItems = Align.Center;
            Label demandLabel = Text("Demand", "ug-text--muted", Weight.Regular);
            demandLabel.style.marginRight = 12;
            demand.Add(demandLabel);
            string[] letters = { "R", "C", "I" };
            string[] colours = { "ug-bg--r", "ug-bg--c", "ug-bg--i" };
            for (int i = 0; i < 3; i++)
            {
                var zone = new VisualElement();
                zone.style.width = 14;
                zone.style.alignItems = Align.Center;
                if (i < 2)
                {
                    zone.style.marginRight = 5;
                }

                var track = new VisualElement();
                track.AddToClassList("ug-track");
                track.AddToClassList("ug-track--v");
                var fill = new VisualElement();
                fill.AddToClassList("ug-fill");
                fill.AddToClassList("ug-fill--v");
                fill.AddToClassList(colours[i]);
                track.Add(fill);
                demandFill[i] = fill;
                zone.Add(track);
                Label letter = Text(letters[i], "ug-text--soft", Weight.Medium);
                letter.style.fontSize = 8;
                letter.style.marginTop = 3;
                zone.Add(letter);
                demand.Add(zone);
            }

            panel.Add(demand);
            panel.Add(Divider(true));

            // Milestone
            var milestone = new VisualElement();
            milestone.style.width = 132;
            var target = new VisualElement();
            target.style.flexDirection = FlexDirection.Row;
            target.style.justifyContent = Justify.SpaceBetween;
            target.Add(Text("Next milestone", "ug-text--muted ug-text--soft", Weight.Regular));
            milestoneTarget = Text("", "ug-text--muted", Weight.Regular);
            target.Add(milestoneTarget);
            milestone.Add(target);
            var progress = new VisualElement();
            progress.AddToClassList("ug-track");
            progress.AddToClassList("ug-track--h");
            progress.style.marginTop = 6;
            milestoneFill = new VisualElement();
            milestoneFill.AddToClassList("ug-fill");
            milestoneFill.AddToClassList("ug-fill--h");
            progress.Add(milestoneFill);
            milestone.Add(progress);
            milestoneDetail = Text("", "ug-text--muted ug-text--soft", Weight.Regular);
            milestoneDetail.style.fontSize = 10;
            milestoneDetail.style.marginTop = 4;
            milestone.Add(milestoneDetail);
            panel.Add(milestone);
        }

        // The scale: a bar whose length is a round number of metres at the current zoom, plus the sizes that matter
        // when placing things (a lot is 8 m, a road 16 m wide).
        private void BuildScale()
        {
            VisualElement panel = Panel("Scale", false);
            Place(panel, 24, null, null, 96);
            panel.style.alignItems = Align.FlexStart;
            panel.style.paddingLeft = 12;
            panel.style.paddingRight = 12;
            panel.style.paddingTop = 8;
            panel.style.paddingBottom = 8;

            scaleLabel = Text("", "ug-text--soft", Weight.Medium);
            scaleLabel.style.fontSize = 11;
            panel.Add(scaleLabel);

            scaleBar = new VisualElement();
            scaleBar.style.height = 6;
            scaleBar.style.width = 80;
            scaleBar.style.marginTop = 3;
            scaleBar.style.borderLeftWidth = 2;
            scaleBar.style.borderRightWidth = 2;
            scaleBar.style.borderBottomWidth = 2;
            Color line = new Color32(176, 189, 201, 255);
            scaleBar.style.borderLeftColor = line;
            scaleBar.style.borderRightColor = line;
            scaleBar.style.borderBottomColor = line;
            scaleBar.pickingMode = PickingMode.Ignore;
            panel.Add(scaleBar);

            Label sizes = Text("1 lot = " + RoadNetwork.LotSize.ToString("0", CultureInfo.InvariantCulture)
                + " m  ·  " + RoadSpecs.Name(tools.RoadType).ToLowerInvariant() + " " + RoadSpecs.Width(tools.RoadType).ToString("0", CultureInfo.InvariantCulture) + " m wide", "ug-text--muted", Weight.Regular);
            sizes.style.marginTop = 6;
            panel.Add(sizes);
        }

        // A small tag that follows the cursor while a road is being drawn and tells how long it is.
        private void BuildMeasureTag()
        {
            measureTag = new VisualElement { name = "Measure" };
            measureTag.AddToClassList("ug-hint");
            measureTag.pickingMode = PickingMode.Ignore;
            measureTag.style.display = DisplayStyle.None;
            measureLabel = Text("", null, Weight.SemiBold);
            measureLabel.style.whiteSpace = WhiteSpace.Normal;
            measureTag.Add(measureLabel);
            root.Add(measureTag);
        }

        private void RefreshMeasure()
        {
            string text = tools.MeasureText;
            bool show = !string.IsNullOrEmpty(text) && Mouse.current != null && root.panel != null;
            SetDisplay(measureTag, show);
            if (!show)
            {
                return;
            }

            SetText(measureLabel, text);
            Vector2 mouse = Mouse.current.position.ReadValue();
            Vector2 p = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(mouse.x, Screen.height - mouse.y));
            measureTag.style.left = p.x + 18f;
            measureTag.style.top = p.y + 22f;
        }

        private void RefreshScale()
        {
            if (rig == null || root.panel == null)
            {
                return;
            }

            // Screen pixels to HUD pixels (the HUD scales with the screen size).
            Vector2 a = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(0f, 0f));
            Vector2 b = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(1000f, 0f));
            float panelPerScreen = Mathf.Abs(b.x - a.x) / 1000f;
            float pixelsPerMetre = rig.PixelsPerMetre() * panelPerScreen;

            float[] steps = { 5f, 10f, 20f, 50f, 100f, 200f, 500f, 1000f, 2000f };
            float metres = steps[0];
            for (int i = 0; i < steps.Length; i++)
            {
                if (steps[i] * pixelsPerMetre <= 150f)
                {
                    metres = steps[i];
                }
            }

            scaleBar.style.width = Mathf.Max(8f, metres * pixelsPerMetre);
            if (!Mathf.Approximately(metres, shownScaleMetres))
            {
                shownScaleMetres = metres;
                scaleLabel.text = metres.ToString("0", CultureInfo.InvariantCulture) + " m";
            }
        }

        private VisualElement Metric(string iconName, string label, out Label value, out Label change)
        {
            var metric = new VisualElement();
            metric.style.flexDirection = FlexDirection.Row;
            metric.style.alignItems = Align.Center;
            VisualElement icon = Icon(iconName, "ug-icon--metric");
            icon.style.marginRight = 10;
            metric.Add(icon);

            var details = new VisualElement();
            details.Add(Text(label, "ug-text--muted", Weight.Regular));
            var line = new VisualElement();
            line.style.flexDirection = FlexDirection.Row;
            line.style.alignItems = Align.FlexEnd;
            line.style.marginTop = 4;
            value = Text("", "ug-text--value", Weight.Medium);
            change = Text("", "ug-text--muted ug-text--good", Weight.Regular);
            change.style.marginLeft = 10;
            line.Add(value);
            line.Add(change);
            details.Add(line);
            metric.Add(details);
            return metric;
        }

        private void CycleSpeed()
        {
            int next = clock.IsPaused ? Mathf.Max(1, lastRunningSpeed) : clock.SpeedLevel + 1;
            if (next > TickClock.MaxSpeedLevel)
            {
                next = 1;
            }

            clock.SetSpeedLevel(next);
        }

        // ---------------------------------------------------------------------------------------------
        // Per-frame refresh

        private void Refresh()
        {
            sim.RefreshEconomyIfStale();
            if (!clock.IsPaused)
            {
                lastRunningSpeed = clock.SpeedLevel;
            }

            // Clock
            DateTime now = StartDate.AddMinutes(sim.TickCount * GameTime.MinutesPerTick);
            SetText(dateLabel, now.ToString("d MMM yyyy", CultureInfo.InvariantCulture));
            SetText(timeLabel, now.ToString("HH:mm", CultureInfo.InvariantCulture));
            SetText(speedLabel, (clock.IsPaused ? Mathf.Max(1, lastRunningSpeed) : clock.SpeedLevel) + "×");
            SetOn(pauseButton, "ug-btn--paused", clock.IsPaused);
            SetOn(playButton, "ug-btn--on", !clock.IsPaused);

            // Tools
            if (tools.IsZoneTool)
            {
                lastZoneTool = tools.CurrentTool;
            }

            for (int i = 0; i < toolEntries.Count; i++)
            {
                ToolEntry entry = toolEntries[i];
                if (entry.IsOn != null)
                {
                    SetOn(entry.Element, "ug-tool--on", entry.IsOn());
                }
            }

            bool road = tools.CurrentTool == ToolType.Road;
            bool zone = tools.IsZoneTool;
            bool brush = tools.IsBrushTool;
            bool junction = tools.CurrentTool == ToolType.Junction;
            bool upgrade = tools.CurrentTool == ToolType.Upgrade;
            bool group = InGroup(0) || InGroup(1) || InGroup(2);
            SetDisplay(optionsPanel, road || brush || junction || upgrade || group);
            for (int g = 0; g < groupOptions.Length; g++) { SetDisplay(groupOptions[g], InGroup(g)); }
            RefreshGroupChips();
            SetDisplay(upgradeOptions, upgrade);
            if (upgrade)
            {
                for (int i = 0; i < upgradeChips.Length; i++)
                {
                    SetOn(upgradeChips[i], "ug-chip--on", tools.UpgradeType == RoadSpecs.All[i]);
                }
            }

            SetDisplay(roadOptions, road);
            SetDisplay(junctionOptions, junction);
            SetDisplay(zoneOptions, zone);
            SetDisplay(brushOptions, brush);
            if (road)
            {
                SetOn(chipStraight, "ug-chip--on", tools.RoadMode == RoadMode.Straight);
                SetOn(chipCurved, "ug-chip--on", tools.RoadMode == RoadMode.Curved);
                for (int i = 0; i < typeChips.Length; i++)
                {
                    SetOn(typeChips[i], "ug-chip--on", tools.RoadType == RoadSpecs.All[i]);
                }
            }

            if (junction)
            {
                RoadNode picked = tools.SelectedJunction;
                SetDisplay(lightsChip, picked != null && picked.CanHaveTrafficLights);
                SetDisplay(resetLanesChip, picked != null && picked.BlockedTurns.Count > 0);
                if (picked == null)
                {
                    SetText(junctionLabel, "Click an intersection to select it");
                }
                else
                {
                    SetText(junctionLabel, (picked.Degree == 4 ? "4-way" : picked.Degree == 3 ? "3-way" : picked.Degree + "-way") + (picked.CanHaveTrafficLights ? " intersection" : " highway junction"));
                    SetText(lightsChipLabel, picked.HasTrafficLight ? "Traffic lights: on" : "Traffic lights: off");
                    SetOn(lightsChip, "ug-chip--on", picked.HasTrafficLight);
                }
            }

            if (zone)
            {
                SetOn(zoneChips[0], "ug-chip--on", tools.CurrentTool == ToolType.ZoneResidential);
                SetOn(zoneChips[1], "ug-chip--on", tools.CurrentTool == ToolType.ZoneCommercial);
                SetOn(zoneChips[2], "ug-chip--on", tools.CurrentTool == ToolType.ZoneIndustrial);
                SetOn(zoneChips[3], "ug-chip--on", tools.CurrentTool == ToolType.ZoneClear);
                SetOn(zoneChips[4], "ug-chip--on", tools.CurrentTool == ToolType.ZoneDense);
                SetOn(zoneChips[4], "ug-chip--locked", !sim.IsUnlocked(Feature.DenseResidential));
                SetOn(chipFill, "ug-chip--on", tools.FillMode);
            }

            if (brush)
            {
                bool fillActive = zone && tools.FillMode;
                SetDisplay(brushLabel.parent, !fillActive);
                SetText(brushLabel, "Brush " + Mathf.RoundToInt(tools.BrushRadius * 2f) + " m");
            }

            // Camera
            if (rig != null)
            {
                compassArrow.style.rotate = new StyleRotate(new Rotate(Angle.Degrees(-rig.YawDegrees)));
            }

            UpdateInspect();
            UpdateSummary();
            RefreshMinimap();

            // Advisory: the tool's live message first, then a transient notice, then a hint for what to do next.
            string title;
            string detail;
            bool hasMessage = Time.unscaledTime < messageUntil;
            string toolStatus = tools.StatusText;
            string advice = sim.Economy.Advice;
            if (hasMessage)
            {
                title = message;
                detail = "";
            }
            else if (!string.IsNullOrEmpty(toolStatus))
            {
                title = toolStatus;
                detail = "";
            }
            else if (tools.CurrentTool == ToolType.None)
            {
                if (!string.IsNullOrEmpty(advice))
                {
                    title = advice;
                    detail = "The demand bars at the bottom show what the city wants.";
                }
                else
                {
                    title = sim.Roads.SegmentCount == 0 ? "Start by laying a road" : "Your city is running smoothly";
                    detail = "Pick a tool below, or press 1–6. Space pauses.";
                }
            }
            else
            {
                title = ToolController.ToolLabel(tools.CurrentTool) + " tool selected";
                detail = !string.IsNullOrEmpty(advice) ? advice : "Esc or right-click drops the tool.";
            }

            bool warn = !string.IsNullOrEmpty(advice) && !hasMessage && string.IsNullOrEmpty(toolStatus);
            SetOn(advisoryPanel, "ug-advisory--warn", warn);
            SetOn(advisoryPanel, "ug-advisory--ok", !warn);

            SetText(advisoryTitle, title);
            SetText(advisoryDetail, detail);
            SetDisplay(advisoryDetail, !string.IsNullOrEmpty(detail));

            RefreshMeasure();
            RefreshScale();

            if (Time.unscaledTime - statsUpdatedAt > StatsRefreshSeconds)
            {
                statsUpdatedAt = Time.unscaledTime;
                RefreshStats();
            }
        }

        private void RefreshStats()
        {
            CityTreasury treasury = sim.Treasury;
            if (treasury.IsEnabled)
            {
                SetText(treasuryValue, CitySimulation.FormatMoney(treasury.Balance));
                double net = treasury.WeeklyNet;
                SetText(treasuryChange, (net >= 0 ? "+" : "-") + CitySimulation.FormatMoney(Math.Abs(net)) + " / wk");
                treasuryChange.EnableInClassList("ug-text--good", net >= 0);
                treasuryChange.EnableInClassList("ug-text--bad", net < 0);
                treasuryValue.EnableInClassList("ug-text--bad", treasury.Balance < LowMoney);
                if (treasury.Balance < LowMoney && !lowMoneyWarned)
                {
                    lowMoneyWarned = true;
                    Notify("Low funds: only " + CitySimulation.FormatMoney(treasury.Balance) + " left. Roads cost money, tax income pays for them.", 6f);
                }
                else if (treasury.Balance >= LowMoney * 1.5)
                {
                    lowMoneyWarned = false;
                }

                if (net < 0 && sim.BuildingCount > 0 && !deficitWarned)
                {
                    deficitWarned = true;
                    Notify("Expenses (" + CitySimulation.FormatMoney(treasury.WeeklyUpkeep) + "/wk) are above income (" + CitySimulation.FormatMoney(treasury.WeeklyIncome) + "/wk). The treasury is shrinking.", 7f);
                }
                else if (net >= 0)
                {
                    deficitWarned = false;
                }
            }

            // City numbers. There is no economy yet: population is an estimate from the residential buildings,
            // and demand is how much of the zoned land is still empty.
            int population = 0;
            foreach (Building b in sim.Buildings)
            {
                population += b.Residents;
            }

            if (lastPopulation < 0)
            {
                lastPopulation = population;
                populationSampledAt = Time.unscaledTime;
            }
            else if (Time.unscaledTime - populationSampledAt > 5f)
            {
                populationTrend = population - lastPopulation;
                lastPopulation = population;
                populationSampledAt = Time.unscaledTime;
            }

            EconomySnapshot jobs = sim.Economy.Snapshot;
            SetText(jobsValue, Mathf.RoundToInt((float)jobs.Employed) + " / " + Mathf.RoundToInt((float)jobs.Jobs));
            string jobsNote = jobs.Unemployed >= 5.0 ? Mathf.RoundToInt((float)jobs.Unemployed) + " jobless: more jobs needed"
                : jobs.UnfilledJobs >= 5.0 ? Mathf.RoundToInt((float)jobs.UnfilledJobs) + " free jobs: more homes needed" : "";
            SetText(jobsChange, jobsNote);
            jobsChange.EnableInClassList("ug-text--bad", jobs.Unemployed >= 5.0);
            jobsChange.EnableInClassList("ug-text--good", jobs.Unemployed < 5.0);

            SetText(populationValue, population.ToString("N0", CultureInfo.InvariantCulture));
            SetText(populationChange, populationTrend > 0 ? "+" + populationTrend.ToString("0", CultureInfo.InvariantCulture) : "");

            // Demand comes from the economy: jobs, goods and shops between the zones (see CityEconomy).
            CityEconomy economy = sim.Economy;
            double[] demand = { economy.ResidentialDemand, economy.CommercialDemand, economy.IndustrialDemand };
            for (int i = 0; i < 3; i++)
            {
                demandFill[i].style.height = new StyleLength(new Length(Mathf.Clamp01((float)demand[i]) * 100f, LengthUnit.Percent));
            }

            EconomySnapshot snapshot = economy.Snapshot;
            int target = Milestones[Milestones.Length - 1];
            int targetIndex = Milestones.Length - 1;
            int previous = 0;
            for (int i = 0; i < Milestones.Length; i++)
            {
                if (population < Milestones[i])
                {
                    target = Milestones[i];
                    targetIndex = i;
                    previous = i > 0 ? Milestones[i - 1] : 0;
                    break;
                }
            }

            SetText(milestoneTarget, FormatShort(target));
            string rewardText = CityMilestones.Reward(targetIndex);
            SetText(milestoneDetail, "Reach " + target.ToString("N0", CultureInfo.InvariantCulture) + " residents (now " + population.ToString("N0", CultureInfo.InvariantCulture) + ")"
                + (rewardText.Length > 0 ? " and it " + rewardText : ""));
            float progress = Mathf.Clamp01((population - previous) / (float)(target - previous));
            milestoneFill.style.width = new StyleLength(new Length(progress * 100f, LengthUnit.Percent));

            SetText(cityDetail, sim.BuildingCount + " buildings · " + sim.Roads.SegmentCount + " roads");
        }

        private static string FormatShort(int value)
        {
            if (value >= 1000000)
            {
                return (value / 1000000f).ToString("0.#", CultureInfo.InvariantCulture) + "M";
            }

            if (value >= 1000)
            {
                return (value / 1000f).ToString("0.#", CultureInfo.InvariantCulture) + "k";
            }

            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static void SetText(Label label, string text)
        {
            if (label.text != text)
            {
                label.text = text;
            }
        }

        private static void SetDisplay(VisualElement element, bool visible)
        {
            DisplayStyle wanted = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (element.style.display != wanted)
            {
                element.style.display = wanted;
            }
        }
    }
}
