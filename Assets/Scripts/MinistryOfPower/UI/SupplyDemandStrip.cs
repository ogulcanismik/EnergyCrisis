using UnityEngine;
using UnityEngine.UI;
using MinistryOfPower.Simulation;
using MinistryOfPower.UI.Charts;

namespace MinistryOfPower.UI
{
    /// <summary>24h demand/supply curve with playhead for the bottom dispatch panel.</summary>
    public sealed class SupplyDemandStrip : MonoBehaviour
    {
        private UguiMeshChart _chart;
        private RectTransform _playhead;
        private Text _caption;
        private readonly float[] _demand = new float[24];
        private readonly float[] _supply = new float[24];
        private readonly string[] _hours =
        {
            "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11",
            "12", "13", "14", "15", "16", "17", "18", "19", "20", "21", "22", "23"
        };

        public static SupplyDemandStrip Create(Transform parent, Vector2 amin, Vector2 amax)
        {
            var host = UiFactory.Panel(parent, "DayStrip", amin, amax, UiFactory.Hex("15110C"));
            var strip = host.gameObject.AddComponent<SupplyDemandStrip>();
            strip.Build(host.transform);
            return strip;
        }

        private void Build(Transform parent)
        {
            _caption = UiFactory.Label(parent, "Cap", UiFactory.Hex("C4A35A"), 11, FontStyle.Bold,
                new Vector2(0.01f, 0.88f), new Vector2(0.99f, 0.99f));
            _caption.text = "24h load (amber) / supply (teal)";

            _chart = UguiMeshChart.Create(parent, "DispatchChart",
                new Vector2(0.01f, 0.02f), new Vector2(0.99f, 0.87f));
            _chart.Configure("24h load / supply", v => DisplayUnits.Capacity(v), autoY: true);

            var ph = UiFactory.Panel(parent, "Playhead", new Vector2(0.18f, 0.08f), new Vector2(0.188f, 0.82f),
                UiFactory.Hex("E7DCC8"));
            _playhead = ph.rectTransform;
            ph.raycastTarget = false;
        }

        public void Render(GameSession session)
        {
            if (session?.DayDemandCurve == null || _chart == null) return;

            for (int i = 0; i < 24; i++)
            {
                _demand[i] = session.DayDemandCurve[i];
                _supply[i] = session.DaySupplyCurve[i];
            }

            _chart.Configure("24h load / supply", v => DisplayUnits.Capacity(v), autoY: true);
            _chart.SetSeries(_hours,
                new UguiMeshChart.Series
                {
                    Name = "Load",
                    Color = UiFactory.Hex("C4A35A"),
                    Kind = UguiMeshChart.SeriesKind.Area,
                    Values = _demand
                },
                new UguiMeshChart.Series
                {
                    Name = "Supply",
                    Color = UiFactory.Hex("3FA88A"),
                    Kind = UguiMeshChart.SeriesKind.Line,
                    Values = _supply
                });

            float hour = session.Clock.DayFraction * 24f;
            float t = Mathf.Clamp01(session.Clock.DayFraction);
            // Playhead parented to DayStrip; chart occupies 0.01–0.99 x and 0.02–0.87 y
            float plotL = 0.01f + (0.99f - 0.01f) * 0.18f;
            float plotR = 0.01f + (0.99f - 0.01f) * 0.98f;
            float x = Mathf.Lerp(plotL, plotR, t);
            _playhead.anchorMin = new Vector2(x, 0.08f);
            _playhead.anchorMax = new Vector2(Mathf.Min(x + 0.006f, 0.99f), 0.82f);

            float margin = session.LastReport.SupplyMw - session.LastReport.DemandMw;
            string peak = IsPeakHour(hour) ? "PEAK" : "off-peak";
            _caption.text =
                "24h dispatch · " + hour.ToString("0.0") + "h (" + peak + ") · dem " +
                DisplayUnits.Capacity(session.LastReport.DemandMw) + " / sup " +
                DisplayUnits.Capacity(session.LastReport.SupplyMw) + " · margin " +
                DisplayUnits.Capacity(margin, signed: true);
        }

        private static bool IsPeakHour(float hour)
        {
            return (hour >= 7f && hour <= 10f) || (hour >= 17f && hour <= 21f);
        }
    }
}
