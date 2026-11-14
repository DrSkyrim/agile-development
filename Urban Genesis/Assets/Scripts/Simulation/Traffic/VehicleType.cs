namespace UrbanGenesis.Simulation.Traffic
{
    public enum VehicleType
    {
        Car = 0,
        ShortTruck = 1,
        LongTruck = 2
    }

    /// <summary>Size and driving character of each kind of vehicle. Lengths are in metres.</summary>
    public static class VehicleSpecs
    {
        public static double Length(VehicleType type)
        {
            return type == VehicleType.Car ? 4.4 : type == VehicleType.ShortTruck ? 7.5 : 16.5;
        }

        public static double Width(VehicleType type)
        {
            return type == VehicleType.Car ? 1.8 : 2.4;
        }

        public static double Height(VehicleType type)
        {
            return type == VehicleType.Car ? 1.5 : type == VehicleType.ShortTruck ? 3.2 : 3.8;
        }

        /// <summary>How fast the vehicle likes to go compared with a car.</summary>
        public static double SpeedFactor(VehicleType type)
        {
            return type == VehicleType.Car ? 1.0 : type == VehicleType.ShortTruck ? 0.85 : 0.75;
        }

        /// <summary>Comfortable acceleration, metres per second squared.</summary>
        public static double Acceleration(VehicleType type)
        {
            return type == VehicleType.Car ? 2.6 : type == VehicleType.ShortTruck ? 1.8 : 1.2;
        }
    }
}
