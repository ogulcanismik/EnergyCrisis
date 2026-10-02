using UnityEngine;
using MinistryOfPower.Simulation;

namespace MinistryOfPower.UI.Map
{
    /// <summary>Fuel / map chrome colors shared by markers and corridors.</summary>
    public static class MapPalette
    {
        public static Color Fuel(FuelKind fuel)
        {
            switch (fuel)
            {
                case FuelKind.Coal: return new Color(0.22f, 0.2f, 0.18f);
                case FuelKind.Gas: return new Color(0.72f, 0.55f, 0.28f);
                case FuelKind.Oil: return new Color(0.55f, 0.22f, 0.16f);
                case FuelKind.Solar: return new Color(0.92f, 0.74f, 0.18f);
                case FuelKind.Wind: return new Color(0.42f, 0.72f, 0.82f);
                case FuelKind.Hydro: return new Color(0.22f, 0.48f, 0.78f);
                case FuelKind.Nuclear: return new Color(0.35f, 0.82f, 0.42f);
                case FuelKind.Storage: return new Color(0.62f, 0.42f, 0.82f);
                case FuelKind.Biomass: return new Color(0.42f, 0.58f, 0.28f);
                default: return Color.gray;
            }
        }

        public static Color Border => new Color(0.12f, 0.12f, 0.14f, 0.95f);
        public static Color Coastline => new Color(0.08f, 0.1f, 0.14f, 1f);
    }
}
