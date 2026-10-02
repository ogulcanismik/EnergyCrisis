using System;
using UnityEngine;
using UnityEngine.UI;
using MinistryOfPower.Simulation;
using MinistryOfPower.UI.Charts;

namespace MinistryOfPower.UI
{
    /// <summary>
    /// Hosts report visuals (mandate sparkline, budget bars, year summary bars)
    /// so left-panel text walls are backed by real charts.
    /// </summary>
    public sealed class ReportChartsHost : MonoBehaviour
    {
        private UguiMeshChart _mandateChart;
        private UguiMeshChart _budgetChart;
        private UguiMeshChart _yearChart;
        private GameObject _mandateHost;
        private GameObject _budgetHost;
        private GameObject _yearHost;

        public static ReportChartsHost Create(Transform sidePanel)
        {
            var go = new GameObject("ReportCharts", typeof(RectTransform));
            go.transform.SetParent(sidePanel, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var host = go.AddComponent<ReportChartsHost>();
            host.Build();
            return host;
        }

        private void Build()
        {
            _mandateHost = MakeSlot("MandateChartSlot", new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.48f));
            _mandateChart = UguiMeshChart.Create(_mandateHost.transform, "MandateChart",
                Vector2.zero, Vector2.one);
            _mandateChart.Configure("Clean % (quarterly)", v => v.ToString("0") + "%", autoY: false, yMin: 0f, yMax: 100f);

            _budgetHost = MakeSlot("BudgetChartSlot", new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.48f));
            _budgetChart = UguiMeshChart.Create(_budgetHost.transform, "BudgetChart",
                Vector2.zero, Vector2.one);
            _budgetChart.Configure("Last quarter ($ bn)", v => DisplayUnits.Money(v), autoY: true);

            HideAll();
        }

        /// <summary>Resize report chart slots (normalized panel Y height from GameTuning).</summary>
        public void ApplyChartHeight(float height)
        {
            float h = Mathf.Clamp(height, 0.2f, 0.55f);
            float y1 = 0.08f + h;
            ApplySlotAnchors(_mandateHost, new Vector2(0.04f, 0.08f), new Vector2(0.96f, y1));
            ApplySlotAnchors(_budgetHost, new Vector2(0.04f, 0.08f), new Vector2(0.96f, y1));
        }

        private static void ApplySlotAnchors(GameObject host, Vector2 min, Vector2 max)
        {
            if (host == null) return;
            var rt = host.GetComponent<RectTransform>();
            if (rt == null) return;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public void AttachYearChart(Transform yearModal)
        {
            if (_yearHost != null || yearModal == null) return;
            _yearHost = MakeSlotUnder(yearModal, "YearChartSlot",
                new Vector2(0.08f, 0.20f), new Vector2(0.92f, 0.48f));
            _yearChart = UguiMeshChart.Create(_yearHost.transform, "YearChart",
                Vector2.zero, Vector2.one);
            _yearChart.Configure("Year scoreboard", v => v.ToString("0.0"), autoY: true);
            _yearHost.SetActive(false);
        }

        public void ShowForMenu(ParadoxChromeHud.MenuId menu, GameSession s)
        {
            HidePanelCharts();
            if (s == null) return;

            if (menu == ParadoxChromeHud.MenuId.Mandate)
            {
                _mandateHost.SetActive(true);
                RenderMandate(s);
            }
            else if (menu == ParadoxChromeHud.MenuId.Budget)
            {
                _budgetHost.SetActive(true);
                RenderBudget(s);
            }
        }

        public void ShowYear(YearReport report)
        {
            if (_yearHost == null || _yearChart == null || report == null) return;
            _yearHost.SetActive(true);
            string[] labels = { "Adeq", "Aff", "Clean%", "Spend", "NetΔ" };
            float[] values =
            {
                report.AdequacyAvg,
                report.AffordAvg,
                report.CleanPctEnd,
                Mathf.Abs(report.Spend),
                report.NetTreasuryDelta
            };
            _yearChart.Configure("Year " + report.Year + " meters", FormatYearAxis, autoY: true);
            _yearChart.SetSeries(labels,
                new UguiMeshChart.Series
                {
                    Name = "Scoreboard",
                    Color = UiFactory.Hex("C4A35A"),
                    Kind = UguiMeshChart.SeriesKind.Bar,
                    Values = values
                });
        }

        public void HideYear()
        {
            if (_yearHost != null) _yearHost.SetActive(false);
        }

        public void HidePanelCharts()
        {
            if (_mandateHost != null) _mandateHost.SetActive(false);
            if (_budgetHost != null) _budgetHost.SetActive(false);
        }

        private void HideAll()
        {
            HidePanelCharts();
            HideYear();
        }

        private void RenderMandate(GameSession s)
        {
            var samples = s.Mandate?.CleanSamples;
            if (samples == null || samples.Count == 0)
            {
                _mandateChart.Configure("Clean % (quarterly)", v => v.ToString("0") + "%", autoY: false, 0f, 100f);
                _mandateChart.SetSeries(new[] { "—" },
                    new UguiMeshChart.Series
                    {
                        Name = "Clean%",
                        Color = UiFactory.Hex("6BA36A"),
                        Kind = UguiMeshChart.SeriesKind.Area,
                        Values = new[] { s.Meters.Transition }
                    });
                return;
            }

            int n = samples.Count;
            var values = new float[n];
            var labels = new string[n];
            for (int i = 0; i < n; i++)
            {
                values[i] = samples[i];
                labels[i] = n <= 8 || i % Mathf.Max(1, n / 6) == 0 || i == n - 1 ? "Q" + (i + 1) : "";
            }

            float ymin = 0f;
            float ymax = 100f;
            for (int i = 0; i < n; i++)
            {
                if (values[i] > ymax) ymax = values[i];
            }

            ymax = Mathf.Clamp(ymax + 5f, 20f, 100f);
            _mandateChart.Configure("Clean % (quarterly)", v => v.ToString("0") + "%", autoY: false, ymin, ymax);
            _mandateChart.SetSeries(labels,
                new UguiMeshChart.Series
                {
                    Name = "Clean%",
                    Color = UiFactory.Hex("6BA36A"),
                    Kind = UguiMeshChart.SeriesKind.Area,
                    Values = values
                },
                new UguiMeshChart.Series
                {
                    Name = "Win ≥70",
                    Color = UiFactory.Hex("C45A5A"),
                    Kind = UguiMeshChart.SeriesKind.Line,
                    Values = Flat(n, MandateTracker.WinTransition)
                });
        }

        private void RenderBudget(GameSession s)
        {
            BudgetLedger led = s.Ledger;
            if (led == null)
            {
                _budgetChart.ClearSeries();
                return;
            }

            string[] labels = { "Income", "Upkeep", "Builds", "Reserve", "Other", "Net" };
            float[] values =
            {
                led.LastIncome,
                -led.LastUpkeep,
                -led.LastBuildDrain,
                -led.LastReserveCost,
                -led.LastOther,
                led.LastNet
            };
            _budgetChart.Configure("Last quarter", v => DisplayUnits.Money(v), autoY: true);
            _budgetChart.SetSeries(labels,
                new UguiMeshChart.Series
                {
                    Name = "Cashflow",
                    Color = UiFactory.Hex("8B6914"),
                    Kind = UguiMeshChart.SeriesKind.Bar,
                    Values = values
                });
        }

        private static float[] Flat(int n, float v)
        {
            var a = new float[n];
            for (int i = 0; i < n; i++) a[i] = v;
            return a;
        }

        private static string FormatYearAxis(float v)
        {
            // Mix of pts / % / $bn — keep compact numeric; unit in title.
            if (Mathf.Abs(v) >= 10f) return v.ToString("0");
            return v.ToString("0.0");
        }

        private GameObject MakeSlot(string name, Vector2 amin, Vector2 amax)
        {
            return MakeSlotUnder(transform, name, amin, amax);
        }

        private static GameObject MakeSlotUnder(Transform parent, string name, Vector2 amin, Vector2 amax)
        {
            var img = UiFactory.Panel(parent, name, amin, amax, new Color(0f, 0f, 0f, 0.15f));
            img.raycastTarget = false;
            return img.gameObject;
        }
    }
}
