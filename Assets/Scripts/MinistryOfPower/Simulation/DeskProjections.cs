using System.Collections.Generic;
using System.Text;
using MinistryOfPower.Simulation;

namespace MinistryOfPower.Simulation
{
    /// <summary>
    /// Desk projections: build ETAs, runway, fuel cover, winter/build risk.
    /// </summary>
    public static class DeskProjections
    {
        private static readonly StringBuilder Sb = new StringBuilder(1024);

        public static string Build(
            GameSession session,
            IReadOnlyList<BuildOrder> orders,
            float quarterlyIncome)
        {
            Sb.Length = 0;
            Sb.AppendLine("PROJECTIONS");

            AppendBuilds(orders);
            AppendBudgetRunway(session.Budget, orders, quarterlyIncome, session);
            AppendFuelCover(session);
            AppendWinterRisk(session);
            AppendBuildRisk(orders, session);
            AppendModifiers(session.Modifiers);
            AppendExposure(session.Portfolio, session.Modifiers);
            AppendDeals(session);

            return Sb.ToString();
        }

        private static void AppendBuilds(IReadOnlyList<BuildOrder> orders)
        {
            bool any = false;
            for (int i = 0; i < orders.Count; i++)
            {
                BuildOrder o = orders[i];
                if (o.IsCancelled || o.IsComplete) continue;
                any = true;
                Sb.Append("· ").Append(o.DisplayName).Append(" online in ").Append(o.QuartersRemaining).Append('q');
                if (o.PaymentMode == BuildPaymentMode.PerQuarter && o.QuarterlyCost > 0f)
                {
                    Sb.Append(" (").Append(o.QuarterlyCost.ToString("0")).Append("/q)");
                }

                Sb.Append('\n');
            }

            if (!any) Sb.AppendLine("· No builds in flight");
        }

        private static void AppendBudgetRunway(
            float budget, IReadOnlyList<BuildOrder> orders, float quarterlyIncome, GameSession session)
        {
            float quarterlyBurn = 0f;
            for (int i = 0; i < orders.Count; i++)
            {
                BuildOrder o = orders[i];
                if (!o.IsCancelled && !o.IsComplete && o.PaymentMode == BuildPaymentMode.PerQuarter)
                {
                    quarterlyBurn += o.QuarterlyCost;
                }
            }

            // Include plant upkeep estimate
            float upkeep = 0f;
            var plants = session.Portfolio.Plants;
            for (int i = 0; i < plants.Count; i++)
            {
                if (!plants[i].IsRetired) upkeep += plants[i].QuarterlyUpkeep;
            }

            quarterlyBurn += upkeep;
            if (session.PrivateReserveMw > 0f) quarterlyBurn += session.PrivateReserveQuarterlyCost;

            float income = quarterlyIncome;
            if (session.Difficulty != null) { /* income already effective in session via _quarterlyIncome — use parameter */ }

            float net = income - quarterlyBurn;
            Sb.Append("· Treasury ").Append(budget.ToString("0.0"))
                .Append(" · net ").Append(net >= 0f ? "+" : "").Append(net.ToString("0.0")).Append("/q\n");

            if (budget < 0f) Sb.AppendLine("· Budget runway: OVERDRAWN");
            else if (net >= -0.01f) Sb.AppendLine("· Budget runway: stable / growing");
            else
            {
                float q = budget / -net;
                Sb.Append("· Budget runway: ~").Append(q.ToString("0.0")).Append("q at current burn\n");
            }
        }

        private static void AppendFuelCover(GameSession session)
        {
            ResourceStockpile r = session.Resources;
            float dailyCoal = 0f, dailyGas = 0f, dailyOil = 0f, dailyU = 0f;
            var plants = session.Portfolio.Plants;
            for (int i = 0; i < plants.Count; i++)
            {
                PlantInstance p = plants[i];
                if (p.IsRetired || p.DailyFuelUse <= 0f) continue;
                switch (p.Fuel)
                {
                    case FuelKind.Coal: dailyCoal += p.DailyFuelUse; break;
                    case FuelKind.Gas: dailyGas += p.DailyFuelUse; break;
                    case FuelKind.Oil: dailyOil += p.DailyFuelUse; break;
                    case FuelKind.Nuclear: dailyU += p.DailyFuelUse; break;
                }
            }

            Sb.AppendLine("· Fuel cover (days):");
            Sb.Append("  coal ").Append(Days(r.Coal, dailyCoal))
                .Append(" · gas ").Append(Days(r.Gas, dailyGas))
                .Append(" · oil ").Append(Days(r.Oil, dailyOil))
                .Append(" · U ").Append(Days(r.Uranium, dailyU)).Append('\n');
        }

        private static string Days(float stock, float daily)
        {
            if (daily <= 0.001f) return "n/a";
            return (stock / daily).ToString("0");
        }

        private static void AppendWinterRisk(GameSession session)
        {
            Season season = session.Clock.CurrentSeason;
            float fossil = session.Portfolio.FossilShare();
            float storage = session.Portfolio.FuelShare(FuelKind.Storage);
            float imports = session.Modifiers.ImportMwAvailable + session.PrivateReserveMw;
            float total = session.Portfolio.TotalCapacityMw() + imports;
            float reserveRatio = total > 1f ? (storage * session.Portfolio.TotalCapacityMw() + imports) / total : 0f;

            float risk = 0.35f;
            if (season == Season.Winter) risk += 0.35f;
            else if (season == Season.Autumn) risk += 0.15f;
            risk += (1f - fossil) * 0.1f; // ironically thin fossil peakers raise winter risk mid-transition
            if (storage < 0.05f) risk += 0.15f;
            if (reserveRatio < 0.12f) risk += 0.2f;
            if (session.Modifiers.ImportDisruptionDaysRemaining > 0) risk += 0.2f;
            risk = MathUtil.Clamp01(risk);

            string label = risk > 0.7f ? "HIGH" : risk > 0.45f ? "ELEVATED" : "MANAGEABLE";
            Sb.Append("· Winter / peak risk: ").Append(label)
                .Append(" (").Append((risk * 100f).ToString("0")).Append("%) — ")
                .Append(season).Append('\n');
        }

        private static void AppendBuildRisk(IReadOnlyList<BuildOrder> orders, GameSession session)
        {
            float quarterlyBurn = 0f;
            int longest = 0;
            for (int i = 0; i < orders.Count; i++)
            {
                BuildOrder o = orders[i];
                if (o.IsCancelled || o.IsComplete) continue;
                if (o.PaymentMode == BuildPaymentMode.PerQuarter) quarterlyBurn += o.QuarterlyCost;
                if (o.QuartersRemaining > longest) longest = o.QuartersRemaining;
            }

            if (longest <= 0)
            {
                Sb.AppendLine("· Build risk: none in flight");
                return;
            }

            float income = session.Scenario.QuarterlyIncome *
                           (session.Difficulty != null ? session.Difficulty.IncomeMultiplier : 1f);
            bool stressed = session.Budget < quarterlyBurn * 2f || quarterlyBurn > income * 0.9f;
            Sb.Append("· Build risk: ").Append(stressed ? "FUNDING STRESS" : "funded")
                .Append(" · longest ").Append(longest).Append("q\n");
        }

        private static void AppendDeals(GameSession session)
        {
            Sb.Append("· Private reserve: ").Append(session.PrivateReserveMw.ToString("0")).Append(" MW");
            if (session.PrivateReserveMw > 0f)
            {
                Sb.Append(" (").Append(session.PrivateReserveQuarterlyCost.ToString("0.0")).Append("/q)");
            }

            Sb.Append(" · Emergency import buffer: ")
                .Append(session.EmergencyImportMw.ToString("0")).Append(" MW (")
                .Append(session.EmergencyImportDaysRemaining).Append("d)\n");
        }

        private static void AppendModifiers(DayModifiers mods)
        {
            if (mods == null) return;
            bool any = false;
            if (mods.OilShockDaysRemaining > 0)
            {
                Sb.Append("· Oil shock ×").Append(mods.OilShockMultiplier.ToString("0.00"))
                    .Append(" (").Append(mods.OilShockDaysRemaining).Append("d)\n");
                any = true;
            }

            if (mods.DroughtDaysRemaining > 0)
            {
                Sb.Append("· Drought hydro ×").Append(mods.HydroFactorMul.ToString("0.00"))
                    .Append(" (").Append(mods.DroughtDaysRemaining).Append("d)\n");
                any = true;
            }

            if (mods.HeatwaveDaysRemaining > 0)
            {
                Sb.Append("· Heatwave demand ×").Append(mods.DemandFactorMul.ToString("0.00"))
                    .Append(" (").Append(mods.HeatwaveDaysRemaining).Append("d)\n");
                any = true;
            }

            if (mods.ImportDisruptionDaysRemaining > 0)
            {
                Sb.Append("· Imports ").Append(mods.ImportMwAvailable.ToString("0")).Append('/')
                    .Append(mods.ImportMwBaseline.ToString("0")).Append(" MW (")
                    .Append(mods.ImportDisruptionDaysRemaining).Append("d)\n");
                any = true;
            }

            if (!any) Sb.AppendLine("· No active shock modifiers");
        }

        private static void AppendExposure(PlantPortfolio portfolio, DayModifiers mods)
        {
            if (portfolio == null) return;
            PortfolioSnapshot snap = portfolio.CaptureSnapshot();
            Sb.Append("· Exposure oil/gas ").Append((snap.OilLinkedShare * 100f).ToString("0"))
                .Append("% · hydro ").Append((snap.HydroShare * 100f).ToString("0")).Append('%');
            if (mods != null && mods.ImportMwBaseline > 1f)
            {
                float dep = snap.ImportDependence(mods.ImportMwBaseline);
                Sb.Append(" · import ").Append((dep * 100f).ToString("0")).Append('%');
            }

            Sb.Append('\n');
        }
    }
}
