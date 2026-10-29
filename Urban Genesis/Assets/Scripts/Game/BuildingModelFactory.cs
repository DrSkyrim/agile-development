using System.Collections.Generic;
using UnityEngine;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Game
{
    /// <summary>
    /// Builds the 3D model of a building out of the parts <see cref="BuildingDesigner"/> describes:
    /// cubes, cylinders, pyramids and gable roofs, all in shades of gray for now. When real Blender models
    /// arrive, this is the one place to swap them in, one prefab per <see cref="BuildingShape"/>.
    /// </summary>
    public sealed class BuildingModelFactory
    {
        private const int ShadesPerZone = 5;

        private readonly Dictionary<int, Material> materials = new Dictionary<int, Material>();

        /// <summary>
        /// Creates the building as a child of <paramref name="parent"/>. The returned object stands in the middle
        /// of the building's footprint, turned to face the way the building faces, so its parts can be placed
        /// in the building's own frame. Scaling it vertically makes the whole building grow from the ground.
        /// </summary>
        public GameObject Create(Building building, Transform parent)
        {
            var root = new GameObject(building.Shape + " " + building.Id);
            root.transform.SetParent(parent, false);

            // Rotation 0 faces lower Y (towards -Z in the world); each quarter turn is 90 degrees about the up axis.
            root.transform.position = new Vector3(
                (building.Cell.X + building.Width * 0.5f) * CityMetrics.CellSize,
                0f,
                (building.Cell.Y + building.Depth * 0.5f) * CityMetrics.CellSize);
            root.transform.rotation = Quaternion.Euler(0f, 90f * building.Rotation, 0f);

            foreach (BuildingPart part in BuildingDesigner.Design(building))
            {
                AddPart(root.transform, building, part);
            }

            return root;
        }

        private void AddPart(Transform root, Building building, BuildingPart part)
        {
            float sizeX = part.SizeX * CityMetrics.CellSize;
            float sizeZ = part.SizeY * CityMetrics.CellSize;
            float height = part.Height * CityMetrics.FloorHeight;
            float bottom = part.Elevation * CityMetrics.FloorHeight;
            float x = part.CenterX * CityMetrics.CellSize;
            float z = part.CenterY * CityMetrics.CellSize;

            GameObject go;
            Vector3 position;
            Vector3 scale;

            switch (part.Kind)
            {
                case PartKind.Cylinder:
                    go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    position = new Vector3(x, bottom + height * 0.5f, z);
                    scale = new Vector3(sizeX, height * 0.5f, sizeX); // Unity's cylinder is 2 units tall
                    break;

                case PartKind.Pyramid:
                    go = CreateMeshObject(ProceduralMeshes.Pyramid());
                    position = new Vector3(x, bottom, z);
                    scale = new Vector3(sizeX, height, sizeZ);
                    break;

                case PartKind.GableRoof:
                    go = CreateMeshObject(ProceduralMeshes.GableRoof(part.RidgeAlongX));
                    position = new Vector3(x, bottom, z);
                    scale = new Vector3(sizeX, height, sizeZ);
                    break;

                default:
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    position = new Vector3(x, bottom + height * 0.5f, z);
                    scale = new Vector3(sizeX, height, sizeZ);
                    break;
            }

            go.name = part.Kind.ToString();
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;

            // Picking works with maths against the ground plane, so parts do not need colliders.
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.Destroy(collider);
            }

            go.GetComponent<MeshRenderer>().sharedMaterial = GetMaterial(building, part.Tone);
        }

        private static GameObject CreateMeshObject(Mesh mesh)
        {
            var go = new GameObject();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>();
            return go;
        }

        // A few shades per zone and tone, shared between buildings so Unity can batch them.
        private Material GetMaterial(Building building, PartTone tone)
        {
            int shade = building.Id % ShadesPerZone;
            int key = ((int)building.Zone * ShadesPerZone + shade) * 10 + (int)tone;

            Material material;
            if (!materials.TryGetValue(key, out material))
            {
                material = MaterialFactory.CreateLitGray(CityPalette.PartBrightness(building.Zone, shade, tone));
                material.name = "Building " + building.Zone + " " + shade + " " + tone;
                materials[key] = material;
            }

            return material;
        }
    }
}
