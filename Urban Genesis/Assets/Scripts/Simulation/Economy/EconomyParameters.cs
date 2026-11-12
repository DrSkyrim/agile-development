using System;
using System.Collections.Generic;

namespace UrbanGenesis.Simulation
{
    /// <summary>What a kind of building gives to and needs from the city, per "unit" (one lot of footprint times one floor).</summary>
    public struct EconomyProfile
    {
        /// <summary>Workers the building employs.</summary>
        public double Jobs;

        /// <summary>Shoppers the building can serve (shops and malls).</summary>
        public double Service;

        /// <summary>Goods the building needs delivered to sell or use (shops and malls).</summary>
        public double GoodsIn;

        /// <summary>Goods the building produces (factories, warehouses, silo yards).</summary>
        public double GoodsOut;

        public EconomyProfile(double jobs, double service, double goodsIn, double goodsOut)
        {
            Jobs = jobs;
            Service = service;
            GoodsIn = goodsIn;
            GoodsOut = goodsOut;
        }
    }

    /// <summary>
    /// Every number that shapes the city's economy, in one place so it can be tuned (and, later, changed by scenarios).
    /// Quantities are abstract "units per economy update", not real people or tonnes.
    /// </summary>
    public sealed class EconomyParameters
    {
        /// <summary>Share of residents who work.</summary>
        public double WorkingShare = 0.6;

        /// <summary>Shopping trips each resident wants to make.</summary>
        public double ShoppingPerResident = 1.0;

        /// <summary>Longest trip (route cost in metres) each kind of trip will make. Further away counts as unreachable.</summary>
        public double MaxWorkDistance = 2500.0;
        public double MaxGoodsDistance = 6000.0;
        public double MaxShoppingDistance = 1500.0;

        /// <summary>How many slices supply and demand are matched in (see CityEconomy). More is fairer and a little slower.</summary>
        public int MatchingRounds = 10;

        /// <summary>Zones need a little demand even in an empty city, or nothing could ever start.</summary>
        public double BaseResidentialDemand = 0.35;
        public double BaseCommercialDemand = 0.15;
        public double BaseIndustrialDemand = 0.20;

        /// <summary>How fast the demand bars follow the target (0 to 1 per update). Lower is steadier.</summary>
        public double DemandSmoothing = 0.3;

        private readonly Dictionary<BuildingShape, EconomyProfile> profiles = new Dictionary<BuildingShape, EconomyProfile>
        {
            //                                                       jobs service goodsIn goodsOut
            { BuildingShape.Shop,        new EconomyProfile(1.5,  8.0,  4.0, 0.0) },
            { BuildingShape.Mall,        new EconomyProfile(1.5,  6.0,  4.0, 0.0) },
            { BuildingShape.OfficeSlab,  new EconomyProfile(1.0,  0.3,  0.2, 0.0) },
            { BuildingShape.Tower,       new EconomyProfile(1.0,  0.3,  0.2, 0.0) },
            { BuildingShape.PodiumTower, new EconomyProfile(1.0,  0.5,  0.3, 0.0) },
            { BuildingShape.Shed,        new EconomyProfile(1.5,  0.0,  0.0, 3.0) },
            { BuildingShape.Warehouse,   new EconomyProfile(0.8,  0.0,  0.0, 2.0) },
            { BuildingShape.Factory,     new EconomyProfile(1.5,  0.0,  0.0, 8.0) },
            { BuildingShape.SiloYard,    new EconomyProfile(1.0,  0.0,  0.0, 3.0) }
        };

        public EconomyProfile ProfileOf(BuildingShape shape)
        {
            EconomyProfile profile;
            return profiles.TryGetValue(shape, out profile) ? profile : new EconomyProfile();
        }

        public void SetProfile(BuildingShape shape, EconomyProfile profile)
        {
            profiles[shape] = profile;
        }

        /// <summary>Footprint lots times floors: the size that profiles are multiplied by.</summary>
        public static double Units(Building b)
        {
            return b.Template.Width * b.Template.Depth * b.Floors;
        }
    }
}
