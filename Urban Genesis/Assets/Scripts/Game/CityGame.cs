using UnityEngine;
using UnityEngine.InputSystem;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Game
{
    /// <summary>
    /// The entry point. It owns the simulation, runs it on a fixed tick, and builds the camera,
    /// renderer, tools and toolbar as child objects. Everything is created from code, so no scene
    /// setup is needed: <see cref="GameBootstrap"/> adds this to the scene automatically when you press Play.
    /// </summary>
    public sealed class CityGame : MonoBehaviour
    {
        [Header("City size")]
        [Tooltip("Side length of the square map, in metres. Roads and lots only exist inside it.")]
        [SerializeField] private float mapSize = 1024f;

        [Header("Simulation")]
        [SerializeField] private int randomSeed = 12345;
        [Tooltip("Real seconds per simulation tick at 1x speed.")]
        [SerializeField] private float secondsPerTick = 0.5f;

        /// <summary>A random name for this launch's city (no saves or maps yet, so nothing else names it).</summary>
        public string CityName { get; private set; }

        public CitySimulation Simulation { get; private set; }
        public TickClock Clock { get; private set; }

        private void Awake()
        {
            CityName = CityNames.PickRandom();
            Simulation = new CitySimulation(mapSize, randomSeed);
            Clock = new TickClock(secondsPerTick, 8);

            CityRenderer cityRenderer = CreateChild<CityRenderer>("City Renderer");
            HudToolbar hud = CreateChild<HudToolbar>("Toolbar");
            CameraRig rig = CreateChild<CameraRig>("Camera Rig");
            ToolController tools = CreateChild<ToolController>("Tools");

            cityRenderer.Init(Simulation);
            rig.Init(hud, mapSize * 0.5f);
            tools.Init(Simulation, rig, cityRenderer, hud);
            hud.Init(Simulation, Clock, tools, rig, CityName);

            EnsureLight();
        }

        private void Update()
        {
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
