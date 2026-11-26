using System.Text;
using UrbanGenesis.Simulation.Traffic;

namespace UrbanGenesis.Simulation
{
    /// <summary>Plain-language facts about a building or a road, for the Inspect tool. No game or UI code in here.</summary>
    public static class Inspector
    {
        /// <summary>Road space one vehicle takes up in a queue, in metres (car, gap and a share of the trucks).</summary>
        public const double MetresPerVehicle = 8.0;

        public static string Title(Building b)
        {
            string kind = b.IsCivic ? CivicInfo.Name(b.Shape)
                : b.Zone == ZoneType.DenseResidential ? "Dense residential building"
                : b.Zone == ZoneType.Residential ? "Residential building"
                : b.Zone == ZoneType.Commercial ? "Commercial building" : "Industrial building";
            return kind + " #" + b.Id;
        }

        public static string DescribeBuilding(CitySimulation sim, Building b, CityTraffic traffic = null)
        {
            var text = new StringBuilder();
            BuildingEconomy e = sim.Economy.StatusOf(b);
            CityTreasury t = sim.Treasury;
            text.Append(Title(b)).Append('\n');
            if (b.IsCivic)
            {
                text.Append(CivicInfo.Description(b.Shape)).Append('\n');
                RoadSegment host = b.Lots.Count > 0 ? sim.Roads.GetSegment(b.Lots[0].Key.SegmentId) : null;
                if (b.Shape == BuildingShape.PowerPlant)
                {
                    text.Append(host != null && host.HasPower ? "Connected to the power cable on its road.\n" : "Not connected: lay a power cable along the road it stands on.\n");
                }
                else if (b.Shape == BuildingShape.WaterTower || b.Shape == BuildingShape.SewageOutlet)
                {
                    text.Append(sim.IsConnectedToPipes(b) ? "Connected to the pipe network.\n" : "Not connected: run a water pipe up to it.\n");
                }

                text.Append("Cost to build: ").Append(CitySimulation.FormatMoney(CivicInfo.Cost(b.Shape))).Append(", demolishing gives nothing back.");
                return text.ToString();
            }

            string missing = sim.MissingServices(b);
            if (missing.Length > 0)
            {
                text.Append("NOT SERVED: no ").Append(missing).Append(". It sits idle and earns nothing.\n");
                text.Append("Lay a power cable along its road (connected to a power plant) and run a water pipe across or next to the road (connected to a water tower and a sewage outlet).");
                return text.ToString();
            }

            text.Append("Floors: ").Append(b.Floors).Append(", footprint ").Append(b.Width).Append("x").Append(b.Depth).Append(" lots\n");
            if (e == null)
            {
                text.Append("Just built: the economy has not counted it yet.");
                return text.ToString();
            }

            double income;
            if (b.IsResidential)
            {
                double employed = e.Workers * e.EmploymentFill;
                text.Append("Residents: ").Append(Round(e.Residents)).Append(" (capacity ").Append(Round(e.Residents)).Append(", full)\n");
                text.Append("Working age: ").Append(Round(e.Workers)).Append(", with a job: ").Append(Round(employed))
                    .Append(", without: ").Append(Round(e.Workers - employed)).Append('\n');
                text.Append("Shopping: ").Append(Percent(e.ShoppingFill)).Append(" of trips find a shop\n");
                income = employed * t.TaxPerEmployedWorkerPerWeek;
            }
            else
            {
                double filled = e.Jobs * e.Staffing;
                text.Append("Jobs: ").Append(Round(e.Jobs)).Append(", filled: ").Append(Round(filled))
                    .Append(", free positions: ").Append(Round(e.Jobs - filled)).Append('\n');
                if (b.Zone == ZoneType.Commercial)
                {
                    text.Append("Serves up to ").Append(Round(e.EffectiveService)).Append(" shoppers (goods supplied: ").Append(Percent(e.GoodsFill)).Append(")\n");
                }
                else
                {
                    text.Append("Produces ").Append(Round(e.EffectiveGoodsOut)).Append(" goods (staffing: ").Append(Percent(e.Staffing)).Append(")\n");
                }

                income = filled * t.TaxPerEmployedWorkerPerWeek + (b.Zone == ZoneType.Commercial ? e.EffectiveService * t.TaxPerShopperServedPerWeek : 0.0);
            }

            RoadSegment front = sim.Roads.GetSegment(e.SegmentId);
            if (front != null && traffic != null)
            {
                double fill = VehiclesOn(traffic, front) / Capacity(front);
                text.Append("Road in front: ").Append(RoadSpecs.Name(front.Type).ToLowerInvariant()).Append(", ").Append(Percent(fill)).Append(" full")
                    .Append(fill >= 0.7 ? " - jammed, more buildings here will make it worse\n" : fill >= 0.35 ? " - getting busy\n" : " - plenty of room\n");
            }

            text.Append("Tax income: about ").Append(CitySimulation.FormatMoney(income)).Append(" per week\n");
            text.Append("Upkeep: none, buildings are free; only roads cost upkeep.");
            return text.ToString();
        }

        public static string Title(RoadSegment s)
        {
            return RoadSpecs.Name(s.Type) + " #" + s.Id;
        }

        public static double Capacity(RoadSegment s)
        {
            int lanes = RoadSpecs.LanesPerDirection(s.Type) * (RoadSpecs.IsOneWay(s.Type) ? 1 : 2);
            return System.Math.Max(1.0, System.Math.Floor(s.Length / MetresPerVehicle)) * lanes;
        }

        public static int VehiclesOn(CityTraffic traffic, RoadSegment s)
        {
            int count = 0;
            if (traffic == null) { return 0; }
            for (int i = 0; i < traffic.Vehicles.Count; i++)
            {
                Vehicle v = traffic.Vehicles[i];
                if (v.Route != null && v.StepIndex < v.Route.Count && v.Step.Segment == s) { count++; }
            }

            return count;
        }

        /// <summary>Vehicles on every segment, counted in one pass (segment id to count).</summary>
        public static System.Collections.Generic.Dictionary<int, int> LoadBySegment(CityTraffic traffic)
        {
            var load = new System.Collections.Generic.Dictionary<int, int>();
            if (traffic == null) { return load; }
            for (int i = 0; i < traffic.Vehicles.Count; i++)
            {
                Vehicle v = traffic.Vehicles[i];
                if (v.Route == null || v.StepIndex >= v.Route.Count) { continue; }
                int id = v.Step.Segment.Id;
                int n;
                load.TryGetValue(id, out n);
                load[id] = n + 1;
            }

            return load;
        }

        public static string DescribeRoad(CitySimulation sim, CityTraffic traffic, RoadSegment s)
        {
            var text = new StringBuilder();
            CityTreasury t = sim.Treasury;
            double capacity = Capacity(s);
            int load = VehiclesOn(traffic, s);
            text.Append(Title(s)).Append('\n');
            text.Append(RoadSpecs.LaneLabel(s.Type)).Append(", ").Append(Round(RoadSpecs.Width(s.Type))).Append(" m wide, ").Append(Round(s.Length)).Append(" m long\n");
            text.Append("Capacity: about ").Append(Round(capacity)).Append(" vehicles at once, now ").Append(load)
                .Append(" (").Append(Percent(load / capacity)).Append(" full)\n");
            text.Append("Built for ").Append(CitySimulation.FormatMoney(t.RoadCost(s.Length, s.Type))).Append(", demolishing refunds ")
                .Append(CitySimulation.FormatMoney(t.RoadCost(s.Length, s.Type) * t.RefundShare)).Append('\n');
            text.Append("Upkeep: ").Append(CitySimulation.FormatMoney(t.UpkeepPerMetreOf(s.Type) * s.Length)).Append(" per week");
            return text.ToString();
        }

        private static string Round(double v) { return ((int)System.Math.Round(v)).ToString(); }
        private static string Percent(double v) { return ((int)System.Math.Round(System.Math.Min(1.0, System.Math.Max(0.0, v)) * 100.0)) + "%"; }
    }
}
