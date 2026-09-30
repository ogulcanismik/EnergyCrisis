using System.Text;

namespace MinistryOfPower.Simulation
{
    /// <summary>Last quarter + running year budget breakdown.</summary>
    public sealed class BudgetLedger
    {
        public float LastIncome;
        public float LastUpkeep;
        public float LastBuildDrain;
        public float LastReserveCost;
        public float LastOther;
        public float LastNet;
        public float YearIncome;
        public float YearExpense;
        public int LastQuarterIndex = -1;

        public void RecordQuarter(float income, float upkeep, float build, float reserve, float other)
        {
            LastIncome = income;
            LastUpkeep = upkeep;
            LastBuildDrain = build;
            LastReserveCost = reserve;
            LastOther = other;
            LastNet = income - upkeep - build - reserve - other;
            YearIncome += income;
            YearExpense += upkeep + build + reserve + other;
        }

        public void OnNewYear()
        {
            YearIncome = 0f;
            YearExpense = 0f;
        }

        public string FormatPanel(float treasury)
        {
            var sb = new StringBuilder(512);
            sb.AppendLine("BUDGET — last quarter");
            sb.Append("Income     +").Append(LastIncome.ToString("0.0")).Append('\n');
            sb.Append("Plant upkeep −").Append(LastUpkeep.ToString("0.0")).Append('\n');
            sb.Append("Build drain  −").Append(LastBuildDrain.ToString("0.0")).Append('\n');
            sb.Append("Reserve deal −").Append(LastReserveCost.ToString("0.0")).Append('\n');
            if (LastOther > 0.01f) sb.Append("Other       −").Append(LastOther.ToString("0.0")).Append('\n');
            sb.Append("Net          ").Append(LastNet >= 0f ? "+" : "").Append(LastNet.ToString("0.0")).Append('\n');
            sb.Append("\nYTD income ").Append(YearIncome.ToString("0.0"))
                .Append(" · YTD expense ").Append(YearExpense.ToString("0.0")).Append('\n');
            sb.Append("Treasury now ").Append(treasury.ToString("0.0"));
            return sb.ToString();
        }
    }
}
