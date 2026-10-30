using UnityEngine;
using UrbanGenesis.Simulation;

namespace UrbanGenesis.Game
{
    /// <summary>
    /// How the simulation's flat ground coordinates map to the 3D world. The simulation measures in metres on a plane
    /// (X east, Y north) with the map centred on (0, 0). In Unity that is world X and world Z, with 1 unit = 1 metre
    /// (matching Blender's default). Height is world Y.
    /// </summary>
    public static class CityMetrics
    {
        /// <summary>
        /// Size of one lot cell in metres. Building designs are drawn in lot cells, so this scales them to metres.
        /// </summary>
        public const float CellSize = (float)RoadNetwork.LotSize;

        /// <summary>How tall one floor of a building is, in world units.</summary>
        public const float FloorHeight = 3.5f;

        public static Vector3 ToWorld(Vec2 p, float height)
        {
            return new Vector3((float)p.X, height, (float)p.Y);
        }

        public static Vec2 ToGround(Vector3 world)
        {
            return new Vec2(world.x, world.z);
        }
    }
}
