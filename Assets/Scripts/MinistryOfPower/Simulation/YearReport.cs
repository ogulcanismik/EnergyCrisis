using System.Text;

namespace MinistryOfPower.Simulation
{
    /// <summary>End-of-year desk summary shown as a modal.</summary>
    public sealed class YearReport
    {
        public int Year;
        public float AdequacyAvg;
        public float Spend;
        public float CleanPctEnd;
        public float AffordAvg;
        public string BiggestEventTitle = "—";
        public float BiggestEventSeverity;
        public int EventCount;
        /// <summary>End treasury minus year-open treasury (income − spend net).</summary>
        public float NetTreasuryDelta;

        public string FormatModal()
        {
            var sb = new StringBuilder(360);
            sb.Append("Year ").Append(Year).Append(" closed.\n\n");
            sb.Append("Adequacy avg: ").Append(AdequacyAvg.ToString("0.0")).Append('\n');
            sb.Append("Affordability avg: ").Append(AffordAvg.ToString("0.0")).Append('\n');
            sb.Append("Clean % (end): ").Append(CleanPctEnd.ToString("0")).Append("%\n");
            sb.Append("Treasury spent (approx): ").Append(Spend.ToString("0.0")).Append('\n');
            sb.Append("Net treasury Δ: ").Append(NetTreasuryDelta >= 0f ? "+" : "")
                .Append(NetTreasuryDelta.ToString("0.0")).Append('\n');
            sb.Append("Events logged: ").Append(EventCount).Append('\n');
            sb.Append("Biggest event: ").Append(string.IsNullOrEmpty(BiggestEventTitle) ? "—" : BiggestEventTitle);
            if (BiggestEventSeverity > 0.01f)
                sb.Append(" (").Append((BiggestEventSeverity * 100f).ToString("0")).Append("% sev)");
            return sb.ToString();
        }
    }
}
