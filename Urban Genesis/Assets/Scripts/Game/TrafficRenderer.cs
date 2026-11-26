using System.Collections.Generic;
using UnityEngine;
using UrbanGenesis.Simulation;
using UrbanGenesis.Simulation.Traffic;

namespace UrbanGenesis.Game
{
    /// <summary>
    /// Draws the vehicles of <see cref="CityTraffic"/>. Until real models exist each vehicle is a little model built from
    /// cubes (a few car colours, a short truck and a long articulated truck). To use your own model, put a prefab at
    /// <c>Assets/Resources/Vehicles/Car</c>, <c>ShortTruck</c> or <c>LongTruck</c> (origin in the middle of the vehicle on the ground,
    /// front pointing along +Z, real-world size in metres); it is used instead of the placeholder.
    /// </summary>
    public sealed class TrafficRenderer : MonoBehaviour
    {
        private const float GroundHeight = 0.06f;

        private sealed class View
        {
            public Vehicle Vehicle;
            public Transform Root;
            public bool Placed;
        }

        private CityTraffic traffic;
        private readonly Dictionary<int, View> views = new Dictionary<int, View>();
        private static readonly string[] TypeNames = { "Car", "ShortTruck", "LongTruck", "Ambulance", "PoliceCar", "FireTruck", "SchoolBus" };
        private readonly List<Transform>[] pool = NewPool();

        private static List<Transform>[] NewPool()
        {
            var p = new List<Transform>[TypeNames.Length];
            for (int i = 0; i < p.Length; i++) { p[i] = new List<Transform>(); }
            return p;
        }
        private readonly Dictionary<int, GameObject> prefabs = new Dictionary<int, GameObject>();
        private Material[] carColors;
        private Material[] cabColors;
        private Material white, trailerBlue, trailerGray, dark, glass, red, policeBlue, busYellow;

        public void Init(CityTraffic cityTraffic)
        {
            traffic = cityTraffic;
            carColors = new[]
            {
                Make(0.85f, 0.15f, 0.15f), Make(0.15f, 0.35f, 0.85f), Make(0.92f, 0.92f, 0.92f),
                Make(0.12f, 0.12f, 0.14f), Make(0.95f, 0.78f, 0.15f), Make(0.20f, 0.65f, 0.35f), Make(0.55f, 0.58f, 0.62f)
            };
            cabColors = new[] { Make(0.85f, 0.45f, 0.10f), Make(0.20f, 0.45f, 0.70f), Make(0.75f, 0.12f, 0.12f) };
            white = Make(0.93f, 0.93f, 0.90f);
            trailerBlue = Make(0.22f, 0.36f, 0.62f);
            trailerGray = Make(0.62f, 0.64f, 0.66f);
            dark = Make(0.07f, 0.07f, 0.08f);
            glass = Make(0.35f, 0.50f, 0.62f);
            red = Make(0.85f, 0.10f, 0.10f);
            policeBlue = Make(0.12f, 0.20f, 0.55f);
            busYellow = Make(0.95f, 0.75f, 0.10f);

            for (int t = 0; t < TypeNames.Length; t++)
            {
                string name = TypeNames[t];
                GameObject prefab = Resources.Load<GameObject>("Vehicles/" + name);
                if (prefab != null) { prefabs[t] = prefab; }
            }

            traffic.VehicleAdded += OnAdded;
            traffic.VehicleRemoved += OnRemoved;
            foreach (Vehicle v in traffic.Vehicles) { OnAdded(v); }
        }

        private void OnDestroy()
        {
            if (traffic != null)
            {
                traffic.VehicleAdded -= OnAdded;
                traffic.VehicleRemoved -= OnRemoved;
            }
        }

        private static Material Make(float r, float g, float b)
        {
            Material m = MaterialFactory.CreateLitGray(1f);
            m.color = new Color(r, g, b, 1f);
            return m;
        }

        private void OnAdded(Vehicle v)
        {
            int type = (int)v.Type;
            Transform root;
            List<Transform> free = pool[type];
            if (free.Count > 0)
            {
                root = free[free.Count - 1];
                free.RemoveAt(free.Count - 1);
                root.gameObject.SetActive(true);
                Recolor(root, v);
            }
            else
            {
                root = BuildModel(v);
            }

            views[v.Id] = new View { Vehicle = v, Root = root };
        }

        private void OnRemoved(Vehicle v)
        {
            View view;
            if (!views.TryGetValue(v.Id, out view)) { return; }
            views.Remove(v.Id);
            view.Root.gameObject.SetActive(false);
            pool[(int)v.Type].Add(view.Root);
        }

        private void LateUpdate()
        {
            if (traffic == null) { return; }
            float blend = 1f - Mathf.Exp(-22f * Time.deltaTime);
            foreach (KeyValuePair<int, View> pair in views)
            {
                View view = pair.Value;
                Vehicle v = view.Vehicle;
                double half = v.Length * 0.5;
                Vec2 front = v.PositionAt(v.S, 0.0);
                Vec2 rear = v.PositionAt(v.S, v.Length);
                Vec2 mid = v.PositionAt(v.S, half);
                Vec2 dir = front - rear;
                if (dir.LengthSquared < 0.0001) { dir = v.Heading; }
                dir = dir.Normalized;

                Vector3 target = CityMetrics.ToWorld(mid, GroundHeight);
                Quaternion look = Quaternion.LookRotation(CityMetrics.ToWorld(dir, 0f), Vector3.up);
                Transform t = view.Root;
                if (!view.Placed || Vector3.Distance(t.position, target) > 12f)
                {
                    t.position = target;
                    t.rotation = look;
                    view.Placed = true;
                }
                else
                {
                    t.position = Vector3.Lerp(t.position, target, blend);
                    t.rotation = Quaternion.Slerp(t.rotation, look, blend);
                }
            }
        }

        // ------------------------------------------------------------ placeholder models

        private Transform BuildModel(Vehicle v)
        {
            var root = new GameObject(v.Type + " " + v.Id).transform;
            root.SetParent(transform, false);
            GameObject prefab;
            if (prefabs.TryGetValue((int)v.Type, out prefab))
            {
                Transform model = Object.Instantiate(prefab).transform;
                model.SetParent(root, false);
                return root;
            }

            if (v.Type == VehicleType.Car)
            {
                Part(root, "Body", 0f, 0.65f, 0f, 1.8f, 0.7f, 4.4f, carColors[0]);
                Part(root, "Cabin", 0f, 1.25f, -0.2f, 1.6f, 0.55f, 2.3f, glass);
                Wheels(root, 0.9f, 1.35f, 0.3f);
                Wheels(root, 0.9f, -1.35f, 0.3f);
            }
            else if (v.Type == VehicleType.ShortTruck)
            {
                Part(root, "Chassis", 0f, 0.65f, 0f, 2.3f, 0.3f, 7.4f, dark);
                Part(root, "Cab", 0f, 1.7f, 2.6f, 2.3f, 2.0f, 2.1f, cabColors[0]);
                Part(root, "Windshield", 0f, 2.1f, 3.66f, 2.0f, 0.8f, 0.05f, glass);
                Part(root, "Box", 0f, 2.15f, -1.15f, 2.4f, 2.7f, 5.0f, white);
                Wheels(root, 1.15f, 2.6f, 0.5f);
                Wheels(root, 1.15f, -1.6f, 0.5f);
                Wheels(root, 1.15f, -2.9f, 0.5f);
            }
            else if (v.Type == VehicleType.Ambulance)
            {
                Part(root, "Body", 0f, 1.35f, -0.2f, 2.3f, 1.9f, 5.4f, white);
                Part(root, "Stripe", 0f, 1.35f, -0.2f, 2.35f, 0.3f, 5.45f, red);
                Part(root, "Windshield", 0f, 1.75f, 2.5f, 2.0f, 0.7f, 0.05f, glass);
                Part(root, "Light", 0f, 2.35f, 2.0f, 1.2f, 0.2f, 0.3f, red);
                Wheels(root, 1.0f, 1.8f, 0.4f);
                Wheels(root, 1.0f, -1.9f, 0.4f);
            }
            else if (v.Type == VehicleType.PoliceCar)
            {
                Part(root, "Body", 0f, 0.65f, 0f, 1.8f, 0.7f, 4.6f, policeBlue);
                Part(root, "Cabin", 0f, 1.25f, -0.2f, 1.6f, 0.55f, 2.3f, white);
                Part(root, "Light", 0f, 1.6f, -0.2f, 1.0f, 0.15f, 0.3f, red);
                Wheels(root, 0.9f, 1.4f, 0.3f);
                Wheels(root, 0.9f, -1.4f, 0.3f);
            }
            else if (v.Type == VehicleType.FireTruck)
            {
                Part(root, "Chassis", 0f, 0.65f, 0f, 2.4f, 0.3f, 9.4f, dark);
                Part(root, "Cab", 0f, 1.8f, 3.2f, 2.4f, 2.1f, 2.4f, red);
                Part(root, "Windshield", 0f, 2.2f, 4.42f, 2.1f, 0.8f, 0.05f, glass);
                Part(root, "Body", 0f, 1.9f, -1.4f, 2.5f, 2.2f, 6.2f, red);
                Part(root, "Ladder", 0f, 3.15f, -1.4f, 0.6f, 0.2f, 6.0f, white);
                Wheels(root, 1.15f, 3.0f, 0.5f);
                Wheels(root, 1.15f, -2.2f, 0.5f);
                Wheels(root, 1.15f, -3.5f, 0.5f);
            }
            else if (v.Type == VehicleType.SchoolBus)
            {
                Part(root, "Body", 0f, 1.85f, 0f, 2.4f, 2.4f, 10.4f, busYellow);
                Part(root, "Windows", 0f, 2.3f, 0f, 2.45f, 0.7f, 9.0f, glass);
                Part(root, "Windshield", 0f, 2.2f, 5.22f, 2.1f, 1.0f, 0.05f, glass);
                Wheels(root, 1.15f, 3.4f, 0.55f);
                Wheels(root, 1.15f, -3.4f, 0.55f);
            }
            else
            {
                Part(root, "Chassis", 0f, 0.65f, 0f, 2.3f, 0.3f, 16.4f, dark);
                Part(root, "Cab", 0f, 1.9f, 6.5f, 2.4f, 2.4f, 2.6f, cabColors[1]);
                Part(root, "Windshield", 0f, 2.4f, 7.82f, 2.1f, 0.9f, 0.05f, glass);
                Part(root, "Trailer", 0f, 2.55f, -1.9f, 2.5f, 3.2f, 12.6f, trailerBlue);
                Wheels(root, 1.15f, 6.0f, 0.5f);
                Wheels(root, 1.15f, -5.0f, 0.5f);
                Wheels(root, 1.15f, -6.5f, 0.5f);
            }

            Recolor(root, v);
            return root;
        }

        private void Recolor(Transform root, Vehicle v)
        {
            if (v.Type == VehicleType.Car)
            {
                SetMaterial(root, "Body", carColors[v.Variant % carColors.Length]);
            }
            else if (v.Type == VehicleType.ShortTruck)
            {
                SetMaterial(root, "Cab", cabColors[v.Variant % cabColors.Length]);
                SetMaterial(root, "Box", v.Variant % 2 == 0 ? white : trailerGray);
            }
            else
            {
                SetMaterial(root, "Cab", cabColors[v.Variant % cabColors.Length]);
                SetMaterial(root, "Trailer", v.Variant % 3 == 0 ? white : v.Variant % 3 == 1 ? trailerBlue : trailerGray);
            }
        }

        private static void SetMaterial(Transform root, string part, Material material)
        {
            Transform child = root.Find(part);
            if (child != null)
            {
                child.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
        }

        private void Wheels(Transform root, float halfTrack, float z, float radius)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                Part(root, "Wheel", side * halfTrack, radius, z, 0.3f, radius * 2f, radius * 2f, dark);
            }
        }

        private void Part(Transform root, string name, float x, float y, float z, float sx, float sy, float sz, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(cube.GetComponent<Collider>());
            cube.name = name;
            cube.transform.SetParent(root, false);
            cube.transform.localPosition = new Vector3(x, y, z);
            cube.transform.localScale = new Vector3(sx, sy, sz);
            cube.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
