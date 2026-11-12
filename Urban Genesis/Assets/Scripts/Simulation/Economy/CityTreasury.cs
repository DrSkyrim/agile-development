using System;

namespace UrbanGenesis.Simulation
{
    /// <summary>
    /// The city's money. Zoning and buildings are free; only roads cost money:
    ///  * building a road costs <see cref="BuildCostPerMetre"/> per metre (taken when it is placed),
    ///  * every road costs <see cref="UpkeepPerMetrePerWeek"/> per metre every week,
    ///  * bulldozing a road gives back <see cref="RefundShare"/> of its build cost.
    /// Income is tax on the economy that works: each employed worker and each shopper served pays a little each week,
    /// so a city without jobs and shops earns nothing.
    /// Money is off until <see cref="Enable"/> is called, so tests and tools can build without a budget.
    /// Money moves a little every tick; <see cref="WeeklyIncome"/> and <see cref="WeeklyUpkeep"/> are the current rates per game week.
    /// </summary>
    public sealed class CityTreasury
    {
        public const double DefaultStartingFunds = 50000.0;

        private readonly CitySimulation sim;

        public bool IsEnabled { get; private set; }

        /// <summary>Money in the bank. Can go below zero when upkeep is more than income.</summary>
        public double Balance { get; private set; }

        public double BuildCostPerMetre = 15.0;
        public double UpkeepPerMetrePerWeek = 0.5;
        public double RefundShare = 0.5;
        public double TaxPerEmployedWorkerPerWeek = 12.0;
        public double TaxPerShopperServedPerWeek = 2.0;

        public double WeeklyIncome { get; private set; }
        public double WeeklyUpkeep { get; private set; }
        public double WeeklyNet { get { return WeeklyIncome - WeeklyUpkeep; } }

        /// <summary>Raised when the balance changes because of a purchase or refund (not for the steady income per tick).</summary>
        public event Action Changed;

        internal CityTreasury(CitySimulation simulation)
        {
            sim = simulation;
        }

        /// <summary>Starts charging for roads, with this much money in the bank.</summary>
        public void Enable(double startingFunds)
        {
            IsEnabled = true;
            Balance = startingFunds;
            Recompute();
        }

        public void Disable()
        {
            IsEnabled = false;
        }

        /// <summary>What a road of this length costs (zero while money is off).</summary>
        public double RoadCost(double lengthMetres)
        {
            return IsEnabled ? lengthMetres * BuildCostPerMetre : 0.0;
        }

        public bool CanAfford(double cost)
        {
            return !IsEnabled || Balance >= cost;
        }

        internal void Charge(double cost)
        {
            if (IsEnabled && cost > 0.0)
            {
                Balance -= cost;
                RaiseChanged();
            }
        }

        internal void RefundRoad(double lengthMetres)
        {
            if (IsEnabled)
            {
                Balance += lengthMetres * BuildCostPerMetre * RefundShare;
                RaiseChanged();
            }
        }

        /// <summary>Takes in the weekly rates again (after an economy update or a road change).</summary>
        internal void Recompute()
        {
            EconomySnapshot s = sim.Economy.Snapshot;
            WeeklyIncome = s.Employed * TaxPerEmployedWorkerPerWeek + s.ShopServed * TaxPerShopperServedPerWeek;

            double roadMetres = 0.0;
            foreach (RoadSegment segment in sim.Roads.Segments)
            {
                roadMetres += segment.Length;
            }

            WeeklyUpkeep = roadMetres * UpkeepPerMetrePerWeek;
        }

        /// <summary>One tick of income and upkeep.</summary>
        internal void Tick()
        {
            if (IsEnabled)
            {
                Balance += WeeklyNet / GameTime.TicksPerWeek;
            }
        }

        private void RaiseChanged()
        {
            Action changed = Changed;
            if (changed != null)
            {
                changed();
            }
        }
    }
}
