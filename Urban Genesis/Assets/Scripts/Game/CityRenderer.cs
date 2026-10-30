using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Game
{
    /// <summary>
    /// Everything you see of the city. It listens to the simulation and draws:
    ///  * the ground (one quad with a texture where every cell is a block of pixels: grass, roads, zones, grid lines),
    ///  * one gray cube per building,
    ///  * a translucent overlay used for tool previews and hover highlights.
    /// Nothing here changes the simulation.
    /// </summary>
    public sealed class CityRenderer : MonoBehaviour
    {
        private const int PixelsPerCell = 8;
        private const float OverlayHeight = 0.15f;

        private CitySimulation sim;

        // Ground
        private Texture2D groundTexture;
        private Material groundMaterial;
        private readonly Color32[] cellPixels = new Color32[PixelsPerCell * PixelsPerCell];
        private readonly HashSet<GridCoord> dirtyCells = new HashSet<GridCoord>();

        // Overlay
        private Mesh overlayMesh;
        private Material overlayMaterial;
        private readonly List<Vector3> overlayVertices = new List<Vector3>();
        private readonly List<Color> overlayColors = new List<Color>();
        private readonly List<int> overlayTriangles = new List<int>();

        // Buildings
        private Transform buildingRoot;
        private readonly Dictionary<int, GameObject> buildingObjects = new Dictionary<int, GameObject>();
        private readonly BuildingModelFactory modelFactory = new BuildingModelFactory();

        public void Init(CitySimulation simulation)
        {
            sim = simulation;

            BuildGround();
            BuildOverlay();

            buildingRoot = new GameObject("Buildings").transform;
            buildingRoot.SetParent(transform, false);

            sim.CellChanged += OnCellChanged;
            sim.BuildingSpawned += OnBuildingSpawned;
            sim.BuildingRemoved += OnBuildingRemoved;
        }

        private void OnDestroy()
        {
            if (sim != null)
            {
                sim.CellChanged -= OnCellChanged;
                sim.BuildingSpawned -= OnBuildingSpawned;
                sim.BuildingRemoved -= OnBuildingRemoved;
            }
        }

        private void LateUpdate()
        {
            if (dirtyCells.Count == 0)
            {
                return;
            }

            foreach (GridCoord c in dirtyCells)
            {
                if (sim.Grid.InBounds(c))
                {
                    DrawCell(c);
                }
            }

            dirtyCells.Clear();
            groundTexture.Apply(true, false);
        }

        // ------------------------------------------------------------ ground

        private void BuildGround()
        {
            int width = sim.Grid.Width;
            int height = sim.Grid.Height;

            groundTexture = new Texture2D(width * PixelsPerCell, height * PixelsPerCell, TextureFormat.RGBA32, true);
            groundTexture.name = "City Ground Texture";
            groundTexture.filterMode = FilterMode.Trilinear;
            groundTexture.anisoLevel = 4;
            groundTexture.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    DrawCell(new GridCoord(x, y));
                }
            }

            groundTexture.Apply(true, false);

            float worldWidth = width * CityMetrics.CellSize;
            float worldDepth = height * CityMetrics.CellSize;

            var mesh = new Mesh();
            mesh.name = "City Ground";
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, worldDepth),
                new Vector3(worldWidth, 0f, worldDepth),
                new Vector3(worldWidth, 0f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f)
            };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();

            groundMaterial = MaterialFactory.CreateUnlitTextured(groundTexture);

            var ground = new GameObject("Ground");
            ground.transform.SetParent(transform, false);
            ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer groundRenderer = ground.AddComponent<MeshRenderer>();
            groundRenderer.sharedMaterial = groundMaterial;
            groundRenderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private void OnCellChanged(GridCoord c)
        {
            // Roads draw their edges depending on their neighbours, so neighbours must redraw as well.
            dirtyCells.Add(c);
            dirtyCells.Add(c.Offset(1, 0));
            dirtyCells.Add(c.Offset(-1, 0));
            dirtyCells.Add(c.Offset(0, 1));
            dirtyCells.Add(c.Offset(0, -1));
        }

        private void DrawCell(GridCoord c)
        {
            bool isRoad = sim.IsRoad(c);
            ZoneType zone = sim.Grid.GetZone(c);

            Color32 fill;
            Color32 line;
            if (isRoad)
            {
                fill = CityPalette.Road;
                line = CityPalette.RoadEdge;
            }
            else
            {
                fill = CityPalette.ZoneFill(zone);
                line = CityPalette.ZoneLine(zone);
            }

            // Road cells outline only the sides that do not continue into another road cell.
            // Other cells draw a thin grid line along their left and bottom sides, plus the grid's outer edge.
            bool left, right, bottom, top;
            if (isRoad)
            {
                left = !sim.IsRoad(c.Offset(-1, 0));
                right = !sim.IsRoad(c.Offset(1, 0));
                bottom = !sim.IsRoad(c.Offset(0, -1));
                top = !sim.IsRoad(c.Offset(0, 1));
            }
            else
            {
                left = true;
                bottom = true;
                right = c.X == sim.Grid.Width - 1;
                top = c.Y == sim.Grid.Height - 1;
            }

            int last = PixelsPerCell - 1;
            for (int py = 0; py < PixelsPerCell; py++)
            {
                for (int px = 0; px < PixelsPerCell; px++)
                {
                    bool edge = (left && px == 0) || (right && px == last) || (bottom && py == 0) || (top && py == last);
                    cellPixels[py * PixelsPerCell + px] = edge ? line : fill;
                }
            }

            groundTexture.SetPixels32(c.X * PixelsPerCell, c.Y * PixelsPerCell, PixelsPerCell, PixelsPerCell, cellPixels);
        }

        // ------------------------------------------------------------ overlay

        private void BuildOverlay()
        {
            overlayMesh = new Mesh();
            overlayMesh.name = "Tool Overlay";
            overlayMesh.indexFormat = IndexFormat.UInt32;
            overlayMesh.MarkDynamic();

            overlayMaterial = MaterialFactory.CreateVertexColorOverlay();

            var overlay = new GameObject("Tool Overlay");
            overlay.transform.SetParent(transform, false);
            overlay.AddComponent<MeshFilter>().sharedMesh = overlayMesh;
            MeshRenderer overlayRenderer = overlay.AddComponent<MeshRenderer>();
            overlayRenderer.sharedMaterial = overlayMaterial;
            overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
            overlayRenderer.receiveShadows = false;
        }

        /// <summary>Shows a translucent coloured square over each cell. cells and colors must have the same length.</summary>
        public void SetOverlay(IList<GridCoord> cells, IList<Color> colors)
        {
            overlayVertices.Clear();
            overlayColors.Clear();
            overlayTriangles.Clear();

            float size = CityMetrics.CellSize;
            for (int i = 0; i < cells.Count; i++)
            {
                Vector3 corner = CityMetrics.CellCorner(cells[i]);
                float y = OverlayHeight;
                int start = overlayVertices.Count;

                overlayVertices.Add(new Vector3(corner.x, y, corner.z));
                overlayVertices.Add(new Vector3(corner.x, y, corner.z + size));
                overlayVertices.Add(new Vector3(corner.x + size, y, corner.z + size));
                overlayVertices.Add(new Vector3(corner.x + size, y, corner.z));

                for (int v = 0; v < 4; v++)
                {
                    overlayColors.Add(colors[i]);
                }

                overlayTriangles.Add(start);
                overlayTriangles.Add(start + 1);
                overlayTriangles.Add(start + 2);
                overlayTriangles.Add(start);
                overlayTriangles.Add(start + 2);
                overlayTriangles.Add(start + 3);
            }

            overlayMesh.Clear();
            overlayMesh.SetVertices(overlayVertices);
            overlayMesh.SetColors(overlayColors);
            overlayMesh.SetTriangles(overlayTriangles, 0);
            overlayMesh.RecalculateBounds();
        }

        public void ClearOverlay()
        {
            overlayMesh.Clear();
        }

        // ------------------------------------------------------------ buildings

        private void OnBuildingSpawned(Building building)
        {
            GameObject model = modelFactory.Create(building, buildingRoot);
            model.AddComponent<BuildingPopIn>().Begin();
            buildingObjects[building.Id] = model;
        }

        private void OnBuildingRemoved(Building building)
        {
            GameObject model;
            if (buildingObjects.TryGetValue(building.Id, out model))
            {
                buildingObjects.Remove(building.Id);
                Destroy(model);
            }
        }
    }
}
