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
            var sb = new StringBuilder(640);
            sb.AppendLine("BUDGET — last quarter");
            sb.Append("Income     +").Append(DisplayUnits.Money(LastIncome)).Append('\n');
            sb.Append("Plant upkeep −").Append(DisplayUnits.Money(LastUpkeep)).Append('\n');
            sb.Append("Build drain  −").Append(DisplayUnits.Money(LastBuildDrain)).Append('\n');
            sb.Append("Reserve deal −").Append(DisplayUnits.Money(LastReserveCost)).Append('\n');
            if (LastOther > 0.01f) sb.Append("Other       −").Append(DisplayUnits.Money(LastOther)).Append('\n');
            sb.Append("Net          ").Append(DisplayUnits.Money(LastNet, signed: true)).Append('\n');
            sb.Append("\nYTD income ").Append(DisplayUnits.Money(YearIncome))
                .Append(" · YTD expense ").Append(DisplayUnits.Money(YearExpense)).Append('\n');
            sb.Append("Treasury now ").Append(DisplayUnits.Money(treasury)).Append('\n');
            sb.Append('(').Append(DisplayUnits.TreasuryExplain()).Append(')');
            return sb.ToString();
        }
    }
}
