using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Game
{
    /// <summary>
    /// Everything you see of the city. It reads the simulation and draws:
    ///  * the ground (one big quad with a faint tiled texture so you can judge distances),
    ///  * the roads (asphalt, edge lines and a dashed centre line, following every curve),
    ///  * the lots: zoned ones tinted by zone, and all of them faintly while a zoning or bulldoze tool is active,
    ///  * one gray model per building,
    ///  * a translucent overlay used for tool previews and hover highlights.
    /// Nothing here changes the simulation.
    /// </summary>
    public sealed class CityRenderer : MonoBehaviour
    {
        private const float RoadHeight = 0.06f;
        private const float RoadLineHeight = 0.09f;
        private const float LotHeight = 0.14f;
        private const float PreviewHeight = 0.3f;
        private const float GroundTileMetres = 64f;

        private CitySimulation sim;

        // Roads
        private Mesh roadMesh;
        private bool roadsDirty = true;

        // Lots
        private Mesh lotMesh;
        private bool showLotGrid;
        private bool lotGridChanged = true;
        private int lotsDrawnForRevision = -1;

        // Preview overlay
        private Mesh previewMesh;
        private readonly MeshBuilder preview = new MeshBuilder();

        // Buildings
        private Transform buildingRoot;
        private readonly Dictionary<int, GameObject> buildingObjects = new Dictionary<int, GameObject>();
        private readonly BuildingModelFactory modelFactory = new BuildingModelFactory();

        private readonly MeshBuilder builder = new MeshBuilder();

        public void Init(CitySimulation simulation)
        {
            sim = simulation;

            BuildGround();
            roadMesh = CreateOverlayObject("Roads", 2100);
            lotMesh = CreateOverlayObject("Lots", 2200);
            previewMesh = CreateOverlayObject("Tool Preview", 3000);

            buildingRoot = new GameObject("Buildings").transform;
            buildingRoot.SetParent(transform, false);

            sim.Roads.SegmentAdded += OnRoadsChanged;
            sim.Roads.SegmentRemoved += OnRoadsChanged;
            sim.Roads.SegmentSplit += OnRoadSplit;
            sim.BuildingSpawned += OnBuildingSpawned;
            sim.BuildingRemoved += OnBuildingRemoved;
        }

        private void OnDestroy()
        {
            if (sim != null)
            {
                sim.Roads.SegmentAdded -= OnRoadsChanged;
                sim.Roads.SegmentRemoved -= OnRoadsChanged;
                sim.Roads.SegmentSplit -= OnRoadSplit;
                sim.BuildingSpawned -= OnBuildingSpawned;
                sim.BuildingRemoved -= OnBuildingRemoved;
            }
        }

        private void LateUpdate()
        {
            if (roadsDirty)
            {
                RebuildRoads();
                roadsDirty = false;
            }

            if (lotGridChanged || lotsDrawnForRevision != sim.Revision)
            {
                RebuildLots();
                lotGridChanged = false;
                lotsDrawnForRevision = sim.Revision;
            }
        }

        private void OnRoadsChanged(RoadSegment segment) { roadsDirty = true; }
        private void OnRoadSplit(RoadSegment old, RoadSegment first, RoadSegment second, double length) { roadsDirty = true; }

        // ------------------------------------------------------------ ground

        private void BuildGround()
        {
            const int size = 64;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool line = x == 0 || y == 0;
                    pixels[y * size + x] = line ? CityPalette.GroundLine : CityPalette.Ground;
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
            texture.name = "City Ground Texture";
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = 4;
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.SetPixels32(0, 0, size, size, pixels);
            texture.Apply(true, false);

            float half = (float)(sim.Roads.WorldBounds.MaxX);
            float uv = half * 2f / GroundTileMetres;

            var mesh = new Mesh();
            mesh.name = "City Ground";
            mesh.vertices = new[]
            {
                new Vector3(-half, 0f, -half),
                new Vector3(-half, 0f, half),
                new Vector3(half, 0f, half),
                new Vector3(half, 0f, -half)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, uv),
                new Vector2(uv, uv),
                new Vector2(uv, 0f)
            };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();

            var ground = new GameObject("Ground");
            ground.transform.SetParent(transform, false);
            ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer groundRenderer = ground.AddComponent<MeshRenderer>();
            groundRenderer.sharedMaterial = MaterialFactory.CreateUnlitTextured(texture);
            groundRenderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private Mesh CreateOverlayObject(string objectName, int renderQueue)
        {
            var mesh = new Mesh();
            mesh.name = objectName;
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.MarkDynamic();

            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = MaterialFactory.CreateVertexColorOverlay(renderQueue);
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            return mesh;
        }

        // ------------------------------------------------------------ roads

        private void RebuildRoads()
        {
            builder.Clear();

            foreach (RoadSegment segment in sim.Roads.Segments)
            {
                foreach (Vec2[] quad in segment.Surface)
                {
                    builder.Quad(quad[0], quad[1], quad[2], quad[3], RoadHeight, CityPalette.Road);
                }
            }

            // Round off the corners where roads meet.
            foreach (RoadNode node in sim.Roads.Nodes)
            {
                if (node.Degree >= 2)
                {
                    builder.Disc(node.Position, RoadNetwork.HalfWidth, 20, RoadHeight, CityPalette.Road);
                }
            }

            // Lines go on top of the asphalt (later triangles are drawn over earlier ones).
            foreach (RoadSegment segment in sim.Roads.Segments)
            {
                AddRoadLines(segment);
            }

            builder.Apply(roadMesh);
        }

        private void AddRoadLines(RoadSegment segment)
        {
            int pieces = segment.Surface.Count;
            double length = segment.Length;
            double half = RoadNetwork.HalfWidth;
            const double edgeWidth = 0.35;
            const double dashHalf = 0.18;

            for (int i = 0; i < pieces; i++)
            {
                double s0 = length * i / pieces;
                double s1 = length * (i + 1) / pieces;
                double mid = (s0 + s1) * 0.5;

                // Leave junctions clear of lines.
                if ((segment.A.Degree >= 3 && mid < half + 0.5) || (segment.B.Degree >= 3 && length - mid < half + 0.5))
                {
                    continue;
                }

                RoadCurve c = segment.Curve;
                foreach (double sign in new[] { 1.0, -1.0 })
                {
                    builder.Quad(
                        c.OffsetPointAtLength(s0, sign * (half - 0.2)),
                        c.OffsetPointAtLength(s1, sign * (half - 0.2)),
                        c.OffsetPointAtLength(s1, sign * (half - 0.2 - edgeWidth)),
                        c.OffsetPointAtLength(s0, sign * (half - 0.2 - edgeWidth)),
                        RoadLineHeight, CityPalette.RoadEdge);
                }

                if (i % 2 == 0)
                {
                    builder.Quad(
                        c.OffsetPointAtLength(s0, -dashHalf),
                        c.OffsetPointAtLength(s1, -dashHalf),
                        c.OffsetPointAtLength(s1, dashHalf),
                        c.OffsetPointAtLength(s0, dashHalf),
                        RoadLineHeight, CityPalette.RoadCentre);
                }
            }
        }

        // ------------------------------------------------------------ lots

        /// <summary>Shows every lot faintly (used while a zoning or bulldoze tool is active), or only the zoned ones.</summary>
        public void SetLotGridVisible(bool visible)
        {
            if (showLotGrid != visible)
            {
                showLotGrid = visible;
                lotGridChanged = true;
            }
        }

        private void RebuildLots()
        {
            builder.Clear();

            foreach (Lot lot in sim.Lots.Lots)
            {
                Color color;
                if (lot.Zone != ZoneType.None)
                {
                    color = CityPalette.ZoneTint(lot.Zone, showLotGrid ? 0.55f : 0.38f);
                }
                else if (showLotGrid)
                {
                    color = CityPalette.LotOutline;
                }
                else
                {
                    continue;
                }

                builder.InsetQuad(lot.Corners, 0.94, LotHeight, color);
            }

            builder.Apply(lotMesh);
        }

        // ------------------------------------------------------------ preview overlay

        /// <summary>Starts a new preview. Add shapes, then call <see cref="EndPreview"/>.</summary>
        public void BeginPreview()
        {
            preview.Clear();
        }

        public void PreviewQuad(Vec2[] corners, Color color)
        {
            preview.InsetQuad(corners, 1.0, PreviewHeight, color);
        }

        /// <summary>A ribbon of the given half-width along a line of points (for roads).</summary>
        public void PreviewRibbon(IList<Vec2> centreLine, double halfWidth, Color color)
        {
            preview.Ribbon(centreLine, halfWidth, PreviewHeight, color);
        }

        public void PreviewDisc(Vec2 center, double radius, Color color)
        {
            preview.Disc(center, radius, 28, PreviewHeight, color);
        }

        public void PreviewRing(Vec2 center, double radius, double thickness, Color color)
        {
            preview.Ring(center, radius, thickness, 48, PreviewHeight + 0.02f, color);
        }

        public void EndPreview()
        {
            preview.Apply(previewMesh);
        }

        public void ClearPreview()
        {
            previewMesh.Clear();
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

        // ------------------------------------------------------------ mesh building helper

        /// <summary>Collects flat, vertex-coloured shapes lying on the ground and writes them into a mesh.</summary>
        private sealed class MeshBuilder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Color> colors = new List<Color>();
            private readonly List<int> triangles = new List<int>();

            public void Clear()
            {
                vertices.Clear();
                colors.Clear();
                triangles.Clear();
            }

            public void Apply(Mesh mesh)
            {
                mesh.Clear();
                if (vertices.Count == 0)
                {
                    return;
                }

                mesh.SetVertices(vertices);
                mesh.SetColors(colors);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
            }

            public void Quad(Vec2 a, Vec2 b, Vec2 c, Vec2 d, float height, Color color)
            {
                int start = vertices.Count;
                vertices.Add(CityMetrics.ToWorld(a, height));
                vertices.Add(CityMetrics.ToWorld(b, height));
                vertices.Add(CityMetrics.ToWorld(c, height));
                vertices.Add(CityMetrics.ToWorld(d, height));
                for (int i = 0; i < 4; i++)
                {
                    colors.Add(color);
                }

                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
            }

            /// <summary>A quad shrunk towards its centre by the given factor (1 = no shrink).</summary>
            public void InsetQuad(Vec2[] corners, double factor, float height, Color color)
            {
                Vec2 centre = (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25;
                Quad(
                    centre + (corners[0] - centre) * factor,
                    centre + (corners[1] - centre) * factor,
                    centre + (corners[2] - centre) * factor,
                    centre + (corners[3] - centre) * factor,
                    height, color);
            }

            public void Quad(Vec2 a, Vec2 b, Vec2 c, Vec2 d, float height, Color32 color)
            {
                Quad(a, b, c, d, height, (Color)color);
            }

            public void Disc(Vec2 centre, double radius, int sides, float height, Color color)
            {
                int start = vertices.Count;
                vertices.Add(CityMetrics.ToWorld(centre, height));
                colors.Add(color);
                for (int i = 0; i < sides; i++)
                {
                    double angle = Math.PI * 2.0 * i / sides;
                    vertices.Add(CityMetrics.ToWorld(centre + new Vec2(Math.Cos(angle), Math.Sin(angle)) * radius, height));
                    colors.Add(color);
                }

                for (int i = 0; i < sides; i++)
                {
                    triangles.Add(start);
                    triangles.Add(start + 1 + i);
                    triangles.Add(start + 1 + (i + 1) % sides);
                }
            }

            public void Disc(Vec2 centre, double radius, int sides, float height, Color32 color)
            {
                Disc(centre, radius, sides, height, (Color)color);
            }

            public void Ring(Vec2 centre, double radius, double thickness, int sides, float height, Color color)
            {
                for (int i = 0; i < sides; i++)
                {
                    double a0 = Math.PI * 2.0 * i / sides;
                    double a1 = Math.PI * 2.0 * (i + 1) / sides;
                    Vec2 d0 = new Vec2(Math.Cos(a0), Math.Sin(a0));
                    Vec2 d1 = new Vec2(Math.Cos(a1), Math.Sin(a1));
                    Quad(
                        centre + d0 * (radius - thickness),
                        centre + d0 * radius,
                        centre + d1 * radius,
                        centre + d1 * (radius - thickness),
                        height, color);
                }
            }

            public void Ribbon(IList<Vec2> line, double halfWidth, float height, Color color)
            {
                for (int i = 0; i + 1 < line.Count; i++)
                {
                    Vec2 a = line[i];
                    Vec2 b = line[i + 1];
                    Vec2 before = i > 0 ? (b - line[i - 1]) : (b - a);
                    Vec2 after = i + 2 < line.Count ? (line[i + 2] - a) : (b - a);
                    Vec2 na = before.Normalized.PerpLeft * halfWidth;
                    Vec2 nb = after.Normalized.PerpLeft * halfWidth;
                    Quad(a - na, b - nb, b + nb, a + na, height, color);
                }
            }
        }
    }
}
