using System.Text;

namespace MinistryOfPower.Simulation
{
    public enum RegionId
    {
        North = 0,
        Coast = 1,
        Desert = 2
    }

    /// <summary>
    /// Sim-facing regional data for click panels + plant siting.
    /// Map presentation shows 50 states; each state belongs to one RegionId group
    /// (North≈Midwest/Plains/Great Lakes, Coast≈Atlantic/Gulf SE, Desert≈Southwest/Mountain West).
    /// </summary>
    public static class RegionCatalog
    {
        public static string DisplayName(RegionId id)
        {
            switch (id)
            {
                case RegionId.North: return "Midwest / North";
                case RegionId.Coast: return "Atlantic Coast";
                case RegionId.Desert: return "Southwest Interior";
                default: return id.ToString();
            }
        }

        public static float DemandShare(RegionId id)
        {
            switch (id)
            {
                case RegionId.North: return 0.28f;
                case RegionId.Coast: return 0.48f;
                case RegionId.Desert: return 0.24f;
                default: return 0.33f;
            }
        }

        public static string LoadShedHint(RegionId id)
        {
            switch (id)
            {
                case RegionId.North: return "Industry / freight — sheds cut steel & rail demand.";
                case RegionId.Coast: return "Suburbs & services — voters notice first.";
                case RegionId.Desert: return "Transit & mining — sparse votes, hard optics.";
                default: return "Mixed load.";
            }
        }

        public static string PreferredShedLabel(RegionId id)
        {
            switch (id)
            {
                case RegionId.North: return "Industry";
                case RegionId.Coast: return "Suburbs";
                case RegionId.Desert: return "Transit";
                default: return "Mixed";
            }
        }

        public static RegionId DefaultRegionForFuel(FuelKind fuel)
        {
            switch (fuel)
            {
                case FuelKind.Coal:
                case FuelKind.Nuclear:
                case FuelKind.Hydro:
                case FuelKind.Wind:
                    return RegionId.North;
                case FuelKind.Gas:
                case FuelKind.Oil:
                case FuelKind.Storage:
                    return RegionId.Coast;
                case FuelKind.Solar:
                    return RegionId.Desert;
                default:
                    return RegionId.Coast;
            }
        }

        public static string FuelIcon(FuelKind fuel)
        {
            switch (fuel)
            {
                case FuelKind.Coal: return "■";
                case FuelKind.Gas: return "▲";
                case FuelKind.Oil: return "●";
                case FuelKind.Solar: return "☀";
                case FuelKind.Wind: return "≈";
                case FuelKind.Hydro: return "≈";
                case FuelKind.Nuclear: return "◆";
                case FuelKind.Storage: return "▣";
                default: return "·";
            }
        }

        public static string BuildDetail(GameSession session, RegionId id)
        {
            var sb = new StringBuilder(768);
            float share = DemandShare(id);
            float dem = session.LastReport.DemandMw > 0f ? session.LastReport.DemandMw * share : session.Scenario.BaseDemandMw * share;
            float adeq = session.Meters.Adequacy;
            string note = adeq < 50f
                ? "Adequacy thin — this region will feel brownouts first."
                : adeq < 70f
                    ? "Margins OK; evening peaks still bite here."
                    : "Comfortable cover — politics, not physics, is the risk.";

            bool selected = session.SelectedRegion == id;
            sb.Append(DisplayName(id));
            if (selected) sb.Append("  [SELECTED — builds/retire prefer here]");
            sb.Append('\n');
            sb.Append("Demand share ~").Append((share * 100f).ToString("0")).Append("%");
            sb.Append(" (~").Append(DisplayUnits.Capacity(dem)).Append(" today)\n\n");
            sb.Append("Sited plants:\n");
            AppendOwnedPlants(sb, session, id);
            sb.Append("\nAdequacy note: ").Append(note).Append('\n');
            sb.Append("Load-shed hint: ").Append(LoadShedHint(id)).Append('\n');
            sb.Append("Preferred shed target: ").Append(PreferredShedLabel(id));
            return sb.ToString();
        }

        private static void AppendOwnedPlants(StringBuilder sb, GameSession session, RegionId id)
        {
            var plants = session.Portfolio.Plants;
            int shown = 0;
            float mw = 0f;
            for (int i = 0; i < plants.Count; i++)
            {
                PlantInstance p = plants[i];
                if (p.IsRetired || p.Region != id) continue;
                sb.Append(FuelIcon(p.Fuel)).Append(' ').Append(p.DisplayName)
                    .Append("  ").Append(p.Fuel)
                    .Append("  ").Append(DisplayUnits.Capacity(p.CapacityMw)).Append('\n');
                mw += p.CapacityMw;
                shown++;
                if (shown >= 8) break;
            }

            if (shown == 0) sb.AppendLine("· Empty — order builds while this region is selected.");
            else sb.Append("Capacity here: ").Append(DisplayUnits.Capacity(mw)).Append('\n');
        }
    }
}
