namespace MinistryOfPower.Simulation
{
    public enum FuelKind
    {
        Coal = 0,
        Gas = 1,
        Oil = 2,
        Solar = 3,
        Wind = 4,
        Hydro = 5,
        Nuclear = 6,
        Storage = 7
    }

    public static class FuelKindExtensions
    {
        public static bool IsFossil(this FuelKind kind)
        {
            return kind == FuelKind.Coal || kind == FuelKind.Gas || kind == FuelKind.Oil;
        }

        public static bool IsClean(this FuelKind kind)
        {
            return kind == FuelKind.Solar
                   || kind == FuelKind.Wind
                   || kind == FuelKind.Hydro
                   || kind == FuelKind.Nuclear
                   || kind == FuelKind.Storage;
        }

        public static bool IsOilLinked(this FuelKind kind)
        {
            return kind == FuelKind.Oil || kind == FuelKind.Gas;
        }

        public static bool IsWeatherSensitive(this FuelKind kind)
        {
            return kind == FuelKind.Solar || kind == FuelKind.Wind || kind == FuelKind.Hydro;
        }
    }
}
