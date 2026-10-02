using System;
using System.Globalization;

namespace MinistryOfPower.Simulation
{
    /// <summary>
    /// Central player-facing number formatting. Sim keeps raw floats;
    /// presentation labels money as billion/million currency, power as MW/GW, prices as $/MWh.
    /// Internal money is already in billions of the active currency (Federal ~180 maps to $180 bn).
    /// </summary>
    public static class DisplayUnits
    {
        public const string DefaultCurrencyCode = "USD";
        public const string DefaultCurrencyName = "US dollars";

        public static string CurrencyCode { get; private set; } = DefaultCurrencyCode;
        public static string CurrencyName { get; private set; } = DefaultCurrencyName;
        public static string CurrencySymbol { get; private set; } = "$";

        public static void Bind(ScenarioConfig scenario)
        {
            if (scenario == null)
            {
                Bind(DefaultCurrencyCode, DefaultCurrencyName);
                return;
            }

            Bind(scenario.CurrencyCode, scenario.CurrencyName);
        }

        public static void Bind(string currencyCode, string currencyName)
        {
            CurrencyCode = string.IsNullOrEmpty(currencyCode) ? DefaultCurrencyCode : currencyCode.Trim().ToUpperInvariant();
            CurrencyName = string.IsNullOrEmpty(currencyName)
                ? NameForCode(CurrencyCode)
                : currencyName.Trim();
            CurrencySymbol = SymbolForCode(CurrencyCode);
        }

        public static string TreasuryExplain()
        {
            return $"Ministry energy budget in billion {CurrencyName}.";
        }

        public static string TreasuryExplainShort()
        {
            return $"Treasury in billion {CurrencyName}";
        }

        /// <summary>Format a money amount already stored in billions (auto bn / m).</summary>
        public static string Money(float billions, bool signed = false)
        {
            float abs = Math.Abs(billions);
            string sign = "";
            if (signed)
            {
                if (billions > 0.0005f) sign = "+";
                else if (billions < -0.0005f) sign = "−";
            }
            else if (billions < -0.0005f)
            {
                sign = "−";
            }

            // Near-zero stays on the treasury scale (bn), not "$0 m".
            if (abs < 0.0005f)
                return sign + CurrencySymbol + "0 bn";

            // Prefer bn when |value| ≥ 1 bn (= 1000 m); else show millions.
            if (abs >= 0.9995f)
            {
                string body = FormatAmount(abs, abs >= 100f ? "0" : "0.#");
                return sign + CurrencySymbol + body + " bn";
            }

            float millions = abs * 1000f;
            string mBody = FormatAmount(millions, millions >= 100f ? "0" : "0.#");
            return sign + CurrencySymbol + mBody + " m";
        }

        public static string MoneyPerQuarter(float billions, bool signed = false)
        {
            return Money(billions, signed) + "/q";
        }

        public static string Capacity(float mw, bool signed = false)
        {
            float abs = Math.Abs(mw);
            string sign = "";
            if (signed)
            {
                if (mw > 0.05f) sign = "+";
                else if (mw < -0.05f) sign = "−";
            }
            else if (mw < -0.05f)
            {
                sign = "−";
            }

            if (abs >= 999.5f)
            {
                float gw = abs / 1000f;
                return sign + FormatAmount(gw, gw >= 10f ? "0.#" : "0.##") + " GW";
            }

            return sign + FormatAmount(abs, abs >= 100f ? "0" : "0.#") + " MW";
        }

        public static string CapacityPair(float supplyMw, float demandMw)
        {
            return $"Supply {Capacity(supplyMw)} − Demand {Capacity(demandMw)}";
        }

        public static string PricePerMwh(float price)
        {
            return CurrencySymbol + FormatAmount(Math.Abs(price), price >= 100f ? "0" : "0.#") + "/MWh";
        }

        public static string Points(float value, string format = "0")
        {
            return value.ToString(format, CultureInfo.InvariantCulture) + " pts";
        }

        public static string ConfidenceHud(float confidence)
        {
            // Spec example keeps "Conf 63"; unit lives in the tooltip.
            return "Conf  " + confidence.ToString("0", CultureInfo.InvariantCulture);
        }

        public static string ConfidenceTip()
        {
            return "Seat confidence in points (0–100).\nAdeq/Aff/Trans feed this meter.\nLobby retirements and crises sting it.";
        }

        public static string LobbyPts(float value01to100)
        {
            return value01to100.ToString("0", CultureInfo.InvariantCulture) + " pts";
        }

        private static string FormatAmount(float value, string format)
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        private static string SymbolForCode(string code)
        {
            switch (code)
            {
                case "USD": return "$";
                case "CAD": return "C$";
                case "EUR": return "€";
                case "GBP": return "£";
                default: return code + " ";
            }
        }

        private static string NameForCode(string code)
        {
            switch (code)
            {
                case "USD": return DefaultCurrencyName;
                case "CAD": return "Canadian dollars";
                case "EUR": return "euros";
                case "GBP": return "pounds sterling";
                default: return code;
            }
        }
    }
}
