using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UrbanGenesis.Simulation;

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
        [SerializeField] private float startingFunds = 50000f;

        [Header("Simulation")]
        [SerializeField] private int randomSeed = 12345;
        [Tooltip("Real seconds per simulation tick at 1x speed.")]
        [SerializeField] private float secondsPerTick = 0.5f;

        /// <summary>The city's name: random for a new city, from the save for a loaded one.</summary>
        public string CityName { get; private set; }

        public CitySimulation Simulation { get; private set; }
        public TickClock Clock { get; private set; }

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
        public void Begin(SaveGame save)
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

            cityRenderer.Init(Simulation);
            rig.Init(hud, (float)(Simulation.Roads.WorldBounds.MaxX));
            tools.Init(Simulation, rig, cityRenderer, hud);
            hud.Init(Simulation, Clock, tools, rig, CityName);

            pauseMenu = CreateChild<PauseMenu>("Pause Menu");
            pauseMenu.Init(this, tools);
            hud.MenuRequested += pauseMenu.Open;

            if (save == null)
            {
                // Until there is terrain, a highway to the outside world appears at a random place on every new city.
                CitySimulation.OutsideConnection connection = Simulation.CreateOutsideConnection(new System.Random());
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
                hud.Notify(skipped > 0 ? "Game loaded. " + skipped + " saved items no longer fit and were dropped." : "Game loaded.", 4f);
            }

            EnsureLight();
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
            rig.CaptureView(save);
            save.City = Simulation.Capture();
            return save;
        }

        /// <summary>Writes the save file. Returns null on success, or a message about what went wrong.</summary>
        public string SaveToDisk()
        {
            return SaveStore.Write(CaptureSave());
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
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                Clock.TogglePause();
            }

            int ticksDue = Clock.Advance(Time.unscaledDeltaTime);
            for (int i = 0; i < ticksDue; i++)
            {
                Simulation.Step();
            }
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
