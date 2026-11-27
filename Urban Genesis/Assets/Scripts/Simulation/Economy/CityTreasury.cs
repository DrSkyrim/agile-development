using System;

namespace UrbanGenesis.Simulation
{
    /// <summary>
    /// The city's money. Zoning and buildings are free; only roads cost money:
    ///  * building a road costs <see cref="BuildCostPerMetre"/> per metre for a street, <see cref="HighwayBuildCostPerMetre"/> for a highway (taken when it is placed),
    ///  * every road costs upkeep per metre every week (the outside connection, built by the state, is free),
    ///  * demolishing anything that was paid for (roads, cables, pipes, civic buildings) gives back <see cref="RefundShare"/> of its cost.
    /// Income is tax on the economy that works: each employed worker and each shopper served pays a little each week,
    /// so a city without jobs and shops earns nothing.
    /// Money is off until <see cref="Enable"/> is called, so tests and tools can build without a budget.
    /// Income and upkeep are settled once at the end of each game day; <see cref="WeeklyIncome"/> and <see cref="WeeklyUpkeep"/> are the current rates per game week.
    /// </summary>
    public sealed class CityTreasury
    {
        public const double DefaultStartingFunds = 100000.0;

        private readonly CitySimulation sim;

        public bool IsEnabled { get; private set; }

        /// <summary>Money in the bank. Can go below zero when upkeep is more than income.</summary>
        public double Balance { get; internal set; }

        public double BuildCostPerMetre = 15.0;
        public double UpkeepPerMetrePerWeek = 0.5;
        public double TwoLaneHighwayBuildCostPerMetre = 35.0;
        public double TwoLaneHighwayUpkeepPerMetrePerWeek = 1.2;
        public double AvenueBuildCostPerMetre = 30.0;
        public double AvenueUpkeepPerMetrePerWeek = 0.9;
        public double OneWayHighwayBuildCostPerMetre = 30.0;
        public double OneWayHighwayUpkeepPerMetrePerWeek = 1.0;
        public double HighwayBuildCostPerMetre = 45.0;
        public double HighwayUpkeepPerMetrePerWeek = 1.5;
        public double RefundShare = 0.5;
        public double TaxPerEmployedWorkerPerWeek = 12.0;
        public double TaxPerShopperServedPerWeek = 2.0;

        public double WeeklyIncome { get; private set; }
        public double WeeklyUpkeep { get; private set; }
        public double WeeklyNet { get { return WeeklyIncome - WeeklyUpkeep; } }

        /// <summary>Raised when the balance changes because of a purchase or refund (not for the steady income per tick).</summary>
        public event Action Changed;

        // The books of the current game day, for the end-of-day summary.
        public double DayStartBalance { get; private set; }
        public double SpentToday { get; private set; }
        public double RefundedToday { get; private set; }

        /// <summary>The figures of the day that just ended.</summary>
        public double LastDayStart { get; private set; }
        public double LastDayIncome { get; private set; }
        public double LastDayUpkeep { get; private set; }
        public double LastDaySpent { get; private set; }
        public double LastDayRefunded { get; private set; }
        public double LastDayEnd { get; private set; }
        public int DaysClosed { get; private set; }

        public double GrantedToday { get; private set; }
        public double LastDayGranted { get; private set; }

        /// <summary>Money given to the city (milestone rewards).</summary>
        public void Grant(double amount)
        {
            if (IsEnabled && amount > 0.0)
            {
                Balance += amount;
                GrantedToday += amount;
                RaiseChanged();
            }
        }

        /// <summary>Raised after each day's income and upkeep are settled.</summary>
        public event Action DayClosed;

        internal CityTreasury(CitySimulation simulation)
        {
            sim = simulation;
        }

        /// <summary>Starts charging for roads, with this much money in the bank.</summary>
        public void Enable(double startingFunds)
        {
            IsEnabled = true;
            Balance = startingFunds;
            DayStartBalance = startingFunds;
            Recompute();
        }

        public void Disable()
        {
            IsEnabled = false;
        }

        /// <summary>What a road of this length costs (zero while money is off).</summary>
        public double RoadCost(double lengthMetres, RoadType type = RoadType.Street)
        {
            return IsEnabled ? lengthMetres * BuildCostPerMetreOf(type) : 0.0;
        }

        public double BuildCostPerMetreOf(RoadType type)
        {
            switch (type)
            {
                case RoadType.Avenue: return AvenueBuildCostPerMetre;
                case RoadType.Highway: return HighwayBuildCostPerMetre;
                case RoadType.OneWayHighway: return OneWayHighwayBuildCostPerMetre;
                case RoadType.TwoLaneHighway: return TwoLaneHighwayBuildCostPerMetre;
                default: return BuildCostPerMetre;
            }
        }

        public double UpkeepPerMetreOf(RoadType type)
        {
            switch (type)
            {
                case RoadType.Avenue: return AvenueUpkeepPerMetrePerWeek;
                case RoadType.Highway: return HighwayUpkeepPerMetrePerWeek;
                case RoadType.OneWayHighway: return OneWayHighwayUpkeepPerMetrePerWeek;
                case RoadType.TwoLaneHighway: return TwoLaneHighwayUpkeepPerMetrePerWeek;
                default: return UpkeepPerMetrePerWeek;
            }
        }

        /// <summary>A new road must leave enough money to pay the deficit it causes for this many weeks.</summary>
        public double SustainWeeks = 4.0;

        /// <summary>
        /// Checks that spending <paramref name="cost"/> and adding <paramref name="extraUpkeepPerWeek"/> does not leave a city that
        /// runs out of money within <see cref="SustainWeeks"/> weeks. Returns null if fine, or a message for the player.
        /// </summary>
        public string CheckSustainable(double cost, double extraUpkeepPerWeek)
        {
            if (!IsEnabled)
            {
                return null;
            }

            double remaining = Balance - cost;
            double net = WeeklyNet - extraUpkeepPerWeek;
            if (net < 0.0 && remaining < -net * SustainWeeks)
            {
                return "Too expensive to keep: with this the upkeep (" + CitySimulation.FormatMoney(WeeklyUpkeep + extraUpkeepPerWeek)
                    + "/wk) outruns the tax income (" + CitySimulation.FormatMoney(WeeklyIncome) + "/wk) and the " + CitySimulation.FormatMoney(remaining)
                    + " left would be gone in under " + (int)SustainWeeks + " weeks. Zone land and grow the city first.";
            }

            return null;
        }

        /// <summary>Upkeep that could not be paid on the last day because the treasury ran dry.</summary>
        public double LastDayShortfall { get; private set; }

        public double PowerLinePerMetre = 10.0;
        public double WaterPipePerMetre = 14.0;

        public double UtilityCost(double length, UtilityKind kind)
        {
            return Math.Round(length * (kind == UtilityKind.Power ? PowerLinePerMetre : WaterPipePerMetre));
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
                SpentToday += cost;
                RaiseChanged();
            }
        }

        /// <summary>Puts money back after something the player paid for was demolished (see <see cref="RefundShare"/>).</summary>
        internal void Refund(double amount)
        {
            if (IsEnabled && amount > 0.0)
            {
                Balance += amount;
                RefundedToday += amount;
                RaiseChanged();
            }
        }

        /// <summary>What demolishing a road gives back: its share of the road's price and of the power cable on it.</summary>
        public double RoadRefund(RoadSegment segment)
        {
            if (segment.IsOutsideConnection) { return 0.0; }
            double paid = RoadCost(segment.Length, segment.Type) + (segment.HasPower ? UtilityCost(segment.Length, UtilityKind.Power) : 0.0);
            return paid * RefundShare;
        }

        /// <summary>Gives back part of what a bulldozed road (and its cable) cost. The outside connection was free, so it refunds nothing.</summary>
        internal void RefundRoad(RoadSegment segment)
        {
            Refund(RoadRefund(segment));
        }

        /// <summary>Takes in the weekly rates again (after an economy update or a road change).</summary>
        internal void Recompute()
        {
            EconomySnapshot s = sim.Economy.Snapshot;
            WeeklyIncome = s.Employed * TaxPerEmployedWorkerPerWeek + s.ShopServed * TaxPerShopperServedPerWeek;

            double upkeep = 0.0;
            foreach (RoadSegment segment in sim.Roads.Segments)
            {
                if (!segment.IsOutsideConnection)
                {
                    upkeep += segment.Length * UpkeepPerMetreOf(segment.Type);
                }
            }

            WeeklyUpkeep = upkeep;
        }

        private int ticksIntoDay;
        internal int TicksIntoDay { get { return ticksIntoDay; } set { ticksIntoDay = value; } }

        /// <summary>Counts a tick. At the end of each game day the day's income and upkeep are settled in one go.</summary>
        internal void Tick()
        {
            if (!IsEnabled)
            {
                return;
            }

            ticksIntoDay++;
            if (ticksIntoDay >= GameTime.TicksPerDay)
            {
                ticksIntoDay = 0;
                LastDayStart = DayStartBalance;
                LastDayIncome = WeeklyIncome / 7.0;
                LastDayUpkeep = WeeklyUpkeep / 7.0;
                LastDaySpent = SpentToday;
                LastDayRefunded = RefundedToday;
                LastDayGranted = GrantedToday;
                GrantedToday = 0.0;
                Balance += WeeklyNet / 7.0;
                LastDayShortfall = 0.0;
                if (Balance < 0.0)
                {
                    LastDayShortfall = -Balance;
                    Balance = 0.0;
                }

                LastDayEnd = Balance;
                DayStartBalance = Balance;
                SpentToday = 0.0;
                RefundedToday = 0.0;
                DaysClosed++;
                RaiseChanged();
                Action closed = DayClosed;
                if (closed != null)
                {
                    closed();
                }
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
