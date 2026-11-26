using System;
using System.Collections.Generic;

namespace UrbanGenesis.Simulation
{
    /// <summary>
    /// Everything needed to rebuild a city, as plain data (public fields, lists and numbers only) so the Unity layer can
    /// write it as JSON. <see cref="CitySimulation.Capture"/> makes one and <see cref="CitySimulation.Restore"/> turns it back into a city.
    /// Lots are named by where their centre is, not by their keys, so a save does not depend on how lot keys are numbered.
    /// </summary>
    [Serializable]
    public sealed class CitySnapshot
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public double WorldSize;
        public int Seed;
        public int Tick;

        public double SpawnChance;
        public int GrowthAttempts;
        public double BigBuildingPriority;
        public bool DemandDrivesGrowth;

        public bool MoneyEnabled;
        public double Balance;
        public int TicksIntoDay;

        public int MilestonesReached;
        public bool RequireUtilities;

        /// <summary>The river's middle line as x, y, x, y, ... (empty if the map has none).</summary>
        public List<double> RiverPoints = new List<double>();
        public double RiverHalfWidth = 20.0;

        /// <summary>Roads that are closed for roadworks: segment id and the tick they reopen.</summary>
        public List<int> WorkSegments = new List<int>();
        public List<int> WorkReopenTicks = new List<int>();

        /// <summary>Water pipes as ax, ay, bx, by, ax, ay, ...</summary>
        public List<double> Pipes = new List<double>();

        public int NextNodeId;
        public int NextSegmentId;
        public int NextBuildingId;

        public List<NodeData> Nodes = new List<NodeData>();
        public List<SegmentData> Segments = new List<SegmentData>();
        public List<ZoneData> Zones = new List<ZoneData>();
        public List<BuildingData> Buildings = new List<BuildingData>();

        [Serializable]
        public sealed class NodeData
        {
            public int Id;
            public double X;
            public double Y;
            public bool LightsOff;
            public List<TurnData> BlockedTurns = new List<TurnData>();
        }

        [Serializable]
        public sealed class TurnData
        {
            public int From;
            public int Lane;
            public int To;

            /// <summary>1 + the lane entered (0 = every lane, as older saves recorded it).</summary>
            public int ToLaneNumber;
        }

        [Serializable]
        public sealed class SegmentData
        {
            public int Id;
            public int A;
            public int B;
            public double P0X, P0Y, P1X, P1Y, P2X, P2Y;
            public int Type;
            public float Open = 1f;
            public bool Outside;
            public bool Power;
        }

        [Serializable]
        public sealed class ZoneData
        {
            public double X;
            public double Y;
            public int Zone;
        }

        [Serializable]
        public sealed class BuildingData
        {
            public int Id;
            public int Shape;
            public int Width;
            public int Depth;
            public int Floors;
            public int Variant;
            public double CX, CY, BX, BY;

            /// <summary>Centres of the lots it stands on, as x, y, x, y, ...</summary>
            public List<double> LotCentres = new List<double>();
        }
    }
}
