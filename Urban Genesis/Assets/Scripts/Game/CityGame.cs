using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UrbanGenesis.Simulation;
using UrbanGenesis.Simulation.Traffic;

namespace UrbanGenesis.Game
{
    /// <summary>
    /// The entry point. It owns the simulation, runs it on a fixed tick, and builds the camera,
    /// renderer, tools and toolbar as child objects. Everything is created from code, so no scene
    /// setup is needed: <see cref="GameFlow"/> creates this when the player starts or loads a city from the main menu.
    /// </summary>
    public sealed class CityGame : MonoBehaviour
    {
        [Header("City size")]
        [Tooltip("Side length of the square map, in metres. Roads and lots only exist inside it.")]
        [SerializeField] private float mapSize = 4096f;

        [Header("Money")]
        [Tooltip("Money at the start. Zones and buildings are free; roads cost money (see CityTreasury).")]
        [SerializeField] private float startingFunds = 100000f;

        [Header("Simulation")]
        [SerializeField] private int randomSeed = 12345;
        [Tooltip("Real seconds per simulation tick at 1x speed.")]
        [SerializeField] private float secondsPerTick = 0.5f;

        /// <summary>The city's name: random for a new city, from the save for a loaded one.</summary>
        public string CityName { get; private set; }

        /// <summary>The save slot (1 to <see cref="SaveStore.SlotCount"/>) this city was loaded from or last saved to; 0 if it has not been saved yet.</summary>
        public int Slot { get; set; }

        public CitySimulation Simulation { get; private set; }
        public TickClock Clock { get; private set; }
        public CityTraffic Traffic { get; private set; }

        /// <summary>Raised when the player asks to go back to the main menu. The game flow then destroys this object.</summary>
        public event Action MainMenuRequested;

        private HudToolbar hud;
        private CameraRig rig;
        private ToolController tools;
        private PauseMenu pauseMenu;
        private bool menuOpen;

        /// <summary>
        /// Starts the city: a brand-new one when <paramref name="save"/> is null, otherwise the saved one.
        /// (Called by <see cref="GameFlow"/>, right after this object is created.)
        /// </summary>
        private DateTime gameStart;

        public void Begin(SaveGame save, string notice = null)
        {
            int skipped = 0;
            if (save == null)
            {
                CityName = CityNames.PickRandom();
                Simulation = new CitySimulation(mapSize, randomSeed);
                Simulation.Treasury.Enable(startingFunds);
            }
            else
            {
                CityName = string.IsNullOrEmpty(save.CityName) ? CityNames.PickRandom() : save.CityName;
                Simulation = CitySimulation.Restore(save.City, out skipped);
            }

            Clock = new TickClock(secondsPerTick, 8);
            if (save != null)
            {
                Clock.SetSpeedLevel(save.SpeedLevel);
            }

            CityRenderer cityRenderer = CreateChild<CityRenderer>("City Renderer");
            hud = CreateChild<HudToolbar>("Toolbar");
            rig = CreateChild<CameraRig>("Camera Rig");
            tools = CreateChild<ToolController>("Tools");

            Traffic = new CityTraffic(Simulation, randomSeed);
            TrafficRenderer trafficRenderer = CreateChild<TrafficRenderer>("Traffic Renderer");
            trafficRenderer.Init(Traffic);

            cityRenderer.Init(Simulation);
            cityRenderer.SetTraffic(Traffic);
            rig.Init(hud, (float)(Simulation.Roads.WorldBounds.MaxX));
            tools.Init(Simulation, rig, cityRenderer, hud);
            tools.Traffic = Traffic;
            Simulation.RandomEventsEnabled = true;
            Simulation.RequireUtilities = true;
            Simulation.EventHappened += message => hud.Notify(message, 8f);
            tools.BeforeChange += PushUndo;
            tools.ChangeDiscarded += DiscardUndo;
            hud.UndoRequested += Undo;
            hud.RedoRequested += Redo;
            rig.LeftDragPanEnabled = () => tools.CurrentTool == ToolType.None;
            gameStart = save != null && save.StartDateTicks > 0 ? new DateTime(save.StartDateTicks) : DateTime.Today.AddHours(9);
            hud.GameStart = gameStart;
            hud.Init(Simulation, Clock, tools, rig, CityName);

            pauseMenu = CreateChild<PauseMenu>("Pause Menu");
            pauseMenu.Init(this, tools);
            hud.MenuRequested += pauseMenu.Open;
            hud.MainMenuRequested += pauseMenu.OpenLeaveConfirm;
            hud.SaveRequested += () =>
            {
                if (Slot < 1)
                {
                    pauseMenu.OpenSave();
                    return;
                }

                string error = SaveToDisk(Slot);
                hud.Notify(error ?? "Game saved to slot " + Slot + ".", 3f);
            };

            if (save == null)
            {
                // Until there is terrain, a highway to the outside world appears at a random place on every new city.
                CitySimulation.OutsideConnection connection = Simulation.CreateOutsideConnection(new System.Random());
                Simulation.CreateRiver(new System.Random());
                if (connection != null)
                {
                    Vec2 meet = connection.CityNode.Position;
                    rig.FocusOn((float)meet.X, (float)meet.Y);
                    hud.Notify("A highway from the outside world reaches your city. Build roads out from its end.", 8f);
                }
            }
            else
            {
                rig.RestoreView(save);
                hud.Notify(notice ?? (skipped > 0 ? "Game loaded. " + skipped + " saved items no longer fit and were dropped." : "Game loaded."), 4f);
            }

            EnsureLight();
        }

        // ------------------------------------------------------------ undo and redo
        // Each change the player makes first stores a full snapshot of the city. Undo rebuilds the city from the last
        // snapshot (and keeps the camera where it is), so every kind of change can be undone the same way.

        private const int MaxUndo = 20;
        private readonly List<SaveGame> undoStack = new List<SaveGame>();
        private readonly List<SaveGame> redoStack = new List<SaveGame>();
        private bool rebuilding;

        private void PushUndo()
        {
            undoStack.Add(CaptureSave());
            if (undoStack.Count > MaxUndo) { undoStack.RemoveAt(0); }
            redoStack.Clear();
        }

        private void DiscardUndo()
        {
            if (undoStack.Count > 0) { undoStack.RemoveAt(undoStack.Count - 1); }
        }

        public void Undo()
        {
            if (rebuilding || Simulation == null) { return; }
            if (undoStack.Count == 0) { hud.Notify("Nothing to undo.", 2f); return; }
            SaveGame target = undoStack[undoStack.Count - 1];
            undoStack.RemoveAt(undoStack.Count - 1);
            redoStack.Add(CaptureSave());
            Reload(target, "Undone. Ctrl+Y redoes it.");
        }

        public void Redo()
        {
            if (rebuilding || Simulation == null) { return; }
            if (redoStack.Count == 0) { hud.Notify("Nothing to redo.", 2f); return; }
            SaveGame target = redoStack[redoStack.Count - 1];
            redoStack.RemoveAt(redoStack.Count - 1);
            undoStack.Add(CaptureSave());
            Reload(target, "Redone.");
        }

        private void Reload(SaveGame target, string notice)
        {
            var view = new SaveGame();
            rig.CaptureView(view);
            target.CameraX = view.CameraX;
            target.CameraZ = view.CameraZ;
            target.CameraYaw = view.CameraYaw;
            target.CameraDistance = view.CameraDistance;
            target.SpeedLevel = Clock.SpeedLevel;
            ToolType tool = tools.CurrentTool;

            rebuilding = true;
            Simulation = null;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            StartCoroutine(Rebuild(target, notice, tool));
        }

        private IEnumerator Rebuild(SaveGame target, string notice, ToolType tool)
        {
            yield return null; // let the old objects go first
            Begin(target, notice);
            tools.SetTool(tool);
            rebuilding = false;
        }

        /// <summary>True while the pause menu is open: the clock stops and the camera and tools ignore input.</summary>
        public bool MenuOpen
        {
            get { return menuOpen; }
            set
            {
                menuOpen = value;
                if (tools != null) { tools.InputBlocked = value; }
                if (rig != null) { rig.InputBlocked = value; }
            }
        }

        public SaveGame CaptureSave()
        {
            var save = new SaveGame();
            save.CityName = CityName;
            save.SavedAt = DateTime.Now.ToString("d MMM HH:mm", System.Globalization.CultureInfo.InvariantCulture);
            save.Population = Mathf.RoundToInt((float)Simulation.Economy.Snapshot.Residents);
            save.Buildings = Simulation.BuildingCount;
            save.Money = Simulation.Treasury.Balance;
            save.SpeedLevel = Clock.SpeedLevel;
            save.StartDateTicks = gameStart.Ticks;
            rig.CaptureView(save);
            save.City = Simulation.Capture();
            return save;
        }

        /// <summary>Writes the save to a slot and remembers it as this city's slot. Returns null on success, or a message about what went wrong.</summary>
        public string SaveToDisk(int slot)
        {
            string error = SaveStore.Write(slot, CaptureSave());
            if (error == null) { Slot = slot; }
            return error;
        }

        public void RequestMainMenu()
        {
            Action requested = MainMenuRequested;
            if (requested != null)
            {
                requested();
            }
        }

        private void Update()
        {
            if (Simulation == null || menuOpen)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed))
            {
                if (keyboard.zKey.wasPressedThisFrame)
                {
                    if (keyboard.leftShiftKey.isPressed) { Redo(); } else { Undo(); }
                }
                else if (keyboard.yKey.wasPressedThisFrame)
                {
                    Redo();
                }
            }

            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                Clock.TogglePause();
            }

            int ticksDue = Clock.Advance(Time.unscaledDeltaTime);
            for (int i = 0; i < ticksDue; i++)
            {
                Simulation.Step();
                Traffic.OnTick();
            }

            // Vehicles move in real time scaled by the game speed (nothing moves while paused).
            Traffic.Advance(Math.Min(Time.unscaledDeltaTime, 0.1f) * Clock.SpeedMultiplier);
        }

        private T CreateChild<T>(string childName) where T : Component
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            return child.AddComponent<T>();
        }

        // The project's sample scene already has a directional light. This only matters if the scene was emptied.
        private static void EnsureLight()
        {
            if (FindFirstObjectByType<Light>() != null)
            {
                return;
            }

            var lightObject = new GameObject("Sun");
            var sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
    }
}
