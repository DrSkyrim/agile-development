namespace UrbanGenesis.Simulation.Traffic
{
    public enum VehicleType
    {
        Car = 0,
        ShortTruck = 1,
        LongTruck = 2,
        Ambulance = 3,
        PoliceCar = 4,
        FireTruck = 5,
        SchoolBus = 6
    }

    /// <summary>Size and driving character of each kind of vehicle. Lengths are in metres.</summary>
    public static class VehicleSpecs
    {
        public static double Length(VehicleType type)
        {
            switch (type)
            {
                case VehicleType.Car: return 4.4;
                case VehicleType.ShortTruck: return 7.5;
                case VehicleType.Ambulance: return 5.6;
                case VehicleType.PoliceCar: return 4.6;
                case VehicleType.FireTruck: return 9.5;
                case VehicleType.SchoolBus: return 10.5;
                default: return 16.5;
            }
        }

        public static double Width(VehicleType type)
        {
            return type == VehicleType.Car || type == VehicleType.PoliceCar ? 1.8 : 2.4;
        }

        public static double Height(VehicleType type)
        {
            switch (type)
            {
                case VehicleType.Car: case VehicleType.PoliceCar: return 1.5;
                case VehicleType.Ambulance: return 2.4;
                case VehicleType.FireTruck: return 3.3;
                case VehicleType.SchoolBus: return 3.0;
                case VehicleType.ShortTruck: return 3.2;
                default: return 3.8;
            }
        }

        /// <summary>How fast the vehicle likes to go compared with a car.</summary>
        public static double SpeedFactor(VehicleType type)
        {
            switch (type)
            {
                case VehicleType.Car: return 1.0;
                case VehicleType.PoliceCar: case VehicleType.Ambulance: return 1.15;
                case VehicleType.FireTruck: return 1.0;
                case VehicleType.SchoolBus: return 0.85;
                case VehicleType.ShortTruck: return 0.85;
                default: return 0.75;
            }
        }

        /// <summary>Comfortable acceleration, metres per second squared.</summary>
        public static double Acceleration(VehicleType type)
        {
            switch (type)
            {
                case VehicleType.Car: case VehicleType.PoliceCar: case VehicleType.Ambulance: return 2.6;
                case VehicleType.FireTruck: return 2.0;
                case VehicleType.SchoolBus: return 1.6;
                case VehicleType.ShortTruck: return 1.8;
                default: return 1.2;
            }
        }
    }
}
