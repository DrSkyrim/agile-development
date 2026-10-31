using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
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
        private const float PersonsPerFloorPerLot = 2f;
        private const string IconFolder = "UI/Icons/";

        // One game tick is ten minutes on the clock in the status bar.
        private static readonly DateTime StartDate = new DateTime(2026, 5, 14, 9, 0, 0);
        private const double GameMinutesPerTick = 10.0;

        private static readonly int[] Milestones = { 500, 1000, 2500, 5000, 10000, 25000, 50000, 100000, 250000, 500000 };

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
        private Label populationValue;
        private Label populationChange;
        private Label happinessValue;
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
        private VisualElement chipFill;
        private readonly VisualElement[] zoneChips = new VisualElement[3];
        private Label brushLabel;
        private readonly List<ToolEntry> toolEntries = new List<ToolEntry>();

        private int lastRunningSpeed = 1;
        private float statsUpdatedAt = -100f;
        private int lastPopulation = -1;
        private float populationSampledAt;
        private float populationTrend;
        private ToolType lastZoneTool = ToolType.ZoneResidential;
        private string message = "";
        private float messageUntil;

        public void Init(CitySimulation simulation, TickClock tickClock, ToolController toolController, CameraRig cameraRig)
        {
            sim = simulation;
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

            Refresh();
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

        private void Notify(string text)
        {
            message = text;
            messageUntil = Time.unscaledTime + 3f;
        }

        // ---------------------------------------------------------------------------------------------
        // Panels

        private void BuildIdentity()
        {
            VisualElement panel = Panel("City identity", true);
            Place(panel, 24, 24, null, null);
            Size(panel, 258, 64);
            panel.style.paddingLeft = 14;
            panel.style.paddingRight = 14;

            var emblem = new VisualElement();
            emblem.AddToClassList("ug-emblem");
            emblem.Add(Icon("building-2", null));
            panel.Add(emblem);

            var details = new VisualElement();
            details.style.flexGrow = 1;
            details.style.marginLeft = 12;
            details.Add(Text("Northhaven", "ug-text--title", Weight.SemiBold));

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

            panel.Add(IconButton("bell", false, () => Notify("Notifications are not available yet.")));
            panel.Add(IconButton("save", false, () => Notify("Saving is not available yet.")));
            panel.Add(IconButton("settings-2", false, () => Notify("Settings are not available yet.")));
            panel.Add(IconButton("menu", false, () => Notify("The main menu is not available yet.")));
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
            optionsPanel.Add(roadOptions);

            zoneOptions = new VisualElement();
            zoneOptions.style.flexDirection = FlexDirection.Row;
            zoneOptions.style.alignItems = Align.Center;
            zoneChips[0] = Chip("Residential", () => tools.SetTool(ToolType.ZoneResidential), "ug-dot--r");
            zoneChips[1] = Chip("Commercial", () => tools.SetTool(ToolType.ZoneCommercial), "ug-dot--c");
            zoneChips[2] = Chip("Industrial", () => tools.SetTool(ToolType.ZoneIndustrial), "ug-dot--i");
            zoneOptions.Add(zoneChips[0]);
            zoneOptions.Add(zoneChips[1]);
            zoneOptions.Add(zoneChips[2]);
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
            AddTool(bar, "grid-2x2", "Zoning", true,
                () => tools.IsZoneTool,
                () => tools.SetTool(tools.IsZoneTool ? ToolType.None : lastZoneTool));
            AddTool(bar, "zap", "Power", false, null, null);
            AddTool(bar, "droplets", "Water", false, null, null);
            AddTool(bar, "shield-plus", "Services", false, null, null);
            AddTool(bar, "tram-front", "Transit", false, null, null);
            AddTool(bar, "trees", "Parks", false, null, null);
            AddTool(bar, "mountain", "Terrain", false, null, null);
            VisualElement barDivider = Divider(true);
            barDivider.style.height = 36;
            bar.Add(barDivider);
            AddTool(bar, "pickaxe", "Demolish", true,
                () => tools.CurrentTool == ToolType.Bulldoze,
                () => tools.SetTool(tools.CurrentTool == ToolType.Bulldoze ? ToolType.None : ToolType.Bulldoze));
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

            Label treasuryChange;
            Label happinessChange;
            VisualElement treasury = Metric("wallet", "Treasury", out treasuryValue, out treasuryChange);
            treasuryValue.text = "—";
            treasuryChange.text = "";
            panel.Add(treasury);
            panel.Add(Metric("users", "Population", out populationValue, out populationChange));
            VisualElement happiness = Metric("smile", "Happiness", out happinessValue, out happinessChange);
            happinessValue.text = "—";
            happinessChange.text = "";
            panel.Add(happiness);

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
            panel.Add(milestone);
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
            if (!clock.IsPaused)
            {
                lastRunningSpeed = clock.SpeedLevel;
            }

            // Clock
            DateTime now = StartDate.AddMinutes(sim.TickCount * GameMinutesPerTick);
            SetText(dateLabel, now.ToString("d MMM yyyy", CultureInfo.InvariantCulture));
            SetText(timeLabel, now.ToString("HH:mm", CultureInfo.InvariantCulture));
            SetText(speedLabel, (clock.IsPaused ? Mathf.Max(1, lastRunningSpeed) : clock.SpeedLevel) + "×");
            SetOn(pauseButton, "ug-btn--on", clock.IsPaused);
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
            SetDisplay(optionsPanel, road || brush);
            SetDisplay(roadOptions, road);
            SetDisplay(zoneOptions, zone);
            SetDisplay(brushOptions, brush);
            if (road)
            {
                SetOn(chipStraight, "ug-chip--on", tools.RoadMode == RoadMode.Straight);
                SetOn(chipCurved, "ug-chip--on", tools.RoadMode == RoadMode.Curved);
            }

            if (zone)
            {
                SetOn(zoneChips[0], "ug-chip--on", tools.CurrentTool == ToolType.ZoneResidential);
                SetOn(zoneChips[1], "ug-chip--on", tools.CurrentTool == ToolType.ZoneCommercial);
                SetOn(zoneChips[2], "ug-chip--on", tools.CurrentTool == ToolType.ZoneIndustrial);
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

            // Advisory: the tool's live message first, then a transient notice, then a hint for what to do next.
            string title;
            string detail;
            bool hasMessage = Time.unscaledTime < messageUntil;
            string toolStatus = tools.StatusText;
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
                title = sim.Roads.SegmentCount == 0 ? "Start by laying a road" : "Your city is running smoothly";
                detail = "Pick a tool below, or press 1–5. Space pauses.";
            }
            else
            {
                title = ToolController.ToolLabel(tools.CurrentTool) + " tool selected";
                detail = "Esc or right-click drops the tool.";
            }

            SetText(advisoryTitle, title);
            SetText(advisoryDetail, detail);
            SetDisplay(advisoryDetail, !string.IsNullOrEmpty(detail));

            if (Time.unscaledTime - statsUpdatedAt > StatsRefreshSeconds)
            {
                statsUpdatedAt = Time.unscaledTime;
                RefreshStats();
            }
        }

        private void RefreshStats()
        {
            // City numbers. There is no economy yet: population is an estimate from the residential buildings,
            // and demand is how much of the zoned land is still empty.
            int population = 0;
            foreach (Building b in sim.Buildings)
            {
                if (b.Zone == ZoneType.Residential)
                {
                    population += Mathf.RoundToInt(b.Floors * b.Width * b.Depth * PersonsPerFloorPerLot);
                }
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

            SetText(populationValue, population.ToString("N0", CultureInfo.InvariantCulture));
            SetText(populationChange, populationTrend > 0 ? "+" + populationTrend.ToString("0", CultureInfo.InvariantCulture) : "");

            int[] zoned = new int[3];
            int[] free = new int[3];
            foreach (Lot lot in sim.Lots.Lots)
            {
                if (lot.Zone == ZoneType.None)
                {
                    continue;
                }

                int index = (int)lot.Zone - 1;
                zoned[index]++;
                if (lot.IsFree)
                {
                    free[index]++;
                }
            }

            for (int i = 0; i < 3; i++)
            {
                float level = zoned[i] == 0 ? 0f : (float)free[i] / zoned[i];
                demandFill[i].style.height = new StyleLength(new Length(Mathf.Max(level, zoned[i] == 0 ? 0f : 0.08f) * 100f, LengthUnit.Percent));
            }

            int target = Milestones[Milestones.Length - 1];
            int previous = 0;
            for (int i = 0; i < Milestones.Length; i++)
            {
                if (population < Milestones[i])
                {
                    target = Milestones[i];
                    previous = i > 0 ? Milestones[i - 1] : 0;
                    break;
                }
            }

            SetText(milestoneTarget, FormatShort(target));
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
